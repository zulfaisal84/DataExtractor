using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using DocumentExtractor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentExtractor.Web.Controllers
{
    /// <summary>
    /// Controller for managing document operations in the web interface.
    /// This provides a user-friendly interface to test document processing functionality.
    /// 
    /// VB.NET to C# Notes:
    /// - Controllers inherit from ControllerBase (for APIs) or Controller (for MVC views)
    /// - Actions return IActionResult or ActionResult<T>
    /// - [HttpGet], [HttpPost] attributes specify HTTP methods
    /// - ViewBag and ViewData work similarly to VB.NET MVC
    /// </summary>
    public class DocumentController : Controller
    {
        private readonly DocumentExtractionContext _context;
        private readonly ILogger<DocumentController> _logger;
        /// <summary>
        /// Constructor with dependency injection.
        /// </summary>
        public DocumentController(
            DocumentExtractionContext context, 
            ILogger<DocumentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display list of all processed documents.
        /// This is the main dashboard for viewing extraction results.
        /// </summary>
        /// <returns>View with list of documents</returns>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get all documents with their fields included
                var documents = await _context.Documents
                    .Include(d => d.Fields)  // Include related fields
                    .OrderByDescending(d => d.ProcessedDate)
                    .ToListAsync();

                // Add statistics to ViewBag for the dashboard
                ViewBag.TotalDocuments = documents.Count;
                ViewBag.AverageConfidence = documents.Any() ? documents.Average(d => d.OverallConfidence) : 0;
                ViewBag.DocumentsByType = documents.GroupBy(d => d.DocumentType)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
                ViewBag.DocumentsBySupplier = documents.Where(d => !string.IsNullOrEmpty(d.Supplier))
                    .GroupBy(d => d.Supplier)
                    .ToDictionary(g => g.Key, g => g.Count());

                return View(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                ViewBag.ErrorMessage = "Error loading documents: " + ex.Message;
                return View(new List<ExtractedDocument>());
            }
        }

        /// <summary>
        /// Display detailed view of a specific document and its extracted fields.
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <returns>Document details view</returns>
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                var document = await _context.Documents
                    .Include(d => d.Fields)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                {
                    return NotFound();
                }

                // Group fields by type for better display
                ViewBag.FieldsByType = document.Fields
                    .GroupBy(f => f.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.ToList());

                return View(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                ViewBag.ErrorMessage = "Error loading document: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// Display the document upload form.
        /// </summary>
        /// <returns>Upload form view</returns>
        [HttpGet]
        public IActionResult Upload()
        {
            // Populate dropdown options for document types
            ViewBag.DocumentTypes = Enum.GetValues<DocumentType>()
                .Where(dt => dt != DocumentType.Unknown)
                .Select(dt => new { Value = dt.ToString(), Text = GetDocumentTypeDisplayName(dt) })
                .ToList();

            return View();
        }

        /// <summary>
        /// Handle document upload and processing.
        /// </summary>
        /// <param name="file">Uploaded file</param>
        /// <param name="documentType">Document type selection</param>
        /// <param name="supplier">Optional supplier name</param>
        /// <returns>Processing result view</returns>
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string documentType, string? supplier)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.ErrorMessage = "Please select a file to upload.";
                return View();
            }

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsPath);

                // Save uploaded file
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsPath, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Simulate document processing
                var document = await SimulateDocumentProcessingAsync(filePath, file.FileName, documentType, supplier);

                // Save to database
                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                ViewBag.SuccessMessage = $"Document processed successfully! ID: {document.Id}";
                ViewBag.DocumentId = document.Id;
                ViewBag.ProcessingTime = document.ProcessingTimeMs;
                ViewBag.Confidence = (document.OverallConfidence * 100).ToString("F1");
                ViewBag.ExtractedFields = document.Fields.Count;
                
                return View("ProcessingResult", document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing uploaded file");
                ViewBag.ErrorMessage = "Error processing file: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// Simulate the document processing workflow.
        /// In a real application, this would call your OCR and extraction services.
        /// </summary>
        /// <param name="filePath">Path to uploaded file</param>
        /// <param name="originalFileName">Original filename</param>
        /// <param name="documentType">Document type</param>
        /// <param name="supplier">Supplier name</param>
        /// <returns>Processed document</returns>
        private async Task<ExtractedDocument> SimulateDocumentProcessingAsync(
            string filePath, 
            string originalFileName, 
            string documentType, 
            string? supplier)
        {
            // Simulate processing delay
            await Task.Delay(1000);

            var document = new ExtractedDocument(filePath)
            {
                FileName = originalFileName,
                DocumentType = Enum.Parse<DocumentType>(documentType),
                Supplier = supplier ?? "Unknown",
                Status = ProcessingStatus.Completed,
                ProcessingTimeMs = Random.Shared.Next(1000, 5000),
                FileSizeBytes = new FileInfo(filePath).Length
            };

            // Add simulated extracted fields based on document type
            AddSimulatedFields(document);

            // Calculate overall confidence
            document.CalculateOverallConfidence();

            return document;
        }

        /// <summary>
        /// Add simulated extracted fields for demonstration purposes.
        /// In a real application, this would be done by your extraction services.
        /// </summary>
        /// <param name="document">Document to add fields to</param>
        private static void AddSimulatedFields(ExtractedDocument document)
        {
            var fields = document.DocumentType switch
            {
                DocumentType.UtilityBill => new[]
                {
                    new ExtractedField("AccountNumber", GenerateRandomAccountNumber(), 0.95, "LocalPattern"),
                    new ExtractedField("CustomerName", "John Smith", 0.92, "LocalPattern"),
                    new ExtractedField("ServiceAddress", "123 Main St, Anytown, NY 10001", 0.89, "LocalPattern"),
                    new ExtractedField("BillDate", DateTime.Now.AddDays(-15).ToString("yyyy-MM-dd"), 0.94, "LocalPattern"),
                    new ExtractedField("DueDate", DateTime.Now.AddDays(15).ToString("yyyy-MM-dd"), 0.91, "LocalPattern"),
                    new ExtractedField("TotalAmountDue", $"${Random.Shared.Next(50, 500)}.{Random.Shared.Next(10, 99)}", 0.88, "LocalPattern"),
                    new ExtractedField("MeterNumber", GenerateRandomAccountNumber(8), 0.86, "LocalPattern")
                },

                DocumentType.TelecomBill => new[]
                {
                    new ExtractedField("AccountNumber", GenerateRandomAccountNumber(12), 0.97, "LocalPattern"),
                    new ExtractedField("CustomerName", "Jane Doe", 0.93, "LocalPattern"),
                    new ExtractedField("PhoneNumber", "(555) 123-4567", 0.96, "LocalPattern"),
                    new ExtractedField("BillDate", DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd"), 0.95, "LocalPattern"),
                    new ExtractedField("DueDate", DateTime.Now.AddDays(20).ToString("yyyy-MM-dd"), 0.92, "LocalPattern"),
                    new ExtractedField("MonthlyCharges", $"${Random.Shared.Next(40, 200)}.{Random.Shared.Next(10, 99)}", 0.90, "LocalPattern"),
                    new ExtractedField("DataUsage", $"{Random.Shared.Next(5, 50)} GB", 0.87, "LocalPattern")
                },

                DocumentType.Invoice => new[]
                {
                    new ExtractedField("InvoiceNumber", $"INV-{Random.Shared.Next(1000, 9999)}", 0.94, "LocalPattern"),
                    new ExtractedField("InvoiceDate", DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd"), 0.93, "LocalPattern"),
                    new ExtractedField("DueDate", DateTime.Now.AddDays(30).ToString("yyyy-MM-dd"), 0.91, "LocalPattern"),
                    new ExtractedField("VendorName", document.Supplier, 0.89, "LocalPattern"),
                    new ExtractedField("Subtotal", $"${Random.Shared.Next(100, 1500)}.{Random.Shared.Next(10, 99)}", 0.92, "LocalPattern"),
                    new ExtractedField("TaxAmount", $"${Random.Shared.Next(10, 150)}.{Random.Shared.Next(10, 99)}", 0.88, "LocalPattern"),
                    new ExtractedField("TotalAmount", $"${Random.Shared.Next(150, 2000)}.{Random.Shared.Next(10, 99)}", 0.90, "LocalPattern")
                },

                _ => new[]
                {
                    new ExtractedField("DocumentDate", DateTime.Now.ToString("yyyy-MM-dd"), 0.85, "LocalPattern"),
                    new ExtractedField("Amount", $"${Random.Shared.Next(50, 1000)}.{Random.Shared.Next(10, 99)}", 0.82, "LocalPattern")
                }
            };

            foreach (var field in fields)
            {
                document.Fields.Add(field);
            }
        }

        /// <summary>
        /// Generate a random account number for demonstration.
        /// </summary>
        /// <param name="length">Length of account number</param>
        /// <returns>Random account number string</returns>
        private static string GenerateRandomAccountNumber(int length = 10)
        {
            var random = Random.Shared;
            var digits = new char[length];

            for (int i = 0; i < length; i++)
            {
                digits[i] = (char)('0' + random.Next(0, 10));
            }

            return new string(digits);
        }

        /// <summary>
        /// Get user-friendly display name for document types.
        /// </summary>
        /// <param name="documentType">Document type enum</param>
        /// <returns>Display name</returns>
        private static string GetDocumentTypeDisplayName(DocumentType documentType)
        {
            return documentType switch
            {
                DocumentType.UtilityBill => "Utility Bill",
                DocumentType.TelecomBill => "Telecom Bill",
                DocumentType.Invoice => "Invoice",
                DocumentType.Receipt => "Receipt",
                DocumentType.MedicalBill => "Medical Bill",
                DocumentType.BankStatement => "Bank Statement",
                DocumentType.InsuranceDocument => "Insurance Document",
                DocumentType.TaxDocument => "Tax Document",
                DocumentType.LegalDocument => "Legal Document",
                DocumentType.Contract => "Contract",
                _ => documentType.ToString()
            };
        }

        /// <summary>
        /// Delete a document and its associated fields.
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <returns>Redirect to index</returns>
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                var document = await _context.Documents
                    .Include(d => d.Fields)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                {
                    return NotFound();
                }

                // Delete associated file if it exists
                if (System.IO.File.Exists(document.FilePath))
                {
                    System.IO.File.Delete(document.FilePath);
                }

                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();

                ViewBag.SuccessMessage = "Document deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                ViewBag.ErrorMessage = "Error deleting document: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}