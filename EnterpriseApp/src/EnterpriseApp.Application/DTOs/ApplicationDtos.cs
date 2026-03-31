namespace EnterpriseApp.Application.DTOs;

// ─── Product DTOs ────────────────────────────────────────────────────────────

/// <summary>Request payload for creating a new product.</summary>
public sealed record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity
);

/// <summary>Request payload for updating an existing product.</summary>
public sealed record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price
);

/// <summary>Read model returned for product queries.</summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAtUtc
);

// ─── Order DTOs ──────────────────────────────────────────────────────────────

/// <summary>Request payload for placing a new order.</summary>
public sealed record PlaceOrderRequest(
    Guid CustomerId,
    IReadOnlyList<OrderItemRequest> Items
);

/// <summary>A single item within an order request.</summary>
public sealed record OrderItemRequest(
    Guid ProductId,
    int Quantity
);

/// <summary>Read model returned after order is placed.</summary>
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemDto> Items
);

/// <summary>Read model for a single order line item.</summary>
public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

// ─── Auth DTOs ───────────────────────────────────────────────────────────────

/// <summary>Login request payload.</summary>
public sealed record LoginRequest(
    string Username,
    string Password
);

/// <summary>Token response returned on successful authentication.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

/// <summary>Refresh token request payload.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

// ─── File Upload ─────────────────────────────────────────────────────────────

/// <summary>Response returned after a successful file upload.</summary>
public sealed record FileUploadResponse(
    string FileName,
    string BlobUrl,
    long SizeBytes,
    string ContentType
);

// ─── Standard API Response ───────────────────────────────────────────────────

/// <summary>Uniform envelope wrapping all API responses.</summary>
public sealed record ApiResponse<T>
{
    /// <summary>Indicates whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable message about the result.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The response payload.</summary>
    public T? Data { get; init; }

    /// <summary>Correlation/trace identifier for log correlation.</summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>Creates a successful response with data.</summary>
    public static ApiResponse<T> Ok(T data, string message = "Success", string traceId = "") =>
        new() { Success = true, Message = message, Data = data, TraceId = traceId };

    /// <summary>Creates a failure response with no data.</summary>
    public static ApiResponse<T> Fail(string message, string traceId = "") =>
        new() { Success = false, Message = message, Data = default, TraceId = traceId };
}
