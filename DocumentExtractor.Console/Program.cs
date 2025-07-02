using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentExtractor.Console
{
    /// <summary>
    /// Console application for testing and demonstrating the Document Intelligence system.
    /// This serves as both a testing tool and an example of how to use the core libraries.
    /// 
    /// VB.NET to C# Learning Notes:
    /// - C# console apps use static Main method instead of VB.NET's Sub Main()
    /// - async/await pattern enables non-blocking operations
    /// - using statements ensure proper resource disposal
    /// - Console.WriteLine is equivalent to VB.NET's Console.WriteLine
    /// </summary>
    class Program
    {
        /// <summary>
        /// Main entry point for the console application.
        /// Made async to demonstrate async/await patterns throughout the application.
        /// 
        /// C# Note: static async Task Main is the modern way to handle async operations in console apps.
        /// In older C# versions, you'd need to call .GetAwaiter().GetResult() on async operations.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        static async Task Main(string[] args)
        {
            // Display application header
            DisplayHeader();
            
            try
            {
                // Initialize the database
                await InitializeDatabaseAsync();
                
                // Run the main demonstration
                await RunDemonstrationAsync();
                
                // Display completion message
                System.Console.WriteLine("\n✅ Demonstration completed successfully!");
                System.Console.WriteLine("Press any key to exit...");
                System.Console.ReadKey();
            }
            catch (Exception ex)
            {
                // Handle any unhandled exceptions
                System.Console.WriteLine($"\n❌ Error: {ex.Message}");
                System.Console.WriteLine($"Details: {ex}");
                System.Console.WriteLine("\nPress any key to exit...");
                System.Console.ReadKey();
            }
        }

        /// <summary>
        /// Display the application header with version and description.
        /// </summary>
        private static void DisplayHeader()
        {
            System.Console.Clear();
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            System.Console.WriteLine("║                    Document Intelligence                       ║");
            System.Console.WriteLine("║                  Desktop Application v1.0                     ║");
            System.Console.WriteLine("║                                                                ║");
            System.Console.WriteLine("║  Professional document data extraction with local learning    ║");
            System.Console.WriteLine("║  Built with .NET 9, Entity Framework Core, and SQLite        ║");
            System.Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            System.Console.ResetColor();
            System.Console.WriteLine();
        }

        /// <summary>
        /// Initialize the SQLite database and verify it's working correctly.
        /// This demonstrates how to set up the database for the first time.
        /// </summary>
        private static async Task InitializeDatabaseAsync()
        {
            System.Console.WriteLine("🔧 Initializing database...");
            
            // Create database context
            using var context = new DocumentExtractionContext();
            
            try
            {
                // Create database if it doesn't exist
                context.EnsureCreated();
                
                // Get database information
                var dbInfo = context.GetDatabaseInfo();
                
                System.Console.WriteLine($"   📁 Database location: {dbInfo.DatabasePath}");
                System.Console.WriteLine($"   📊 Database size: {dbInfo.DatabaseSizeFormatted}");
                System.Console.WriteLine($"   ✅ Database initialized successfully");
                
                // Test database connectivity
                await TestDatabaseConnectivityAsync(context);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"   ❌ Database initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test database connectivity by performing basic CRUD operations.
        /// This verifies that Entity Framework is working correctly with SQLite.
        /// </summary>
        /// <param name="context">Database context to test</param>
        private static async Task TestDatabaseConnectivityAsync(DocumentExtractionContext context)
        {
            System.Console.WriteLine("   🔍 Testing database connectivity...");
            
            try
            {
                // Test basic query
                int documentCount = await context.Documents.CountAsync();
                int patternCount = await context.Patterns.CountAsync();
                
                System.Console.WriteLine($"   📄 Documents in database: {documentCount}");
                System.Console.WriteLine($"   🧠 Learned patterns: {patternCount}");
                
                System.Console.WriteLine("   ✅ Database connectivity test passed");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"   ❌ Database connectivity test failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Run the main demonstration of our Document Intelligence system.
        /// This showcases all major features and components.
        /// </summary>
        private static async Task RunDemonstrationAsync()
        {
            System.Console.WriteLine("\n🚀 Starting Document Intelligence demonstration...\n");
            
            // Demonstrate core data models
            await DemonstrateDataModelsAsync();
            
            // Demonstrate pattern learning
            await DemonstratePatternLearningAsync();
            
            // Demonstrate document processing simulation
            await DemonstrateDocumentProcessingAsync();
            
            // Display system statistics
            await DisplaySystemStatisticsAsync();
        }

        /// <summary>
        /// Demonstrate creating and working with core data models.
        /// Shows how to create documents, fields, and patterns programmatically.
        /// </summary>
        private static async Task DemonstrateDataModelsAsync()
        {
            System.Console.WriteLine("📊 Demonstrating Core Data Models");
            System.Console.WriteLine("═══════════════════════════════════");
            
            // Create a sample document
            var document = new ExtractedDocument("/sample/utility_bill_jan2024.pdf")
            {
                DocumentType = DocumentType.UtilityBill,
                Supplier = "ConEd",
                Status = ProcessingStatus.Completed,
                OverallConfidence = 0.92
            };
            
            // Add some extracted fields
            document.Fields.Add(new ExtractedField("AccountNumber", "1234567890", 0.95, "LocalPattern"));
            document.Fields.Add(new ExtractedField("TotalAmount", "$245.67", 0.88, "LocalPattern"));
            document.Fields.Add(new ExtractedField("DueDate", "2024-02-15", 0.91, "LocalPattern"));
            
            // Calculate overall confidence
            document.CalculateOverallConfidence();
            
            System.Console.WriteLine($"   📄 Created document: {document.FileName}");
            System.Console.WriteLine($"   🏢 Supplier: {document.Supplier}");
            System.Console.WriteLine($"   📊 Overall confidence: {document.OverallConfidence:P1}");
            System.Console.WriteLine($"   🔢 Fields extracted: {document.Fields.Count}");
            
            // Display extracted fields
            foreach (var field in document.Fields)
            {
                string confidenceColor = field.Confidence >= 0.9 ? "🟢" : field.Confidence >= 0.7 ? "🟡" : "🔴";
                System.Console.WriteLine($"      {confidenceColor} {field.FieldName}: {field.Value} ({field.Confidence:P1})");
            }
            
            // Demonstrate field validation
            bool needsReview = document.NeedsManualReview(0.85);
            System.Console.WriteLine($"   ⚠️  Needs manual review: {(needsReview ? "Yes" : "No")}");
            
            // Save to database
            using var context = new DocumentExtractionContext();
            context.Documents.Add(document);
            await context.SaveChangesAsync();
            
            System.Console.WriteLine($"   💾 Document saved to database with ID: {document.Id}");
            System.Console.WriteLine();
        }

        /// <summary>
        /// Demonstrate the pattern learning system.
        /// Shows how patterns are created, tested, and improved over time.
        /// </summary>
        private static async Task DemonstratePatternLearningAsync()
        {
            System.Console.WriteLine("🧠 Demonstrating Pattern Learning System");
            System.Console.WriteLine("═══════════════════════════════════════");
            
            // Create sample learned patterns
            var patterns = new[]
            {
                new LearnedPattern("ConEd", "AccountNumber", @"Account\s*(?:Number|#)?:?\s*(\d{10})", 
                    "Extracts 10-digit account number from ConEd utility bills")
                {
                    SuccessRate = 0.94,
                    UsageCount = 47,
                    SuccessCount = 44,
                    ExpectedFieldType = FieldType.AccountNumber,
                    Priority = 1
                },
                
                new LearnedPattern("ConEd", "TotalAmount", @"Total\s*(?:Amount\s*)?Due:?\s*\$?(\d+\.\d{2})",
                    "Extracts total amount due from ConEd bills")
                {
                    SuccessRate = 0.89,
                    UsageCount = 52,
                    SuccessCount = 46,
                    ExpectedFieldType = FieldType.Currency,
                    Priority = 1
                },
                
                new LearnedPattern("Verizon", "AccountNumber", @"Account:?\s*(\d{12})",
                    "Extracts 12-digit account number from Verizon bills")
                {
                    SuccessRate = 0.97,
                    UsageCount = 31,
                    SuccessCount = 30,
                    ExpectedFieldType = FieldType.AccountNumber,
                    Priority = 1
                }
            };
            
            // Compile patterns and save to database
            using var context = new DocumentExtractionContext();
            
            foreach (var pattern in patterns)
            {
                try
                {
                    // Compile regex to validate pattern
                    pattern.CompileRegex();
                    
                    // Add to database
                    context.Patterns.Add(pattern);
                    
                    System.Console.WriteLine($"   🔍 Pattern: {pattern.Supplier}.{pattern.FieldName}");
                    System.Console.WriteLine($"      📈 Success rate: {pattern.SuccessRate:P1} ({pattern.SuccessCount}/{pattern.UsageCount})");
                    System.Console.WriteLine($"      🎯 Priority: {pattern.Priority}");
                    System.Console.WriteLine($"      📝 Regex: {pattern.RegexPattern}");
                    
                    // Test pattern with sample text
                    await TestPatternAsync(pattern);
                    
                    System.Console.WriteLine();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"   ❌ Failed to create pattern {pattern.Supplier}.{pattern.FieldName}: {ex.Message}");
                }
            }
            
            await context.SaveChangesAsync();
            System.Console.WriteLine($"   💾 {patterns.Length} patterns saved to database");
            System.Console.WriteLine();
        }

        /// <summary>
        /// Test a learned pattern with sample text.
        /// Demonstrates how patterns extract data from document text.
        /// </summary>
        /// <param name="pattern">Pattern to test</param>
        private static async Task TestPatternAsync(LearnedPattern pattern)
        {
            // Sample texts for different suppliers
            var sampleTexts = new Dictionary<string, string>
            {
                ["ConEd"] = "CONSOLIDATED EDISON COMPANY\nAccount Number: 1234567890\nService Address: 123 Main St\nTotal Amount Due: $245.67\nDue Date: February 15, 2024",
                ["Verizon"] = "Verizon Wireless\nAccount: 123456789012\nBill Date: Jan 15, 2024\nTotal: $89.99\nDue: Feb 15, 2024"
            };
            
            if (sampleTexts.TryGetValue(pattern.Supplier, out string? sampleText))
            {
                var result = pattern.TryExtract(sampleText);
                
                if (result.Success)
                {
                    System.Console.WriteLine($"      ✅ Test result: '{result.Value}' (confidence: {result.Confidence:P1})");
                }
                else
                {
                    System.Console.WriteLine($"      ❌ Pattern test failed - no match found");
                }
            }
            
            await Task.CompletedTask; // Placeholder for actual async operations
        }

        /// <summary>
        /// Demonstrate document processing workflow.
        /// Shows how documents flow through the entire processing pipeline.
        /// </summary>
        private static async Task DemonstrateDocumentProcessingAsync()
        {
            System.Console.WriteLine("⚙️  Demonstrating Document Processing Workflow");
            System.Console.WriteLine("══════════════════════════════════════════════");
            
            // Simulate processing multiple documents
            var documents = new[]
            {
                new { Path = "/samples/coned_bill_jan.pdf", Type = DocumentType.UtilityBill, Supplier = "ConEd" },
                new { Path = "/samples/verizon_bill_feb.pdf", Type = DocumentType.TelecomBill, Supplier = "Verizon" },
                new { Path = "/samples/invoice_acme.pdf", Type = DocumentType.Invoice, Supplier = "ACME Corp" }
            };
            
            using var context = new DocumentExtractionContext();
            
            foreach (var docInfo in documents)
            {
                System.Console.WriteLine($"   📄 Processing: {Path.GetFileName(docInfo.Path)}");
                
                // Simulate processing steps
                await SimulateProcessingStepAsync("OCR Text Extraction", 1500);
                await SimulateProcessingStepAsync("Document Type Detection", 500);
                await SimulateProcessingStepAsync("Supplier Identification", 300);
                await SimulateProcessingStepAsync("Pattern Matching", 800);
                await SimulateProcessingStepAsync("Field Validation", 400);
                
                // Create processed document
                var document = new ExtractedDocument(docInfo.Path)
                {
                    DocumentType = docInfo.Type,
                    Supplier = docInfo.Supplier,
                    Status = ProcessingStatus.Completed,
                    ProcessingTimeMs = 3500,
                    OverallConfidence = 0.85 + (Random.Shared.NextDouble() * 0.15) // Random confidence 0.85-1.0
                };
                
                // Add some sample fields based on document type
                AddSampleFields(document);
                
                // Save to database
                context.Documents.Add(document);
                
                System.Console.WriteLine($"      ✅ Completed in {document.ProcessingTimeMs}ms (confidence: {document.OverallConfidence:P1})");
                System.Console.WriteLine();
            }
            
            await context.SaveChangesAsync();
            System.Console.WriteLine($"   💾 {documents.Length} documents processed and saved");
            System.Console.WriteLine();
        }

        /// <summary>
        /// Simulate a processing step with realistic timing.
        /// Shows progress feedback for long-running operations.
        /// </summary>
        /// <param name="stepName">Name of the processing step</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        private static async Task SimulateProcessingStepAsync(string stepName, int durationMs)
        {
            System.Console.Write($"      🔄 {stepName}... ");
            await Task.Delay(durationMs / 10); // Speed up for demo
            System.Console.WriteLine($"✅ ({durationMs}ms)");
        }

        /// <summary>
        /// Add sample extracted fields based on document type.
        /// Demonstrates different field types and confidence levels.
        /// </summary>
        /// <param name="document">Document to add fields to</param>
        private static void AddSampleFields(ExtractedDocument document)
        {
            var fields = document.DocumentType switch
            {
                DocumentType.UtilityBill => new[]
                {
                    new ExtractedField("AccountNumber", GenerateRandomAccountNumber(), 0.94, "LocalPattern"),
                    new ExtractedField("TotalAmount", $"${Random.Shared.Next(50, 500)}.{Random.Shared.Next(10, 99)}", 0.91, "LocalPattern"),
                    new ExtractedField("DueDate", DateTime.Now.AddDays(30).ToString("yyyy-MM-dd"), 0.89, "LocalPattern"),
                    new ExtractedField("ServiceAddress", "123 Main St, Anytown, NY 10001", 0.87, "LocalPattern")
                },
                
                DocumentType.TelecomBill => new[]
                {
                    new ExtractedField("AccountNumber", GenerateRandomAccountNumber(12), 0.96, "LocalPattern"),
                    new ExtractedField("PhoneNumber", "(555) 123-4567", 0.93, "LocalPattern"),
                    new ExtractedField("MonthlyCharges", $"${Random.Shared.Next(40, 200)}.{Random.Shared.Next(10, 99)}", 0.88, "LocalPattern"),
                    new ExtractedField("DataUsage", $"{Random.Shared.Next(5, 50)} GB", 0.85, "LocalPattern")
                },
                
                _ => new[]
                {
                    new ExtractedField("InvoiceNumber", $"INV-{Random.Shared.Next(1000, 9999)}", 0.92, "LocalPattern"),
                    new ExtractedField("TotalAmount", $"${Random.Shared.Next(100, 2000)}.{Random.Shared.Next(10, 99)}", 0.90, "LocalPattern"),
                    new ExtractedField("InvoiceDate", DateTime.Now.ToString("yyyy-MM-dd"), 0.88, "LocalPattern")
                }
            };
            
            foreach (var field in fields)
            {
                document.Fields.Add(field);
            }
            
            document.CalculateOverallConfidence();
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
        /// Display comprehensive system statistics.
        /// Shows current state of the database and processing metrics.
        /// </summary>
        private static async Task DisplaySystemStatisticsAsync()
        {
            System.Console.WriteLine("📈 System Statistics & Summary");
            System.Console.WriteLine("═════════════════════════════");
            
            using var context = new DocumentExtractionContext();
            
            try
            {
                // Document statistics
                var totalDocuments = await context.Documents.CountAsync();
                var documentsByType = await context.Documents
                    .GroupBy(d => d.DocumentType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();
                
                var documentsBySupplier = await context.Documents
                    .Where(d => !string.IsNullOrEmpty(d.Supplier))
                    .GroupBy(d => d.Supplier)
                    .Select(g => new { Supplier = g.Key, Count = g.Count() })
                    .ToListAsync();
                
                // Pattern statistics
                var totalPatterns = await context.Patterns.CountAsync();
                var activePatterns = await context.Patterns.CountAsync(p => p.IsActive);
                var avgSuccessRate = await context.Patterns
                    .Where(p => p.UsageCount > 0)
                    .AverageAsync(p => p.SuccessRate);
                
                // Display statistics
                System.Console.WriteLine($"   📄 Total Documents: {totalDocuments}");
                System.Console.WriteLine($"   🧠 Total Patterns: {totalPatterns} ({activePatterns} active)");
                System.Console.WriteLine($"   📊 Average Pattern Success Rate: {avgSuccessRate:P1}");
                System.Console.WriteLine();
                
                if (documentsByType.Any())
                {
                    System.Console.WriteLine("   📋 Documents by Type:");
                    foreach (var item in documentsByType)
                    {
                        System.Console.WriteLine($"      • {item.Type}: {item.Count}");
                    }
                    System.Console.WriteLine();
                }
                
                if (documentsBySupplier.Any())
                {
                    System.Console.WriteLine("   🏢 Documents by Supplier:");
                    foreach (var item in documentsBySupplier)
                    {
                        System.Console.WriteLine($"      • {item.Supplier}: {item.Count}");
                    }
                    System.Console.WriteLine();
                }
                
                // Database info
                var dbInfo = context.GetDatabaseInfo();
                System.Console.WriteLine("   💾 Database Information:");
                System.Console.WriteLine($"      📁 Location: {dbInfo.DatabasePath}");
                System.Console.WriteLine($"      📊 Size: {dbInfo.DatabaseSizeFormatted}");
                System.Console.WriteLine($"      📅 Last Modified: {dbInfo.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"   ❌ Error retrieving statistics: {ex.Message}");
            }
        }
    }
}
