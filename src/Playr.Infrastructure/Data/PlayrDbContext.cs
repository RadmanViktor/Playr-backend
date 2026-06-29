using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;

namespace Playr.Infrastructure.Data;

public sealed class PlayrDbContext(DbContextOptions<PlayrDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasOne(user => user.Profile)
            .WithOne(profile => profile.User)
            .HasForeignKey<UserProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserProfile>(profile =>
        {
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.Username).HasMaxLength(32).IsRequired();
            profile.HasIndex(p => p.Username).IsUnique();
            profile.Property(p => p.DisplayName).HasMaxLength(64).IsRequired();
            profile.Property(p => p.Bio).HasMaxLength(500);
            profile.Property(p => p.AvatarUrl).HasMaxLength(500);
            profile.Property(p => p.Region).HasMaxLength(64);
            profile.Property(p => p.Languages).HasColumnType("jsonb");
            profile.Property(p => p.Platforms).HasColumnType("jsonb");
            profile.Property(p => p.ExternalLinks).HasColumnType("jsonb");
            profile.Property(p => p.CurrentlyPlayingGames).HasColumnType("jsonb");
        });
    }
}
