using BakeryHub.Domain.Entities;
using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Catalog.Domain.Models;
using BakeryHub.Modules.Orders.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BakeryHub.Modules.Orders.Infrastructure.Persistence;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.HasKey(o => o.Id);
        entity.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
        entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(50);

        entity.HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<ApplicationUser>(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne<Tenant>(o => o.Tenant)
                .WithMany()
                .HasForeignKey(o => o.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> entity)
    {
        entity.HasKey(oi => oi.Id);
        entity.Property(oi => oi.ProductName).IsRequired().HasMaxLength(250);
        entity.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
        entity.Ignore(oi => oi.Subtotal);

        entity.HasOne<Product>(oi => oi.Product)
              .WithMany()
              .HasForeignKey(oi => oi.ProductId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
