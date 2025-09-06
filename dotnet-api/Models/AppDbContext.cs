using Microsoft.EntityFrameworkCore;
using System;

namespace DotnetApi.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) {}
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentPage> DocumentPages { get; set; }
        public DbSet<Chunk> Chunks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("documents");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.FileUrl).HasColumnName("file_url");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<DocumentPage>(entity =>
            {
                entity.ToTable("document_pages");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DocumentId).HasColumnName("document_id");
                entity.Property(e => e.PageNo).HasColumnName("page_no");
                entity.Property(e => e.Text).HasColumnName("text");
            });

            modelBuilder.Entity<Chunk>(entity =>
            {
                entity.ToTable("chunks");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DocumentId).HasColumnName("document_id");
                entity.Property(e => e.PageNo).HasColumnName("page_no");
                entity.Property(e => e.Text).HasColumnName("text");
            });
        }
    }

    public class Document {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string? FileUrl { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DocumentPage {
        public long Id { get; set; }
        public long DocumentId { get; set; }
        public int PageNo { get; set; }
        public string? Text { get; set; }
    }

    public class Chunk {
        public long Id { get; set; }
        public long DocumentId { get; set; }
        public int PageNo { get; set; }
        public string? Text { get; set; }
        // embedding stored in DB as pgvector - handled with raw SQL
    }
}
