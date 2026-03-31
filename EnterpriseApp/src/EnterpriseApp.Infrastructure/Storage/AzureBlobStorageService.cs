using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EnterpriseApp.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IBlobStorageService"/>.
/// Streams data directly to blob — no full memory buffering.
/// Connection strings are resolved via <see cref="ISecretProvider"/>, never from appsettings.
/// </summary>
public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private const string ContainerName = "enterprise-uploads";

    /// <summary>Initializes a new instance of <see cref="AzureBlobStorageService"/>.</summary>
    public AzureBlobStorageService(BlobServiceClient blobServiceClient, ILogger<AzureBlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> UploadStreamAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"{Guid.NewGuid():N}/{SanitizeFileName(fileName)}";
        var blobClient = containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        // Direct stream upload — no intermediate buffering in memory
        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

        _logger.LogInformation("File uploaded to blob storage. BlobName={BlobName} ContentType={ContentType}", blobName, contentType);

        return blobClient.Uri.ToString();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string blobUrl, CancellationToken cancellationToken)
    {
        var uri = new Uri(blobUrl);
        var blobName = uri.LocalPath.TrimStart('/');
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        if (deleted)
            _logger.LogInformation("Blob deleted: {BlobUrl}", blobUrl);
        else
            _logger.LogWarning("Blob not found for deletion: {BlobUrl}", blobUrl);
    }

    private static string SanitizeFileName(string fileName)
    {
        return Path.GetFileName(fileName)
            .Replace(" ", "_")
            .ToLowerInvariant();
    }
}
