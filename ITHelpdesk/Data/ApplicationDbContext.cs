using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ITHelpdesk.Models;

namespace ITHelpdesk.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Ticket>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ScreenshotPath).HasMaxLength(260);
            entity.Property(x => x.Priority).IsRequired();

            entity.HasIndex(x => x.AssignedToUserId);
            entity.HasIndex(x => new { x.TicketStatusId, x.Priority, x.CreatedAtUtc });

            entity.HasOne(x => x.Status)
                .WithMany(x => x.Tickets)
                .HasForeignKey(x => x.TicketStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TicketComment>(entity =>
        {
            entity.Property(x => x.CommentText).HasMaxLength(1000).IsRequired();

            entity.HasOne(x => x.Ticket)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

            builder.Entity<TicketActivity>(entity =>
            {
                entity.Property(x => x.Description).HasMaxLength(500).IsRequired();

                entity.HasIndex(x => new { x.TicketId, x.CreatedAtUtc });
                entity.HasIndex(x => new { x.TicketId, x.ActivityType });

                entity.HasOne(x => x.Ticket)
                .WithMany(x => x.Activities)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            });

        builder.Entity<TicketStatus>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();

            entity.HasData(
                new TicketStatus { Id = TicketStatusValues.OpenId, Name = "Open" },
                new TicketStatus { Id = TicketStatusValues.InProgressId, Name = "In Progress" },
                new TicketStatus { Id = TicketStatusValues.ResolvedId, Name = "Resolved" }
            );
        });
    }
}
