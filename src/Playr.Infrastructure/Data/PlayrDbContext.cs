using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using System.Text.Json;

namespace Playr.Infrastructure.Data;

public sealed class PlayrDbContext(DbContextOptions<PlayrDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Post> Posts => Set<Post>();

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
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                profile.Property(p => p.Languages).HasColumnType("jsonb");
                profile.Property(p => p.Platforms).HasColumnType("jsonb");
                profile.Property(p => p.ExternalLinks).HasColumnType("jsonb");
                profile.Property(p => p.CurrentlyPlayingGames).HasColumnType("jsonb");
            }
            else
            {
                profile.Property(p => p.Languages).HasJsonConversion();
                profile.Property(p => p.Platforms).HasJsonConversion();
                profile.Property(p => p.ExternalLinks).HasJsonConversion();
                profile.Property(p => p.CurrentlyPlayingGames).HasJsonConversion();
            }
        });

        builder.Entity<Game>(game =>
        {
            game.HasKey(g => g.Id);
            game.Property(g => g.Name).HasMaxLength(128).IsRequired();
            game.Property(g => g.CoverImageUrl).HasMaxLength(500);
            game.Property(g => g.Genre).HasMaxLength(64);
            game.HasData(
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000001"), Name = "Apex Legends" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000002"), Name = "Call of Duty" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000003"), Name = "Counter-Strike 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000004"), Name = "Destiny 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000005"), Name = "Elden Ring" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000006"), Name = "Genshin Impact" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000007"), Name = "Hollow Knight" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000008"), Name = "Valorant" }
            );
        });

        builder.Entity<Post>(post =>
        {
            post.HasKey(p => p.Id);
            post.Property(p => p.TextContent).HasMaxLength(1000).IsRequired();
            post.Property(p => p.Mood)
                .HasConversion<string>()
                .HasMaxLength(16);
            post.HasOne(p => p.Author)
                .WithMany()
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
            post.HasOne(p => p.Game)
                .WithMany()
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Restrict);
            post.HasIndex(p => p.CreatedAt);
        });
    }
}

file static class JsonPropertyBuilderExtensions
{
    public static void HasJsonConversion<T>(this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> propertyBuilder)
        where T : class, new()
    {
        propertyBuilder.HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<T>(value, (JsonSerializerOptions?)null) ?? new T());
        propertyBuilder.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new T()));
    }
}
