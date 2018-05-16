using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using SQLite.CodeFirst;

namespace TwainDirect.Scanner.Storage
{
    public class CloudContext : DbContext
    {
        public CloudContext() : base("twainCloudDb")
        { }

        public DbSet<CloudScanner> Scanners { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var sqliteConnectionInitializer = new SqliteCreateDatabaseIfNotExists<CloudContext>(modelBuilder);
            Database.SetInitializer(sqliteConnectionInitializer);
        }
    }
}
