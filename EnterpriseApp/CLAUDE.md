# CLAUDE.md — EnterpriseApp

This file tells Claude how to work with this codebase. Read this entire file before making any changes.

---

## Project Overview

Production-grade ASP.NET Core 8 enterprise API running in a distributed environment (multiple pods behind a load balancer). Built with Clean Architecture principles.

---

## Solution Structure

```text
EnterpriseApp/
├── src/
│   ├── EnterpriseApp.API              # Controllers, Middleware, Program.cs
│   ├── EnterpriseApp.Application      # Services, DTOs, Validators, Interfaces
│   ├── EnterpriseApp.Domain           # Entities, Repositories (interfaces), Exceptions
│   └── EnterpriseApp.Infrastructure   # EF Core, Dapper, Redis, Blob, Outbox, Security
├── tests/
│   └── EnterpriseApp.Tests            # NUnit tests
├── docker-compose.yml
├── Dockerfile
└── .editorconfig
```

---

## Architecture Rules (STRICT — never violate these)

### Layer dependencies
- `API` -> depends on `Application`
- `Application` -> depends on `Domain`
- `Infrastructure` -> depends on `Application` + `Domain`
- `Domain` -> depends on nothing
- Never reference `Infrastructure` directly from `API` or `Application`

### ORM Strategy (STRICTLY ENFORCED)
- **EF Core -> write operations ONLY** (Insert, Update, Delete, SaveChanges)
- **Dapper -> read-only queries ONLY** (SELECT)
- NEVER mix EF and Dapper in the same class or method
- EF repositories live in: `Infrastructure/Persistence/Repositories/`
- Dapper query services live in: `Infrastructure/Queries/`

### No business logic in controllers
- Controllers only: validate input, call service, return response
- All business logic lives in `Application/Services/`

---

## Naming Conventions (.editorconfig enforced)

| Type | Convention | Example |
|---|---|---|
| Classes | PascalCase | `ProductService` |
| Interfaces | PascalCase + I prefix | `IProductService` |
| Methods | PascalCase | `GetProductsAsync` |
| Public properties | PascalCase | `CustomerName` |
| Private fields | camelCase + underscore | `_productService` |
| Parameters | camelCase | `cancellationToken` |
| Local variables | camelCase | `productDto` |

Never use: `temp`, `obj`, `data`, `data1`, `result2`, `x`, `y`

---

## CancellationToken (MANDATORY)

Every async method MUST accept and pass CancellationToken:

```csharp
// Controller
public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)

// Service
public async Task<ProductDto> GetProductAsync(Guid id, CancellationToken cancellationToken)

// Repository
public async Task AddAsync(Product product, CancellationToken cancellationToken)

// Dapper
await connection.QueryAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

// EF Core
await _context.SaveChangesAsync(cancellationToken);
```

Missing CancellationToken = violation. Fix before submitting.

---

## Standard API Response Format

All endpoints MUST return this shape:

```json
{
  "success": true,
  "message": "string",
  "data": {},
  "traceId": "correlation-id-here"
}
```

Use the `ApiResponse<T>` class in `Application/DTOs/`:

```csharp
return Ok(ApiResponse<ProductDto>.Ok(product, HttpContext.TraceIdentifier));
return BadRequest(ApiResponse<ProductDto>.Fail("Validation failed", HttpContext.TraceIdentifier));
```

---

## HTTP Status Codes (STRICT)

| Scenario | Code |
|---|---|
| Success | 200 |
| Resource created | 201 |
| Validation failure | 400 |
| Authentication failure | 401 |
| Authorization failure | 403 |
| Resource not found | 404 |
| Concurrency conflict | 409 |
| Too many requests | 429 |
| Unexpected error | 500 |

---

## Middleware Pipeline Order (Program.cs — do not change order)

```csharp
app.UseResponseCompression();
app.UseMiddleware<CorrelationIdMiddleware>();     // 1. Must be first
app.UseMiddleware<GlobalExceptionMiddleware>();   // 2. Catches everything below
app.UseMiddleware<RedisRateLimitingMiddleware>(); // 3. Rate limiting
app.UseMiddleware<HmacAuthenticationMiddleware>();// 4. HMAC validation
app.UseCors("DefaultPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

---

## Logging Rules (Serilog)

```csharp
// Information — business operations, request start/end, successful DB ops
_logger.LogInformation("Product {ProductId} created successfully", product.Id);

// Warning — expected failures, rate limit exceeded, concurrency conflicts
_logger.LogWarning("Rate limit exceeded for client {ClientIp}", clientIp);

// Error — exceptions, failed DB calls, external service failures
_logger.LogError(ex, "Failed to create product {ProductName}", request.Name);

