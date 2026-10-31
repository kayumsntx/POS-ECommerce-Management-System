using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace POSWebApi.Models
{
    public class AppDbContext : IdentityDbContext<AppUser, AppRole, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Category>        Categories        { get; set; }
        //public DbSet<Customer>        Customers         { get; set; }
        public DbSet<DeliveryAddress> DeliveryAddresses { get; set; }
        public DbSet<Inventory>       Inventories       { get; set; }
        public DbSet<Order>           Orders            { get; set; }
        public DbSet<OrderItem>       OrderItems        { get; set; }
        public DbSet<Payment>         Payments          { get; set; }
        public DbSet<PaymentMethod>   PaymentMethods    { get; set; }
        public DbSet<Product>         Products          { get; set; }
        //public DbSet<Refund>          Refunds           { get; set; }
        public DbSet<Return>          Returns           { get; set; }
        public DbSet<ReturnItem>      ReturnItems       { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Product → Category: Restrict (protect products from accidental category delete)
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Inventory → Product: Cascade (delete inventory when product is deleted)
            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Order → AppUser: SetNull (preserve order history if user is deleted)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // OrderItem → Order: Cascade (delete items when order is deleted)
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderItem → Product: Restrict (keep product even if it has order items)
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment → Order: Cascade (delete payments when order is deleted)
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Payment → PaymentMethod: Restrict (preserve payment audit trail)
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.PaymentMethod)
                .WithMany(pm => pm.Payments)
                .HasForeignKey(p => p.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            // DeliveryAddress ↔ Order: one-to-one, Cascade (delete address when order is deleted)
            modelBuilder.Entity<DeliveryAddress>()
                .HasOne(da => da.Order)
                .WithOne(o => o.DeliveryAddress)
                .HasForeignKey<DeliveryAddress>(da => da.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Return → Order: Restrict (don't delete orders that have returns)
            modelBuilder.Entity<Return>()
                .HasOne(r => r.Order)
                .WithMany(o => o.Returns)
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            // ReturnItem → Return: Cascade (delete return items when return is deleted)
            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.Return)
                .WithMany(r => r.ReturnItems)
                .HasForeignKey(ri => ri.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            // ReturnItem → OrderItem: Restrict (keep order item records intact)
            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.OrderItem)
                .WithMany(oi => oi.ReturnItems)
                .HasForeignKey(ri => ri.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            //// Refund → Return: Cascade (delete refunds when return is deleted)
            //modelBuilder.Entity<Refund>()
            //    .HasOne(r => r.Return)
            //    .WithMany(ret => ret.Refunds)
            //    .HasForeignKey(r => r.ReturnId)
            //    .OnDelete(DeleteBehavior.Cascade);

            //// Refund → Payment: Restrict (preserve payment records)
            //modelBuilder.Entity<Refund>()
            //    .HasOne(r => r.Payment)
            //    .WithMany(p => p.Refunds)
            //    .HasForeignKey(r => r.PaymentId)
            //    .OnDelete(DeleteBehavior.Restrict);

            // ── Seed Roles ────────────────────────────────────────────────
            modelBuilder.Entity<AppRole>().HasData(
                new AppRole { Id = 1, Name = "Admin",       NormalizedName = "ADMIN",       ConcurrencyStamp = "1" },
                new AppRole { Id = 2, Name = "User",         NormalizedName = "USER",         ConcurrencyStamp = "2" },
                new AppRole { Id = 3, Name = "POSOperator",  NormalizedName = "POSOPERATOR",  ConcurrencyStamp = "3" },
                new AppRole { Id = 4, Name = "Cashier",      NormalizedName = "CASHIER",      ConcurrencyStamp = "4" },
                new AppRole { Id = 5, Name = "Salesman",     NormalizedName = "SALESMAN",     ConcurrencyStamp = "5" }
            );
        }
    }
}
