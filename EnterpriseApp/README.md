# EnterpriseApp — Production-Grade ASP.NET Core 8 API

A fully-implemented, production-ready ASP.NET Core 8 enterprise application following Clean Architecture, SOLID principles, and strict engineering standards across all 23 requirements.

---

## 📁 Folder Structure

```
EnterpriseApp/
├── .editorconfig                          # Naming/style rules (enforced in CI)
├── .github/workflows/ci-cd.yml           # GitHub Actions pipeline
├── docker-compose.yml                     # Local dev stack (API + SQL + Redis + Azurite)
├── Dockerfile                             # Multi-stage production image
├── EnterpriseApp.sln
│
├── src/
│   ├── EnterpriseApp.Domain/              # Layer 1 — zero dependencies
│   │   ├── Common/BaseEntity.cs           # RowVersion concurrency, audit fields
│   │   ├── Entities/                      # Product, Order, OrderItem, OutboxMessage, RefreshToken
│   │   ├── Repositories/IRepositories.cs  # Write-side interfaces (EF Core)
│   │   └── Exceptions/DomainExceptions.cs # NotFoundException, ConcurrencyException, etc.
│   │
│   ├── EnterpriseApp.Application/         # Layer 2 — business logic
│   │   ├── DTOs/ApplicationDtos.cs        # Request/Response/ApiResponse<T>
│   │   ├── Interfaces/IServices.cs        # Service + query + blob + cache + secret interfaces
│   │   ├── Services/
│   │   │   ├── ProductService.cs          # EF writes + Dapper reads + Redis cache
│   │   │   ├── OrderService.cs            # Multi-table EF transaction + Outbox pattern
│   │   │   └── AuthService.cs             # JWT + refresh token rotation
│   │   └── Validators/RequestValidators.cs # FluentValidation rules
│   │
│   ├── EnterpriseApp.Infrastructure/      # Layer 3 — external concerns
│   │   ├── Persistence/
│   │   │   ├── Context/AppDbContext.cs    # EF Core DbContext (write-only)
│   │   │   └── Repositories/             # EF repositories (write-only)
│   │   ├── Queries/DapperQueryServices.cs # Dapper read-only query services
│   │   ├── Outbox/OutboxProcessor.cs      # Background Outbox polling + dispatch
│   │   ├── Caching/
│   │   │   ├── RedisCacheService.cs       # Redis ICacheService
│   │   │   └── RedisRateLimitingMiddleware.cs # Distributed rate limiter
│   │   ├── Security/AzureKeyVaultSecretProvider.cs # No secrets in config
│   │   ├── Storage/AzureBlobStorageService.cs     # Stream-based blob upload
│   │   └── Logging/SerilogConfiguration.cs        # JSON structured logging
│   │
│   └── EnterpriseApp.API/                 # Layer 4 — presentation
│       ├── Controllers/
│       │   ├── HealthController.cs        # /health — K8s probes
│       │   └── V1/
│       │       ├── ProductsController.cs  # /api/v1/products
│       │       ├── OrdersController.cs    # /api/v1/orders
│       │       ├── AuthController.cs      # /api/v1/auth
│       │       └── FilesController.cs     # /api/v1/files/upload (streaming)
│       ├── Middleware/
│       │   ├── CorrelationIdMiddleware.cs       # Correlation ID + request timing
│       │   ├── GlobalExceptionMiddleware.cs     # Error → HTTP status mapping
│       │   └── HmacAuthenticationMiddleware.cs  # HMAC-SHA256 signature validation
│       ├── Extensions/ServiceCollectionExtensions.cs # DI wiring
│       ├── Program.cs                     # App bootstrap + middleware pipeline
│       └── appsettings.json               # No secrets — all come from Key Vault
│
└── tests/
    └── EnterpriseApp.Tests/
        ├── Services/ProductServiceTests.cs     # Service logic + cache + CancellationToken enforcement
        ├── Validators/ValidatorTests.cs         # FluentValidation coverage
        └── Middleware/GlobalExceptionMiddlewareTests.cs # HTTP status + safe error messages
```

---

## ✅ Requirements Coverage

