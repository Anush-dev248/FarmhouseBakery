using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FarmhouseBakery.Services
{
    // Saves uploaded files to Azure Blob Storage (connection string "AzureStorage").
    //   "uploads" - menu item images, anonymous read (the blob URL is stored in the database)
    //   "resumes" - candidates' resumes, PRIVATE (only downloaded by admins through the site)
    //
    // "AzureStorage" can be either:
    //   - just the blob address, e.g. https://farmhousebakerystorage.blob.core.windows.net
    //     -> the Web App signs in with its own Azure identity (managed identity), no key stored
    //   - a full connection string with an AccountKey (used for local development)
    public class BlobStorageService
    {
        private const string UploadsContainerName = "uploads";
        private const string ResumesContainerName = "resumes";

        private readonly Lazy<BlobServiceClient> _serviceClient;

        // Containers are only checked/created once per app start.
        private bool _containersReady;
        private readonly SemaphoreSlim _containersLock = new(1, 1);

        public BlobStorageService(IConfiguration configuration)
        {
            _serviceClient = new Lazy<BlobServiceClient>(() =>
            {
                var connectionString = configuration.GetConnectionString("AzureStorage");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "Connection string 'AzureStorage' is not configured. " +
                        "Set it with dotnet user-secrets locally, or in the Web App's environment variables on Azure.");
                }

                if (connectionString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return new BlobServiceClient(new Uri(connectionString), new DefaultAzureCredential());
                }

                return new BlobServiceClient(connectionString);
            });
        }

        private BlobContainerClient Uploads =>
            _serviceClient.Value.GetBlobContainerClient(UploadsContainerName);

        private BlobContainerClient Resumes =>
            _serviceClient.Value.GetBlobContainerClient(ResumesContainerName);

        private async Task EnsureContainersAsync()
        {
            if (_containersReady)
            {
                return;
            }

            await _containersLock.WaitAsync();

            try
            {
                if (!_containersReady)
                {
                    // "uploads" is created in the Azure portal with anonymous blob read access.
                    // It is only created here (as Blob access) if it is missing.
                    await Uploads.CreateIfNotExistsAsync(PublicAccessType.Blob);

                    // "resumes" must never be public.
                    await Resumes.CreateIfNotExistsAsync(PublicAccessType.None);

                    _containersReady = true;
                }
            }
            finally
            {
                _containersLock.Release();
            }
        }

        // ---------- Menu images (public) ----------

        // Uploads an image and returns its public blob URL.
        public async Task<string> UploadImageAsync(IFormFile file)
        {
            await EnsureContainersAsync();

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var blob = Uploads.GetBlobClient(Guid.NewGuid().ToString() + extension);

            var contentType = extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            using var stream = file.OpenReadStream();

            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = "public, max-age=604800" // browsers may cache images for 7 days
                }
            });

            return blob.Uri.ToString();
        }

        // Deletes an image by its URL. Ignores URLs that aren't in our "uploads" container
        // (e.g. the site's own images such as /images/LogoBLKbg.png).
        public async Task DeleteImageAsync(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            var containerUrl = Uploads.Uri.ToString().TrimEnd('/') + "/";

            if (!imageUrl.StartsWith(containerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var blobName = Uri.UnescapeDataString(imageUrl.Substring(containerUrl.Length));

            await Uploads.GetBlobClient(blobName).DeleteIfExistsAsync();
        }

        // ---------- Resumes (private) ----------

        // Uploads a resume and returns its blob name (stored in the database).
        public async Task<string> UploadResumeAsync(IFormFile file)
        {
            await EnsureContainersAsync();

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var blobName = Guid.NewGuid().ToString() + extension;

            using var stream = file.OpenReadStream();

            await Resumes.GetBlobClient(blobName).UploadAsync(stream);

            return blobName;
        }

        // Opens a resume for download, or returns null if it doesn't exist.
        public async Task<Stream?> OpenResumeAsync(string blobName)
        {
            var blob = Resumes.GetBlobClient(Path.GetFileName(blobName));

            if (!await blob.ExistsAsync())
            {
                return null;
            }

            var download = await blob.DownloadStreamingAsync();

            return download.Value.Content;
        }

        public async Task DeleteResumeAsync(string blobName)
        {
            await Resumes.GetBlobClient(Path.GetFileName(blobName)).DeleteIfExistsAsync();
        }
    }
}
