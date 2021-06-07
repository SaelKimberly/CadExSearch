using System;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace CadExSearch.Commons
{
    public class CacheContext : DbContext
    {
        public CacheContext()
        {
        }

        public CacheContext(DbContextOptions<CacheContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Record> Records { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#pragma warning disable CS1030 // Директива #warning
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https: //go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlite("Data Source=.\\CadEx.Cache.db3");
#pragma warning restore CS1030 // Директива #warning
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Record>(entity => { entity.Property(e => e.BaseId).HasDefaultValueSql("'subject'"); });

            OnModelCreatingPartial(modelBuilder);
        }

        private void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            
        }
    }
}