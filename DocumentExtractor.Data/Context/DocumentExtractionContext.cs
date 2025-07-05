using DocumentExtractor.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace DocumentExtractor.Data.Context
{
    /// <summary>
    /// Entity Framework Core database context for the Document Extraction application.
    /// This class manages all database operations and provides access to our data entities.
    /// 
    /// Key Features:
    /// - SQLite database for local storage
    /// - Pattern learning intelligence data
    /// - Document processing history
    /// - Automatic database creation and migrations
    /// 
    /// VB.NET to C# Learning Notes:
    /// - DbContext is the main class for Entity Framework database operations
    /// - DbSet<T> properties represent database tables
    /// - OnConfiguring method sets up database connection
    /// - OnModelCreating method configures entity relationships and constraints
    /// </summary>
    public class DocumentExtractionContext : DbContext
    {
        /// <summary>
        /// Table for storing processed documents and their extraction results.
        /// Each row represents one document that has been processed by our system.
        /// </summary>
        public DbSet<ExtractedDocument> Documents { get; set; } = null!;

        /// <summary>
        /// Table for storing individual extracted fields from documents.
        /// This is where the actual extracted data (account numbers, amounts, etc.) is stored.
        /// </summary>
        public DbSet<ExtractedField> Fields { get; set; } = null!;

        /// <summary>
        /// Table for storing learned patterns that improve over time.
        /// This is the core of our "local learning intelligence" feature.
        /// </summary>
        public DbSet<LearnedPattern> Patterns { get; set; } = null!;

        /// <summary>
        /// Table for storing user templates for automated data filling.
        /// Templates are Excel, Word, PDF files that users upload to teach the AI.
        /// </summary>
        public DbSet<Template> Templates { get; set; } = null!;

        /// <summary>
        /// Table for storing field mappings between extracted data and template locations.
        /// Defines where each piece of extracted data should be placed in templates.
        /// </summary>
        public DbSet<TemplateFieldMapping> TemplateFieldMappings { get; set; } = null!;

        /// <summary>
        /// Default constructor for dependency injection and migrations.
        /// </summary>
        public DocumentExtractionContext()
        {
        }

        /// <summary>
        /// Constructor that accepts DbContextOptions.
        /// This is used when the context is configured via dependency injection.
        /// 
        /// C# Constructor Note:
        /// - base(options) calls the parent class constructor
        /// - This is equivalent to VB.NET's MyBase.New(options)
        /// </summary>
        /// <param name="options">Database context options</param>
        public DocumentExtractionContext(DbContextOptions<DocumentExtractionContext> options) : base(options)
        {
        }

        /// <summary>
        /// Configure the database connection and options.
        /// This method is called by Entity Framework to set up the database connection.
        /// 
        /// We're using SQLite for local storage which provides:
        /// - No server required (file-based database)
        /// - Fast local queries
        /// - Perfect for desktop applications
        /// - Cross-platform compatibility
        /// </summary>
        /// <param name="optionsBuilder">Options builder for configuring the context</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only configure if not already configured (allows for dependency injection override)
            if (!optionsBuilder.IsConfigured)
            {
                // Database will be created in the user's local application data folder
                string databasePath = GetDatabasePath();
                
                optionsBuilder.UseSqlite($"Data Source={databasePath}");
                
                // Enable detailed logging in development (comment out for production)
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
                
                // Log SQL queries to console (helpful for debugging)
                optionsBuilder.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
            }
        }

        /// <summary>
        /// Configure entity relationships, constraints, and database schema.
        /// This method is called by Entity Framework to set up the database structure.
        /// 
        /// VB.NET to C# Notes:
        /// - Fluent API uses method chaining (similar to LINQ)
        /// - Lambda expressions (=>) are used instead of VB.NET's Function() syntax
        /// - Entity<T>() specifies which entity to configure
        /// </summary>
        /// <param name="modelBuilder">Model builder for configuring entities</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ExtractedDocument entity
            ConfigureExtractedDocument(modelBuilder);
            
            // Configure ExtractedField entity
            ConfigureExtractedField(modelBuilder);
            
            // Configure LearnedPattern entity
            ConfigureLearnedPattern(modelBuilder);
            
            // Configure Template entity
            ConfigureTemplate(modelBuilder);
            
            // Configure TemplateFieldMapping entity
            ConfigureTemplateFieldMapping(modelBuilder);

            // Set up database indexes for better query performance
            ConfigureIndexes(modelBuilder);
        }

        /// <summary>
        /// Configure the ExtractedDocument entity.
        /// Sets up table structure, constraints, and relationships.
        /// </summary>
        /// <param name="modelBuilder">Model builder instance</param>
        private static void ConfigureExtractedDocument(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExtractedDocument>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);
                
                // Configure string properties with maximum lengths
                entity.Property(e => e.Id)
                    .HasMaxLength(50)
                    .IsRequired();
                
                entity.Property(e => e.FilePath)
                    .HasMaxLength(1000)
                    .IsRequired();
                
                entity.Property(e => e.FileName)
                    .HasMaxLength(255)
                    .IsRequired();
                
                entity.Property(e => e.Supplier)
                    .HasMaxLength(100);
                
                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(2000);
                
                entity.Property(e => e.RawText)
                    .HasMaxLength(50000); // Large text field for OCR results
                
                // Configure enum properties
                entity.Property(e => e.DocumentType)
                    .HasConversion<string>(); // Store enums as strings for readability
                
                entity.Property(e => e.Status)
                    .HasConversion<string>();
                
                // Configure decimal properties
                entity.Property(e => e.OverallConfidence)
                    .HasColumnType("REAL"); // SQLite type for double
                
                // Configure date properties
                entity.Property(e => e.ProcessedDate)
                    .HasColumnType("TEXT") // SQLite stores dates as ISO 8601 strings
                    .HasConversion(
                        v => v.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        v => DateTime.Parse(v));
                
                // Set up one-to-many relationship with ExtractedField
                entity.HasMany(e => e.Fields)
                    .WithOne()
                    .HasForeignKey("DocumentId")
                    .OnDelete(DeleteBehavior.Cascade); // Delete fields when document is deleted
            });
        }

        /// <summary>
        /// Configure the ExtractedField entity.
        /// Sets up table structure and constraints for individual field data.
        /// </summary>
        /// <param name="modelBuilder">Model builder instance</param>
        private static void ConfigureExtractedField(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExtractedField>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);
                
                // Configure string properties
                entity.Property(e => e.Id)
                    .HasMaxLength(50)
                    .IsRequired();
                
                entity.Property(e => e.FieldName)
                    .HasMaxLength(100)
                    .IsRequired();
                
                entity.Property(e => e.Value)
                    .HasMaxLength(1000)
                    .IsRequired();
                
                entity.Property(e => e.Source)
                    .HasMaxLength(50)
                    .IsRequired();
                
                entity.Property(e => e.BoundingBox)
                    .HasMaxLength(100);
                
                entity.Property(e => e.OriginalContext)
                    .HasMaxLength(500);
                
                entity.Property(e => e.OriginalValue)
                    .HasMaxLength(1000);
                
                // Configure enum properties
                entity.Property(e => e.Type)
                    .HasConversion<string>();
                
                // Configure decimal properties
                entity.Property(e => e.Confidence)
                    .HasColumnType("REAL");
                
                // Configure date properties
                entity.Property(e => e.ExtractedDate)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        v => v.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        v => DateTime.Parse(v));
                
                // Add foreign key for relationship with ExtractedDocument
                entity.Property<string>("DocumentId")
                    .HasMaxLength(50);
            });
        }

        /// <summary>
        /// Configure the LearnedPattern entity.
        /// Sets up table structure for our pattern learning intelligence system.
        /// </summary>
        /// <param name="modelBuilder">Model builder instance</param>
        private static void ConfigureLearnedPattern(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LearnedPattern>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);
                
                // Configure string properties
                entity.Property(e => e.Id)
                    .HasMaxLength(50)
                    .IsRequired();
                
                entity.Property(e => e.Supplier)
                    .HasMaxLength(100)
                    .IsRequired();
                
                entity.Property(e => e.FieldName)
                    .HasMaxLength(100)
                    .IsRequired();
                
                entity.Property(e => e.RegexPattern)
                    .HasMaxLength(1000)
                    .IsRequired();
                
                entity.Property(e => e.Description)
                    .HasMaxLength(500);
                
                entity.Property(e => e.ExampleMatch)
                    .HasMaxLength(1000);
                
                // Configure enum properties
                entity.Property(e => e.ExpectedFieldType)
                    .HasConversion<string>();
                
                // Configure decimal properties
                entity.Property(e => e.SuccessRate)
                    .HasColumnType("REAL");
                
                entity.Property(e => e.MinimumConfidence)
                    .HasColumnType("REAL");
                
                entity.Property(e => e.MaximumConfidence)
                    .HasColumnType("REAL");
                
                // Configure date properties
                entity.Property(e => e.LastUsed)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        v => v.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        v => DateTime.Parse(v));
                
                entity.Property(e => e.CreatedDate)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        v => v.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        v => DateTime.Parse(v));
                
                // Ignore the compiled regex property (not stored in database)
                entity.Ignore(e => e.CompiledRegex);
            });
        }

        /// <summary>
        /// Configure the Template entity for database storage.
        /// </summary>
        /// <param name="modelBuilder">Model builder instance</param>
        private static void ConfigureTemplate(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Template>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);
                
                // Configure string properties with appropriate lengths
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);
                
                entity.Property(e => e.Description)
                    .HasMaxLength(500);
                
                entity.Property(e => e.Category)
                    .HasMaxLength(100);
                
                entity.Property(e => e.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);
                
                entity.Property(e => e.FileName)
                    .IsRequired()
                    .HasMaxLength(255);
                
                entity.Property(e => e.FileExtension)
                    .HasMaxLength(10);
                
                entity.Property(e => e.PreviewImagePath)
                    .HasMaxLength(500);
                
                // Configure date properties for SQLite
                entity.Property(e => e.CreatedDate)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        v => v.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        v => DateTime.Parse(v));
                
                entity.Property(e => e.LastUsedDate)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        v => v != null ? v.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") : null,
                        v => v != null ? DateTime.Parse(v) : null);
                
                // Configure the one-to-many relationship with TemplateFieldMapping
                entity.HasMany(e => e.FieldMappings)
                    .WithOne(f => f.Template)
                    .HasForeignKey(f => f.TemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        /// <summary>
        /// Configure the TemplateFieldMapping entity for database storage.
        /// </summary>
        /// <param name="modelBuilder">Model builder instance</param>
        private static void ConfigureTemplateFieldMapping(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TemplateFieldMapping>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);
                
                // Configure string properties
                entity.Property(e => e.TemplateId)
                    .IsRequired();
                
                entity.Property(e => e.FieldName)
                    .IsRequired()
                    .HasMaxLength(100);
                
                entity.Property(e => e.TargetLocation)
                    .IsRequired()
                    .HasMaxLength(100);
                
                entity.Property(e => e.LocationType)
                    .HasMaxLength(50);
                
                entity.Property(e => e.FormatInstructions)
                    .HasMaxLength(200);
                
                entity.Property(e => e.Description)
                    .HasMaxLength(200);
                
                // Configure date property for SQLite
                entity.Property(e => e.CreatedDate)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        v => v.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        v => DateTime.Parse(v));
                
                // Configure the many-to-one relationship with Template
                entity.HasOne(e => e.Template)
                    .WithMany(t => t.FieldMappings)
                    .HasForeignKey(e => e.TemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        /// <summary>
        /// Configure database indexes for better query performance.
        /// Indexes speed up queries but take additional storage space.
        /// </summary>
        /// <param name="modelBuilder">Model builder instance</param>
        private static void ConfigureIndexes(ModelBuilder modelBuilder)
        {
            // Index on DocumentType and Supplier for fast filtering
            modelBuilder.Entity<ExtractedDocument>()
                .HasIndex(e => e.DocumentType)
                .HasDatabaseName("IX_Documents_DocumentType");
            
            modelBuilder.Entity<ExtractedDocument>()
                .HasIndex(e => e.Supplier)
                .HasDatabaseName("IX_Documents_Supplier");
            
            modelBuilder.Entity<ExtractedDocument>()
                .HasIndex(e => e.ProcessedDate)
                .HasDatabaseName("IX_Documents_ProcessedDate");
            
            // Index on field names for fast field lookups
            modelBuilder.Entity<ExtractedField>()
                .HasIndex(e => e.FieldName)
                .HasDatabaseName("IX_Fields_FieldName");
            
            // Composite index for pattern lookups (supplier + field name)
            modelBuilder.Entity<LearnedPattern>()
                .HasIndex(e => new { e.Supplier, e.FieldName })
                .HasDatabaseName("IX_Patterns_Supplier_FieldName");
            
            // Index on pattern success rate for finding best patterns
            modelBuilder.Entity<LearnedPattern>()
                .HasIndex(e => e.SuccessRate)
                .HasDatabaseName("IX_Patterns_SuccessRate");

            // Template indexes for fast lookups
            modelBuilder.Entity<Template>()
                .HasIndex(e => e.Category)
                .HasDatabaseName("IX_Templates_Category");
            
            modelBuilder.Entity<Template>()
                .HasIndex(e => e.CreatedDate)
                .HasDatabaseName("IX_Templates_CreatedDate");
            
            modelBuilder.Entity<Template>()
                .HasIndex(e => e.LastUsedDate)
                .HasDatabaseName("IX_Templates_LastUsedDate");

            // Template field mapping indexes
            modelBuilder.Entity<TemplateFieldMapping>()
                .HasIndex(e => e.TemplateId)
                .HasDatabaseName("IX_TemplateFieldMappings_TemplateId");
            
            modelBuilder.Entity<TemplateFieldMapping>()
                .HasIndex(e => e.FieldName)
                .HasDatabaseName("IX_TemplateFieldMappings_FieldName");
        }

        /// <summary>
        /// Get the path where the SQLite database file should be stored.
        /// Uses the user's local application data folder for proper file permissions.
        /// 
        /// Database Location Strategy:
        /// - Windows: %LOCALAPPDATA%\DocumentExtractor\database.db
        /// - macOS: ~/Library/Application Support/DocumentExtractor/database.db
        /// - Linux: ~/.local/share/DocumentExtractor/database.db
        /// </summary>
        /// <returns>Full path to the database file</returns>
        private static string GetDatabasePath()
        {
            // Get the appropriate application data folder for the current platform
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            // Create application-specific folder
            string appFolder = Path.Combine(appDataPath, "DocumentExtractor");
            
            // Ensure the directory exists
            Directory.CreateDirectory(appFolder);
            
            // Return full path to database file
            return Path.Combine(appFolder, "document_extraction.db");
        }

        /// <summary>
        /// Ensure the database is created and up-to-date.
        /// Call this method during application startup.
        /// 
        /// This method will:
        /// 1. Create the database file if it doesn't exist
        /// 2. Run any pending migrations
        /// 3. Seed initial data if needed
        /// </summary>
        public void EnsureCreated()
        {
            try
            {
                // Create database if it doesn't exist
                Database.EnsureCreated();
                
                // Alternatively, use migrations in production:
                // Database.Migrate();
                
                Console.WriteLine($"Database initialized at: {GetDatabasePath()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get database connection information for debugging and monitoring.
        /// </summary>
        /// <returns>Database connection info</returns>
        public DatabaseInfo GetDatabaseInfo()
        {
            string dbPath = GetDatabasePath();
            var fileInfo = new FileInfo(dbPath);
            
            return new DatabaseInfo
            {
                DatabasePath = dbPath,
                DatabaseExists = fileInfo.Exists,
                DatabaseSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : null,
                ConnectionString = Database.GetConnectionString()
            };
        }
    }

    /// <summary>
    /// Information about the database for monitoring and debugging.
    /// </summary>
    public class DatabaseInfo
    {
        /// <summary>
        /// Full path to the database file.
        /// </summary>
        public string DatabasePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether the database file exists.
        /// </summary>
        public bool DatabaseExists { get; set; }

        /// <summary>
        /// Size of the database file in bytes.
        /// </summary>
        public long DatabaseSizeBytes { get; set; }

        /// <summary>
        /// When the database was last modified.
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Database connection string.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Get human-readable database size.
        /// </summary>
        public string DatabaseSizeFormatted
        {
            get
            {
                if (DatabaseSizeBytes < 1024)
                    return $"{DatabaseSizeBytes} bytes";
                if (DatabaseSizeBytes < 1024 * 1024)
                    return $"{DatabaseSizeBytes / 1024:F1} KB";
                return $"{DatabaseSizeBytes / (1024 * 1024):F1} MB";
            }
        }
    }
}