using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DocumentExtractor.Web.Controllers
{
    /// <summary>
    /// Controller for viewing database contents and system statistics.
    /// This provides insight into the stored data and pattern learning system.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class DatabaseController : Controller
    {
        private readonly DocumentExtractionContext _context;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(DocumentExtractionContext context, ILogger<DatabaseController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display database overview with statistics.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get database statistics
                var totalDocuments = await _context.Documents.CountAsync();
                var totalFields = await _context.Fields.CountAsync();
                var totalPatterns = await _context.Patterns.CountAsync();
                var activePatterns = await _context.Patterns.CountAsync(p => p.IsActive);

                // Document statistics
                var documentsByType = await _context.Documents
                    .GroupBy(d => d.DocumentType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                var documentsBySupplier = await _context.Documents
                    .Where(d => !string.IsNullOrEmpty(d.Supplier))
                    .GroupBy(d => d.Supplier)
                    .Select(g => new { Supplier = g.Key, Count = g.Count() })
                    .ToListAsync();

                var documentsByStatus = await _context.Documents
                    .GroupBy(d => d.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                // Field statistics
                var fieldsByType = await _context.Fields
                    .GroupBy(f => f.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                var averageConfidence = await _context.Documents
                    .Where(d => d.OverallConfidence > 0)
                    .AverageAsync(d => d.OverallConfidence);

                // Pattern statistics
                var patternStats = await _context.Patterns
                    .Where(p => p.UsageCount > 0)
                    .Select(p => new { p.Supplier, p.FieldName, p.SuccessRate, p.UsageCount })
                    .ToListAsync();

                var avgPatternSuccessRate = patternStats.Any() ? patternStats.Average(p => p.SuccessRate) : 0;

                // Database file information
                var dbInfo = _context.GetDatabaseInfo();

                // Create view model
                var viewModel = new DatabaseStatsViewModel
                {
                    TotalDocuments = totalDocuments,
                    TotalFields = totalFields,
                    TotalPatterns = totalPatterns,
                    ActivePatterns = activePatterns,
                    AverageConfidence = averageConfidence,
                    AveragePatternSuccessRate = avgPatternSuccessRate,
                    DocumentsByType = documentsByType.ToDictionary(x => x.Type.ToString(), x => x.Count),
                    DocumentsBySupplier = documentsBySupplier.ToDictionary(x => x.Supplier, x => x.Count),
                    DocumentsByStatus = documentsByStatus.ToDictionary(x => x.Status.ToString(), x => x.Count),
                    FieldsByType = fieldsByType.ToDictionary(x => x.Type.ToString(), x => x.Count),
                    DatabaseInfo = dbInfo,
                    RecentDocuments = await _context.Documents
                        .OrderByDescending(d => d.ProcessedDate)
                        .Take(5)
                        .ToListAsync(),
                    TopPatterns = patternStats
                        .OrderByDescending(p => p.SuccessRate)
                        .Take(10)
                        .Cast<object>()
                        .ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving database statistics");
                ViewBag.ErrorMessage = "Error loading database statistics: " + ex.Message;
                return View(new DatabaseStatsViewModel());
            }
        }

        /// <summary>
        /// Display all learned patterns with their performance metrics.
        /// </summary>
        public async Task<IActionResult> Patterns()
        {
            try
            {
                var patterns = await _context.Patterns
                    .OrderByDescending(p => p.SuccessRate)
                    .ThenByDescending(p => p.UsageCount)
                    .ToListAsync();

                return View(patterns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving patterns");
                ViewBag.ErrorMessage = "Error loading patterns: " + ex.Message;
                return View(new List<LearnedPattern>());
            }
        }

        /// <summary>
        /// Display all extracted fields with filtering options.
        /// </summary>
        public async Task<IActionResult> Fields(string? fieldType, string? supplier, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = _context.Fields.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(fieldType))
                {
                    query = query.Where(f => f.Type.ToString() == fieldType);
                }

                if (!string.IsNullOrEmpty(supplier))
                {
                    query = query.Where(f => _context.Documents
                        .Any(d => d.Id == f.Id && d.Supplier == supplier));
                }

                // Get total count for pagination
                var totalFields = await query.CountAsync();

                // Apply pagination
                var fields = await query
                    .OrderByDescending(f => f.ExtractedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Get filter options
                ViewBag.FieldTypes = await _context.Fields
                    .Select(f => f.Type.ToString())
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                ViewBag.Suppliers = await _context.Documents
                    .Where(d => !string.IsNullOrEmpty(d.Supplier))
                    .Select(d => d.Supplier)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync();

                // Pagination info
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalFields = totalFields;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalFields / pageSize);
                ViewBag.SelectedFieldType = fieldType;
                ViewBag.SelectedSupplier = supplier;

                return View(fields);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fields");
                ViewBag.ErrorMessage = "Error loading fields: " + ex.Message;
                return View(new List<ExtractedField>());
            }
        }

        /// <summary>
        /// Clear all data from the database (for testing purposes).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ClearData()
        {
            try
            {
                // Remove all data
                _context.Fields.RemoveRange(_context.Fields);
                _context.Documents.RemoveRange(_context.Documents);
                _context.Patterns.RemoveRange(_context.Patterns);

                await _context.SaveChangesAsync();

                ViewBag.SuccessMessage = "All data cleared successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing database");
                ViewBag.ErrorMessage = "Error clearing data: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// View model for database statistics display.
    /// </summary>
    public class DatabaseStatsViewModel
    {
        public int TotalDocuments { get; set; }
        public int TotalFields { get; set; }
        public int TotalPatterns { get; set; }
        public int ActivePatterns { get; set; }
        public double AverageConfidence { get; set; }
        public double AveragePatternSuccessRate { get; set; }
        public Dictionary<string, int> DocumentsByType { get; set; } = new();
        public Dictionary<string, int> DocumentsBySupplier { get; set; } = new();
        public Dictionary<string, int> DocumentsByStatus { get; set; } = new();
        public Dictionary<string, int> FieldsByType { get; set; } = new();
        public DatabaseInfo DatabaseInfo { get; set; } = new();
        public List<ExtractedDocument> RecentDocuments { get; set; } = new();
        public List<object> TopPatterns { get; set; } = new();
    }
}