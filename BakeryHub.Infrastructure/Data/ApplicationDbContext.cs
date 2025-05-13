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
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureApplicationUser(builder);
        ConfigureApplicationRole(builder);
        ConfigureCustomerTenantMembership(builder);
        ConfigureTenant(builder);
        ConfigureCategory(builder);
        ConfigureProduct(builder);
        ConfigureOrder(builder);
        ConfigureOrderItem(builder);

        builder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
    }

    private static void ConfigureApplicationUser(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable(name: "AspNetUsers");
            entity.HasIndex(u => u.TenantId).IsUnique();
            entity.HasOne(u => u.AdministeredTenant)
                  .WithOne()
                  .HasForeignKey<ApplicationUser>(u => u.TenantId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.Property(u => u.Name).HasMaxLength(150);

            entity.HasMany(u => u.Orders)
                  .WithOne(o => o.User)
                  .HasForeignKey(o => o.ApplicationUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureApplicationRole(ModelBuilder builder)
    {
        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable(name: "AspNetRoles");
        });
    }

    private static void ConfigureCustomerTenantMembership(ModelBuilder builder)
    {
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
    }

    private static void ConfigureTenant(ModelBuilder builder)
    {
        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Subdomain).IsUnique();
            entity.Property(t => t.Subdomain).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);

            entity.HasMany(t => t.Orders)
                  .WithOne(o => o.Tenant)
                  .HasForeignKey(o => o.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCategory(ModelBuilder builder)
    {
        builder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
            entity.HasOne(c => c.Tenant)
                  .WithMany(t => t.Categories)
                  .HasForeignKey(c => c.TenantId);
            
            entity.Property(c => c.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(c => c.IsDeleted);
        });
    }

    private static void ConfigureProduct(ModelBuilder builder)
    {
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
            entity.Property(p => p.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(p => p.IsDeleted);
        });
    }

    private static void ConfigureOrder(ModelBuilder builder)
    {
        builder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.OrderDate).IsRequired();
            entity.Property(o => o.DeliveryDate).IsRequired();
            entity.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(o => o.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(50);
            entity.HasMany(o => o.OrderItems)
                  .WithOne(oi => oi.Order)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOrderItem(ModelBuilder builder)
    {
        builder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.ProductName).IsRequired().HasMaxLength(250);
            entity.Property(oi => oi.Quantity).IsRequired();
            entity.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
            entity.HasOne(oi => oi.Product)
                  .WithMany()
                  .HasForeignKey(oi => oi.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