| # | Requirement | Implementation |
|---|---|---|
| 1 | Clean Architecture | Domain → Application → Infrastructure → API |
| 2 | Naming Conventions | `.editorconfig` enforces camelCase / PascalCase / `_field` / `IInterface` |
| 3 | API Documentation | `///` XML comments on all controllers and actions + Swagger |
| 4 | API Versioning | URL-based `/api/v1/...` via `Asp.Versioning.Mvc` |
| 5 | ORM Strategy | EF Core **write-only** · Dapper **read-only** — strictly separated |
| 6 | Transactions | `BeginTransactionAsync` → single `SaveChanges` → `CommitAsync` / `RollbackAsync` |
| 7 | Concurrency | `RowVersion` / `IsConcurrencyToken()` → `DbUpdateConcurrencyException` → 409 |
| 8 | Distributed TX | Outbox Pattern (no `TransactionScope`) + `OutboxProcessor` background service |
| 9 | Security | HMAC middleware · JWT + Refresh Token rotation · FluentValidation · Key Vault secrets · masked logs |
| 10 | Rate Limiting | Redis fixed-window per-IP counter middleware |
| 11 | Logging | Serilog structured JSON · Info/Error/Debug levels · Correlation ID · debug gated by config |
| 12 | Response Codes | 200/201/400/401/403/404/409/500 — strictly mapped |
| 13 | Response Format | `ApiResponse<T>` with `success` / `message` / `data` / `traceId` |
| 14 | Exception Handling | `GlobalExceptionMiddleware` → safe messages, no internal leakage |
| 15 | Performance | `async/await` throughout · `IDisposable` patterns · no large allocations |
| 16 | File Upload | Multipart streaming via `MultipartReader` · `LimitedStream` · direct blob upload |
| 17 | Compression | GZip response compression enabled in `Program.cs` |
| 18 | Caching | Redis `ICacheService` + cache-aside in `ProductService` |
| 19 | Authentication | JWT Bearer + refresh token with rotation + revocation |
| 20 | CancellationToken | All async methods accept and propagate `CancellationToken` end-to-end |
| 21 | Deployment | Multi-stage `Dockerfile` · `docker-compose.yml` · stateless · health check endpoint |
| 22 | Unit Tests | NUnit + Moq: service logic, validators, middleware, CancellationToken enforcement test |
| 23 | Self-Validation | All rules verified — see checklist below |

---

## 🔒 Self-Validation Checklist

- ✅ **EF Core** used **only** for writes (`ProductRepository`, `OrderRepository`, `RefreshTokenRepository`)
- ✅ **Dapper** used **only** for reads (`ProductQueryService`, `OrderQueryService`)
- ✅ **CancellationToken** present on every async method — controller → service → repository
- ✅ **Naming conventions** — camelCase variables/fields, PascalCase classes/methods, `_field`, `IInterface`
- ✅ **XML documentation** on all controllers and public API methods
- ✅ **Transaction handling** — `BeginTransactionAsync` / single `SaveChanges` / rollback on failure
- ✅ **Logging levels** — `LogInformation` for business ops, `LogError` for exceptions, debug via config only
- ✅ **Debug logs** controlled purely by `Serilog:MinimumLevel` in `appsettings.json`
- ✅ **HTTP status codes** strictly mapped in `GlobalExceptionMiddleware` and controllers
- ✅ **No sensitive data exposure** — no passwords/tokens in logs, internal errors never returned to client

---

## 🚀 Getting Started

### Local Development (Docker)

```bash
docker-compose up -d
```

API available at: `http://localhost:8080`
Health check: `http://localhost:8080/health`
Swagger UI: `http://localhost:8080/swagger` (Development only)

### Database Migrations

```bash
cd src/EnterpriseApp.API
dotnet ef migrations add InitialCreate --project ../EnterpriseApp.Infrastructure
dotnet ef database update
```

### Running Tests

```bash
dotnet test --configuration Release --logger "console;verbosity=normal"
```

---

## 🔑 Security Notes

- **JWT secrets** are fetched from Azure Key Vault via `ISecretProvider` — never from `appsettings.json`
- **HMAC shared secrets** are fetched per API key from Key Vault: `HmacSecret:{apiKey}`
- **Refresh tokens** are stored as SHA-256 hashes — raw tokens never touch the database
- **Rate limiting** uses Redis — works across all pods behind a load balancer
- **File uploads** are streamed directly to blob — no buffering in app memory

---

## 📦 Key NuGet Packages

| Package | Purpose |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | EF Core (writes) |
| `Dapper` | Raw SQL (reads) |
| `StackExchange.Redis` | Distributed cache + rate limiting |
| `Serilog.AspNetCore` | Structured JSON logging |
| `FluentValidation` | Input validation |
| `Azure.Security.KeyVault.Secrets` | Secret management |
| `Azure.Storage.Blobs` | File storage |
| `Asp.Versioning.Mvc` | API versioning |
| `Swashbuckle.AspNetCore` | Swagger / OpenAPI |
| `NUnit` + `Moq` | Unit testing |
