using DocumentExtractor.Core.Interfaces;
using DocumentExtractor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace DocumentExtractor.Services
{
    /// <summary>
    /// Service for handling document file uploads and initial processing
    /// </summary>
    public class DocumentUploadService
    {
        private readonly ILogger<DocumentUploadService> _logger;
        private readonly string _uploadDirectory;
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB limit
        private readonly string[] _allowedExtensions = { ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp", ".gif", ".pdf" };

        /// <summary>
        /// Initialize the document upload service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public DocumentUploadService(ILogger<DocumentUploadService> logger)
        {
            _logger = logger;
            
            // Create uploads directory in application data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "DocumentExtractor");
            _uploadDirectory = Path.Combine(appFolder, "uploads");
            
            // Ensure directory exists
            Directory.CreateDirectory(_uploadDirectory);
            
            _logger.LogInformation("Document upload service initialized. Upload directory: {UploadDirectory}", _uploadDirectory);
        }

        /// <summary>
        /// Process uploaded file and save it to the upload directory
        /// </summary>
        /// <param name="file">Uploaded file from web request</param>
        /// <returns>File information for further processing</returns>
        public async Task<UploadedFileInfo> ProcessUploadAsync(IFormFile file)
        {
            try
            {
                _logger.LogInformation("Processing file upload: {FileName}, Size: {FileSize} bytes", 
                    file.FileName, file.Length);

                // Validate file
                ValidateFile(file);

                // Generate unique filename to prevent conflicts
                string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                string filePath = Path.Combine(_uploadDirectory, uniqueFileName);

                // Save file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create file info object
                var uploadedFileInfo = new UploadedFileInfo
                {
                    OriginalFileName = file.FileName,
                    SavedFileName = uniqueFileName,
                    FilePath = filePath,
                    FileSizeBytes = file.Length,
                    ContentType = file.ContentType,
                    UploadedDate = DateTime.UtcNow
                };

                _logger.LogInformation("File uploaded successfully: {OriginalFileName} -> {SavedFileName}", 
                    file.FileName, uniqueFileName);

                return uploadedFileInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file upload: {FileName}", file.FileName);
                throw;
            }
        }

        /// <summary>
        /// Validate uploaded file meets requirements
        /// </summary>
        /// <param name="file">File to validate</param>
        private void ValidateFile(IFormFile file)
        {
            // Check if file is provided
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file was uploaded or file is empty.");
            }

            // Check file size
            if (file.Length > MaxFileSize)
            {
                throw new ArgumentException($"File size ({file.Length:N0} bytes) exceeds maximum allowed size ({MaxFileSize:N0} bytes).");
            }

            // Check file extension
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"File type '{extension}' is not supported. " +
                    $"Allowed types: {string.Join(", ", _allowedExtensions)}");
            }

            // Basic security check - ensure filename doesn't contain path traversal
            if (file.FileName.Contains("..") || Path.IsPathRooted(file.FileName))
            {
                throw new ArgumentException("Invalid filename detected.");
            }

            _logger.LogDebug("File validation passed for: {FileName}", file.FileName);
        }

        /// <summary>
        /// Get upload directory path
        /// </summary>
        /// <returns>Full path to upload directory</returns>
        public string GetUploadDirectory()
        {
            return _uploadDirectory;
        }

        /// <summary>
        /// Delete uploaded file from disk
        /// </summary>
        /// <param name="filePath">Path to file to delete</param>
        public async Task DeleteFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                    _logger.LogInformation("Deleted file: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Attempted to delete non-existent file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Get file size in human-readable format
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Formatted file size string</returns>
        public static string GetHumanReadableFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Get allowed file extensions for upload
        /// </summary>
        /// <returns>Array of allowed file extensions</returns>
        public string[] GetAllowedExtensions()
        {
            return _allowedExtensions.ToArray();
        }

        /// <summary>
        /// Get maximum allowed file size
        /// </summary>
        /// <returns>Maximum file size in bytes</returns>
        public long GetMaxFileSize()
        {
            return MaxFileSize;
        }
    }

    /// <summary>
    /// Information about an uploaded file
    /// </summary>
    public class UploadedFileInfo
    {
        public string OriginalFileName { get; set; } = string.Empty;
        public string SavedFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
    }
}