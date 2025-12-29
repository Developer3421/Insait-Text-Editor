using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LiteDB;

namespace InsaitTextEditor.Services
{
    /// <summary>
    /// Manages storing and retrieving document text from a LiteDB database.
    /// </summary>
    public class DatabaseService
    {
        private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());
        public static DatabaseService Instance => _instance.Value;
        
        private readonly string _dbPath;

        public DatabaseService()
        {
            var baseDir = GetExecutableDirectory();
            var dbDir = Path.Combine(baseDir, "Database");
            
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
                Console.WriteLine($"[DatabaseService] ✓ Створено папку Database: {dbDir}");
            }
            else
            {
                Console.WriteLine($"[DatabaseService] ○ Папка Database вже існує: {dbDir}");
            }
            
            _dbPath = Path.Combine(dbDir, "documents.litedb");
        }

        /// <summary>
        /// Отримує директорію, де знаходиться виконуваний файл
        /// </summary>
        private static string GetExecutableDirectory()
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                return Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
            }
            return AppContext.BaseDirectory;
        }

        private LiteDatabase OpenDb()
        {
            LiteDatabase TryOpen(string? path)
            {
                var cs = new ConnectionString { Filename = path, Connection = ConnectionType.Shared, Upgrade = true };
                return new LiteDatabase(cs);
            }

            try
            {
                return TryOpen(_dbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseService] Failed to open DB at '{_dbPath}': {ex.GetType().Name}: {ex.Message}");
            }

            // Recreate file and retry
            try
            {
                try
                {
                    if (File.Exists(_dbPath))
                        File.Delete(_dbPath);
                }
                catch (Exception delEx)
                {
                    Console.WriteLine($"[DatabaseService] Failed to delete DB file '{_dbPath}': {delEx.GetType().Name}: {delEx.Message}");
                }

                return TryOpen(_dbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseService] Failed to recreate DB at '{_dbPath}': {ex.GetType().Name}: {ex.Message}");
            }

            // Sandbox / FS blocked: in-memory fallback
            Console.WriteLine("[DatabaseService] Falling back to in-memory DB. Data won't persist.");
            return TryOpen(null);
        }

        /// <summary>
        /// Get a collection from the database for generic use
        /// </summary>
        public ILiteCollection<T> GetCollection<T>(string collectionName)
        {
            var db = OpenDb();
            return db.GetCollection<T>(collectionName);
        }

        /// <summary>
        /// Saves the document text associated with a given ID.
        /// </summary>
        public Task SaveDocumentAsync(Guid id, string text)
        {
            return Task.Run(() =>
            {
                using var db = OpenDb();
                var col = db.GetCollection<DocumentText>("documents");
                
                var doc = new DocumentText
                {
                    Id = id,
                    Content = text,
                    LastModified = DateTime.UtcNow
                };

                col.Upsert(doc);
            });
        }

        /// <summary>
        /// Retrieves the document text for a given ID.
        /// </summary>
        public Task<string?> LoadDocumentAsync(Guid id)
        {
            return Task.Run(() =>
            {
                using var db = OpenDb();
                var col = db.GetCollection<DocumentText>("documents");
                var doc = col.FindById(id);
                return doc?.Content;
            });
        }
    }

    /// <summary>
    /// Represents a document stored in the database.
    /// </summary>
    public class DocumentText
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}