// Debug — only enabled via config, never in production by default
_logger.LogDebug("SQL query executed in {ElapsedMs}ms", elapsed);
```

**NEVER log:** passwords, tokens, secrets, credit card numbers, personal data.

Debug logs are controlled via `appsettings.json`:

```json
"Logging": {
  "EnableDebugLogs": false
}
```

---

## Security Rules

- No secrets in `appsettings.json` or environment variables
- Secrets loaded from Azure Key Vault via `ISecretProvider`
- HMAC signature validated on machine-to-machine endpoints
- JWT used for user authentication with refresh token rotation
- FluentValidation on all request DTOs — never trust raw input
- Input sanitized to prevent XSS/SQL injection

---

## Transaction Handling

For multi-table operations always use explicit transactions:

```csharp
await _unitOfWork.BeginTransactionAsync(cancellationToken);
try
{
    // EF write — table 1
    // EF write — table 2 (e.g. OutboxMessage)
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    await _unitOfWork.CommitTransactionAsync(cancellationToken);
}
catch
{
    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
    throw;
}
```

Single `SaveChangesAsync` call per transaction — never call it multiple times.

---

## Outbox Pattern

When raising domain events, always write the OutboxMessage in the same transaction as the primary entity. Never use TransactionScope.

```csharp
// In the same EF transaction:
await _repository.AddAsync(product, cancellationToken);
await _outboxRepository.AddAsync(OutboxMessage.Create("ProductCreated", payload), cancellationToken);
await _unitOfWork.SaveChangesAsync(cancellationToken); // single commit
```

OutboxProcessor (`Infrastructure/Outbox/OutboxProcessor.cs`) polls every 10 seconds and publishes unprocessed messages.

---

## Concurrency

All entities that can be updated concurrently must have a `RowVersion` property:

```csharp
public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
```

Configured in DbContext as:

```csharp
entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
```

`DbUpdateConcurrencyException` is handled in `GlobalExceptionMiddleware` -> returns 409.

---

## Rate Limiting

Redis-backed fixed window counter in `RedisRateLimitingMiddleware`.

Configuration in `appsettings.json`:

```json
"RateLimit": {
  "RequestsPerWindow": 100,
  "WindowSeconds": 60
}
```

Key format: `rate_limit:{clientIp}` — no timestamp in key, expiry handles window reset.

---

## File Upload Rules

- Always stream — never load full file into memory
- Max size: 50MB
- Allowed types: `application/pdf`, `image/jpeg`, `image/png`, `text/csv`
- Upload directly to Azure Blob Storage via `IBlobStorageService`
- Use `[DisableRequestSizeLimit]` + `[RequestFormLimits]` on upload endpoints

---

## API Versioning

All controllers must be versioned:

```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
```

New versions get a new controller file in `Controllers/V2/` — never modify V1 controllers.

---

## XML Documentation (mandatory on all controllers)

```csharp
/// <summary>Creates a new product.</summary>
/// <param name="request">Product creation payload.</param>
/// <param name="cancellationToken">Token to observe for cancellation.</param>
/// <returns>
/// 201 — product created.
/// 400 — validation failure.
/// 401 — unauthenticated.
/// </returns>
```

---

## Unit Testing Rules

- Framework: NUnit + Moq
- Test files live in `tests/EnterpriseApp.Tests/`
- Mirror the src structure: `Tests/Services/`, `Tests/Validators/`, `Tests/Middleware/`
- Mock all dependencies — no real DB or Redis in unit tests
- Every service method needs at least one happy path + one failure path test
- Include at least one enforcement test (e.g. CancellationToken forwarding, EF not used for reads)

```csharp
[Test]
[Description("Enforcement: CancellationToken must be forwarded to repository")]
public async Task CreateProductAsync_ForwardsCancellationToken_ToRepository()
{
    using var cts = new CancellationTokenSource();
    var token = cts.Token;
    // ...
    _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Product>(), token), Times.Once);
}
```

---

## Running Locally

```bash
# Start all services
docker compose up -d

# View API logs
docker compose logs -f api

# Filter logs by correlation ID
docker compose logs -f api | findstr "YOUR-CORRELATION-ID"

# Filter errors only
docker compose logs -f api | findstr /i "error exception"

# Access Redis CLI
docker exec -it enterpriseapp-redis-1 redis-cli

# Rebuild after code changes
docker compose up -d --build api

# Force clean rebuild
docker compose down
docker compose build --no-cache api
docker compose up -d
```

---

## Services in docker-compose

| Service | Port | Purpose |
|---|---|---|
| `api` | 8080 | ASP.NET Core API |
| `sqlserver` | 1433 | SQL Server database |
| `redis` | 6379 | Cache + rate limiting |
| `azurite` | 10000 | Azure Blob Storage emulator |

---

## Common Mistakes to Avoid

- Do NOT use EF Core for reads — use Dapper query services
- Do NOT put business logic in controllers
- Do NOT forget CancellationToken on any async method
- Do NOT log sensitive data
- Do NOT use `TransactionScope` — use `BeginTransactionAsync`
- Do NOT include timestamp in Redis rate limit keys
- Do NOT call `SaveChangesAsync` multiple times in one transaction
- Do NOT add secrets to `appsettings.json`
- Do NOT change middleware pipeline order in `Program.cs`
- Do NOT modify V1 controllers — create V2 instead

---

## Before Submitting Any Code Change

Run through this checklist:

- [ ] EF Core used ONLY for writes
- [ ] Dapper used ONLY for reads
- [ ] CancellationToken present on every async method
- [ ] Naming conventions followed
- [ ] XML documentation on all controller methods
- [ ] No sensitive data in logs
- [ ] Correct HTTP status codes returned
- [ ] Standard ApiResponse format used
- [ ] Unit tests added or updated
- [ ] No secrets in config files
