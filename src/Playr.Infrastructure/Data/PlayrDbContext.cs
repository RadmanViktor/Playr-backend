using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Playr.Domain.Chat;
using Playr.Domain.Comments;
using Playr.Domain.Friendships;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Invitations;
using Playr.Domain.Posts;
using Playr.Domain.Profiles;
using Playr.Domain.Steam;
using System.Text.Json;

namespace Playr.Infrastructure.Data;

public sealed class PlayrDbContext(DbContextOptions<PlayrDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<SteamAccount> SteamAccounts => Set<SteamAccount>();
    public DbSet<SteamOwnedGame> SteamOwnedGames => Set<SteamOwnedGame>();
    public DbSet<SteamAchievement> SteamAchievements => Set<SteamAchievement>();

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
            profile.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(ProfileStatus.Online)
                .IsRequired();
            profile.Property(p => p.LookingForPlayStyle)
                .HasConversion<string>()
                .HasMaxLength(32);
            profile.HasOne(p => p.LookingForGame)
                .WithMany()
                .HasForeignKey(p => p.LookingForGameId)
                .OnDelete(DeleteBehavior.SetNull);
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
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                post.Property(p => p.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<PostMedia>(media =>
        {
            media.HasKey(m => m.Id);
            media.Property(m => m.Url).HasMaxLength(500).IsRequired();
            media.Property(m => m.MediaType)
                .HasConversion<string>()
                .HasMaxLength(16);
            media.HasOne(m => m.Post)
                .WithMany(p => p.Media)
                .HasForeignKey(m => m.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            media.HasIndex(m => new { m.PostId, m.SortOrder });
        });


        builder.Entity<PostLike>(like =>
        {
            like.HasKey(l => new { l.PostId, l.UserId });
            like.HasOne(l => l.Post)
                .WithMany()
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                like.Property(l => l.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<PostComment>(comment =>
        {
            comment.HasKey(c => c.Id);
            comment.Property(c => c.TextContent).HasMaxLength(500).IsRequired();
            comment.HasOne(c => c.Post)
                .WithMany()
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            comment.HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
            comment.HasIndex(c => new { c.PostId, c.CreatedAt });
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                comment.Property(c => c.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                comment.Property(c => c.UpdatedAt)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);
            }
        });

        builder.Entity<CommentReaction>(reaction =>
        {
            reaction.HasKey(r => new { r.CommentId, r.UserId });
            reaction.Property(r => r.Type)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            reaction.HasOne(r => r.Comment)
                .WithMany()
                .HasForeignKey(r => r.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
            reaction.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            reaction.HasIndex(r => r.CommentId);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                reaction.Property(r => r.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<Invitation>(invitation =>
        {
            invitation.HasKey(i => i.Id);
            invitation.Property(i => i.Message).HasMaxLength(500).IsRequired();
            invitation.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(InvitationStatus.Pending)
                .IsRequired();
            invitation.HasOne(i => i.Sender)
                .WithMany()
                .HasForeignKey(i => i.SenderUserId)
                .OnDelete(DeleteBehavior.Cascade);
            invitation.HasOne(i => i.Recipient)
                .WithMany()
                .HasForeignKey(i => i.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            invitation.HasIndex(i => new { i.SenderUserId, i.RecipientUserId, i.Status });
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                invitation.Property(i => i.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                invitation.Property(i => i.RespondedAt)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);
            }
        });

        builder.Entity<Friendship>(friendship =>
        {
            friendship.HasKey(f => f.Id);
            friendship.HasOne(f => f.UserA)
                .WithMany()
                .HasForeignKey(f => f.UserAId)
                .OnDelete(DeleteBehavior.Cascade);
            friendship.HasOne(f => f.UserB)
                .WithMany()
                .HasForeignKey(f => f.UserBId)
                .OnDelete(DeleteBehavior.Restrict);
            friendship.HasIndex(f => new { f.UserAId, f.UserBId }).IsUnique();
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                friendship.Property(f => f.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<Conversation>(conversation =>
        {
            conversation.HasKey(c => c.Id);
            conversation.HasOne(c => c.DirectUserA)
                .WithMany()
                .HasForeignKey(c => c.DirectUserAId)
                .OnDelete(DeleteBehavior.Cascade);
            conversation.HasOne(c => c.DirectUserB)
                .WithMany()
                .HasForeignKey(c => c.DirectUserBId)
                .OnDelete(DeleteBehavior.Restrict);
            conversation.HasIndex(c => new { c.DirectUserAId, c.DirectUserBId }).IsUnique();
            conversation.HasIndex(c => c.UpdatedAt);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                conversation.Property(c => c.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                conversation.Property(c => c.UpdatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<ConversationParticipant>(participant =>
        {
            participant.HasKey(p => new { p.ConversationId, p.UserId });
            participant.HasOne(p => p.Conversation)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            participant.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                participant.Property(p => p.JoinedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<ChatMessage>(message =>
        {
            message.HasKey(m => m.Id);
            message.Property(m => m.Body).HasMaxLength(1000).IsRequired();
            message.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            message.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Cascade);
            message.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                message.Property(m => m.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                message.Property(m => m.ReadAt)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);
            }
        });

        builder.Entity<SteamAccount>(steam =>
        {
            steam.HasKey(s => s.UserId);
            steam.Property(s => s.SteamId).HasMaxLength(32).IsRequired();
            steam.HasIndex(s => s.SteamId).IsUnique();
            steam.Property(s => s.DisplayName).HasMaxLength(128);
            steam.Property(s => s.AvatarUrl).HasMaxLength(500);
            steam.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                steam.Property(s => s.LinkedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                steam.Property(s => s.LastSyncedAt)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);
            }
        });

        builder.Entity<SteamOwnedGame>(game =>
        {
            game.HasKey(g => g.Id);
            game.Property(g => g.Name).HasMaxLength(256).IsRequired();
            game.Property(g => g.IconUrl).HasMaxLength(500);
            game.HasIndex(g => new { g.UserId, g.AppId }).IsUnique();
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                game.Property(g => g.LastSyncedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<SteamAchievement>(achievement =>
        {
            achievement.HasKey(a => a.Id);
            achievement.Property(a => a.ApiName).HasMaxLength(128).IsRequired();
            achievement.Property(a => a.DisplayName).HasMaxLength(256);
            achievement.Property(a => a.IconUrl).HasMaxLength(500);
            achievement.Property(a => a.IconGrayUrl).HasMaxLength(500);
            achievement.HasIndex(a => new { a.UserId, a.AppId, a.ApiName }).IsUnique();
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                achievement.Property(a => a.UnlockedAt)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);
                achievement.Property(a => a.LastSyncedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
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
