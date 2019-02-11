using System.Configuration;
using System.Data.Entity;
using System.Data.SQLite;
using System.IO;
using SQLite.CodeFirst;
using TwainDirect.Support;

namespace TwainDirect.Scanner.Storage
{
    public class CloudContext : DbContext
    {
        public CloudContext() : base(GetConnectionString("twainCloudDb"), true)
        { }

        private static SQLiteConnection GetConnectionString(string connectionName)
        {
            CloudManager.CloudInfo cloudinfo;

            // get the folder we have write access to (use current executable folder as default)
            var writeFolder = Config.Get("writeFolder", ".");

            // We want to be able to support multiple clouds...
            cloudinfo = CloudManager.GetCurrentCloudInfo();
            if ((cloudinfo != null) && !string.IsNullOrEmpty(cloudinfo.szFolderName))
            {
                writeFolder = Path.Combine(writeFolder, "clouds");
                writeFolder = Path.Combine(writeFolder, cloudinfo.szFolderName);
                if (!Directory.Exists(writeFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(writeFolder);
                    }
                    catch
                    {
                    }
                }
            }

            // retrieve connection string and update it to use write folder
            var settings = ConfigurationManager.ConnectionStrings;
            var connection = settings[connectionName];
            var updatedConnection = connection.ConnectionString.Replace("data source=.", $"data source={writeFolder}");
            
            return new SQLiteConnection(updatedConnection);
        }

        public DbSet<CloudScanner> Scanners { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var sqliteConnectionInitializer = new SqliteCreateDatabaseIfNotExists<CloudContext>(modelBuilder);
            Database.SetInitializer(sqliteConnectionInitializer);
        }
    }
}
