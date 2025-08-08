using BakeryHub.Modules.Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace BakeryHub.Modules.Catalog.Infrastructure.Persistence;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> entity)
    {
        entity.HasKey(c => c.Id);
        entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
        entity.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
        entity.Property(c => c.IsDeleted).HasDefaultValue(false);
        entity.HasIndex(c => c.IsDeleted);

        entity.HasOne(c => c.Tenant)
              .WithMany()
              .HasForeignKey(c => c.TenantId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.HasKey(p => p.Id);
        entity.Property(p => p.Name).IsRequired().HasMaxLength(250);
        entity.Property(p => p.Description).HasMaxLength(2000);
        entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
        entity.Property(p => p.IsAvailable).HasDefaultValue(true);
        entity.Property(p => p.LeadTime);
        entity.Property(p => p.Images)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>()
                );
        entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId);
        entity.Property(p => p.IsDeleted).HasDefaultValue(false);
        entity.HasIndex(p => p.IsDeleted);

        entity.HasOne(p => p.Tenant)
              .WithMany()
              .HasForeignKey(p => p.TenantId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> entity)
    {
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Name).IsRequired().HasMaxLength(50);
        entity.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}

public class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> entity)
    {
        entity.HasKey(pt => new { pt.ProductId, pt.TagId });

        entity.HasOne(pt => pt.Product)
                .WithMany(p => p.ProductTags)
                .HasForeignKey(pt => pt.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(pt => pt.Tag)
                .WithMany(t => t.ProductTags)
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
    }
}
