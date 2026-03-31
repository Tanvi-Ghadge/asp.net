using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace EnterpriseApp.API.Controllers.V1;

/// <summary>
/// File Upload API — version 1.
/// Provides stream-based file upload directly to Azure Blob Storage.
/// Files are never fully buffered in application memory.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/files")]
[Authorize]
[Produces("application/json")]
public sealed class FilesController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<FilesController> _logger;

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "application/pdf", "text/csv"
    };

    /// <summary>Initializes a new instance of <see cref="FilesController"/>.</summary>
    public FilesController(IBlobStorageService blobStorageService, ILogger<FilesController> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a file using multipart streaming — no buffering in memory.
    /// Validates content type and file size before upload.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// 200 — file uploaded, blob URL returned.
    /// 400 — invalid file type or size exceeded.
    /// 401 — unauthenticated.
    /// 500 — upload failure.
    /// </returns>
    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)] // 50 MB + overhead
    [ProducesResponseType(typeof(ApiResponse<FileUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FileUploadResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadFile(CancellationToken cancellationToken)
    {
        if (!IsMultipartContentType(Request.ContentType))
            return BadRequest(ApiResponse<FileUploadResponse>.Fail("Request must be multipart/form-data.", HttpContext.TraceIdentifier));

        var boundary = GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType));
        var reader = new MultipartReader(boundary, Request.Body);

        var section = await reader.ReadNextSectionAsync(cancellationToken);

        while (section is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
            {
                section = await reader.ReadNextSectionAsync(cancellationToken);
                continue;
            }

            if (!HasFileContentDisposition(contentDisposition))
            {
                section = await reader.ReadNextSectionAsync(cancellationToken);
                continue;
            }

            var fileName = contentDisposition.FileName.Value ?? "unknown";
            var contentType = section.ContentType ?? "application/octet-stream";

            if (!AllowedContentTypes.Contains(contentType))
                return BadRequest(ApiResponse<FileUploadResponse>.Fail($"Content type '{contentType}' is not allowed.", HttpContext.TraceIdentifier));

            // Stream directly to blob — no intermediate MemoryStream
            using var limitedStream = new LimitedStream(section.Body, MaxFileSizeBytes);

            _logger.LogInformation("Uploading file {FileName} ContentType={ContentType}", fileName, contentType);

            var blobUrl = await _blobStorageService.UploadStreamAsync(limitedStream, fileName, contentType, cancellationToken);

            _logger.LogInformation("File uploaded successfully. BlobUrl={BlobUrl}", blobUrl);

            var response = new FileUploadResponse(fileName, blobUrl, limitedStream.BytesRead, contentType);
            return Ok(ApiResponse<FileUploadResponse>.Ok(response, "File uploaded.", HttpContext.TraceIdentifier));
        }

        return BadRequest(ApiResponse<FileUploadResponse>.Fail("No file found in request.", HttpContext.TraceIdentifier));
    }

    private static bool IsMultipartContentType(string? contentType) =>
        !string.IsNullOrEmpty(contentType) && contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase);

    private static string GetBoundary(MediaTypeHeaderValue contentType)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value
            ?? throw new InvalidDataException("Missing content-type boundary.");
        return boundary;
    }

    private static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition) =>
        contentDisposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase)
        && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
            || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
}

/// <summary>Stream wrapper that enforces a maximum read size and tracks bytes read.</summary>
internal sealed class LimitedStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _bytesRead;

    internal LimitedStream(Stream inner, long maxBytes)
    {
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public long BytesRead => _bytesRead;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _bytesRead; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_bytesRead >= _maxBytes)
            throw new InvalidOperationException($"File exceeds maximum allowed size of {_maxBytes} bytes.");

        var toRead = (int)Math.Min(count, _maxBytes - _bytesRead);
        var bytesRead = _inner.Read(buffer, offset, toRead);
        _bytesRead += bytesRead;
        return bytesRead;
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
