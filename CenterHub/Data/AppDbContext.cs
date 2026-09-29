using CenterHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TicketCategory>()
            .HasKey(tc => new { tc.TicketId, tc.ServiceCategoryId });

        builder.Entity<TicketCategory>()
            .HasOne(tc => tc.Ticket)
            .WithMany(t => t.Categories)
            .HasForeignKey(tc => tc.TicketId);

        builder.Entity<TicketCategory>()
            .HasOne(tc => tc.ServiceCategory)
            .WithMany()
            .HasForeignKey(tc => tc.ServiceCategoryId);

        builder.Entity<Ticket>()
            .Property(t => t.ServiceMode)
            .HasConversion<string>();

        builder.Entity<Ticket>()
            .Property(t => t.ResolutionStatus)
            .HasConversion<string>();

        builder.Entity<Ticket>()
            .Property(t => t.ShiftPeriod)
            .HasConversion<string>();

        builder.Entity<ServiceCategory>().HasData(
            new ServiceCategory { Id = 1, Name = "硬體" },
            new ServiceCategory { Id = 2, Name = "軟體" },
            new ServiceCategory { Id = 3, Name = "網路" },
            new ServiceCategory { Id = 4, Name = "郵件" },
            new ServiceCategory { Id = 5, Name = "重灌" },
            new ServiceCategory { Id = 6, Name = "中毒" },
            new ServiceCategory { Id = 7, Name = "無線" },
            new ServiceCategory { Id = 8, Name = "諮詢" },
            new ServiceCategory { Id = 9, Name = "印表機" },
            new ServiceCategory { Id = 10, Name = "其他" }
        );
    }
}
