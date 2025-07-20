using SQLite;
using System.Collections.Generic;
using System.IO;

namespace Excel_To_SQLite_WPF.Logic
{
    public class DatabaseManager
    {
        public string DbPath { get; }
        public List<string> GeneratedFilePaths { get; } = new List<string>();

        public DatabaseManager(string dbPath)
        {
            DbPath = dbPath;
        }

        public SQLiteConnection CreateConnection(string fileName)
        {
            var dbFilePath = Path.Combine(DbPath, $"{fileName}.db");
            GeneratedFilePaths.Add(dbFilePath);

            var options = new SQLiteConnectionString(dbFilePath,
               SQLiteOpenFlags.Create |
               SQLiteOpenFlags.FullMutex |
               SQLiteOpenFlags.ReadWrite,
               true,
               key: "your_password");

            return new SQLiteConnection(options);
        }

        public void ExecuteQuery(SQLiteConnection conn, string dbName, string createTableQuery, List<string> insertQueries)
        {
            try
            {
                conn.Execute($"CREATE TABLE {dbName} ({createTableQuery})");
            }
            catch
            {
                conn.Execute($"DROP TABLE {dbName}");
                conn.Execute($"CREATE TABLE {dbName} ({createTableQuery})");
            }

            conn.Execute($"DELETE FROM {dbName}");

            foreach (var query in insertQueries)
            {
                conn.Execute($"INSERT INTO {dbName} VALUES {query}");
            }
        }
    }
}