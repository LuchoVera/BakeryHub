using BakeryHub.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BakeryHub.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }


    public DbSet<CustomerTenantMembership> CustomerTenantMemberships { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {

        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable(name: "AspNetUsers");
            entity.HasIndex(u => u.TenantId).IsUnique();
            entity.HasOne(u => u.AdministeredTenant)
                    .WithOne() 
                    .HasForeignKey<ApplicationUser>(u => u.TenantId) 
                    .OnDelete(DeleteBehavior.SetNull); 

            entity.Property(u => u.Name).HasMaxLength(150);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable(name: "AspNetRoles"); 

        });

        builder.Entity<CustomerTenantMembership>(entity =>
        {

            entity.HasKey(ctm => new { ctm.ApplicationUserId, ctm.TenantId });

            entity.HasOne(ctm => ctm.User)
                .WithMany(u => u.TenantMemberships) 
                .HasForeignKey(ctm => ctm.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade); 

            entity.HasOne(ctm => ctm.Tenant)
                .WithMany(t => t.CustomerMemberships) 
                .HasForeignKey(ctm => ctm.TenantId)
                .OnDelete(DeleteBehavior.Cascade); 
        });

        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Subdomain).IsUnique();
            entity.Property(t => t.Subdomain).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
        });


        builder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);

            entity.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();


            entity.HasOne(c => c.Tenant)
                    .WithMany(t => t.Categories)
                    .HasForeignKey(c => c.TenantId);
        });


        builder.Entity<Product>(entity =>
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

            entity.HasOne(p => p.Tenant)
                    .WithMany(t => t.Products)
                    .HasForeignKey(p => p.TenantId);

            entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId);
        });

    }
}
