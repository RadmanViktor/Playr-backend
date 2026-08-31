using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Playr.Domain.Chat;
using Playr.Domain.Comments;
using Playr.Domain.Follows;
using Playr.Domain.Friendships;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Invitations;
using Playr.Domain.Notifications;
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
    public DbSet<UserGameLibraryEntry> UserGameLibraryEntries => Set<UserGameLibraryEntry>();
    public DbSet<UserPlayingNow> UserPlayingNows => Set<UserPlayingNow>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
    public DbSet<PostMention> PostMentions => Set<PostMention>();
    public DbSet<CommentMention> CommentMentions => Set<CommentMention>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
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
            profile.Property(p => p.CoverImageUrl).HasMaxLength(500);
            profile.Property(p => p.Region).HasMaxLength(64);
            profile.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(ProfileStatus.Online)
                .IsRequired();
            profile.Property(p => p.LookingForPlayStyle)
                .HasConversion<string>()
                .HasMaxLength(32);
            profile.Property(p => p.LookingForGameNote).HasMaxLength(200);
            profile.Property(p => p.HasCompletedOnboarding).HasDefaultValue(false).IsRequired();
            profile.Property(p => p.ChatSoundEnabled).HasDefaultValue(true).IsRequired();
            profile.Property(p => p.ChatBrowserNotificationsEnabled).HasDefaultValue(true).IsRequired();
            profile.HasOne(p => p.LookingForGame)
                .WithMany()
                .HasForeignKey(p => p.LookingForGameId)
                .OnDelete(DeleteBehavior.SetNull);
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                profile.Property(p => p.Languages).HasColumnType("jsonb");
                profile.Property(p => p.Platforms).HasColumnType("jsonb");
                profile.Property(p => p.Genres).HasColumnType("jsonb");
                profile.Property(p => p.TypicalPlayTimes).HasColumnType("jsonb");
                profile.Property(p => p.ExternalLinks).HasColumnType("jsonb");
            }
            else
            {
                profile.Property(p => p.Languages).HasJsonConversion();
                profile.Property(p => p.Platforms).HasJsonConversion();
                profile.Property(p => p.Genres).HasJsonConversion();
                profile.Property(p => p.TypicalPlayTimes).HasJsonConversion();
                profile.Property(p => p.ExternalLinks).HasJsonConversion();
            }
        });

        builder.Entity<UserPlayingNow>(playingNow =>
        {
            playingNow.HasKey(p => new { p.UserId, p.GameId });
            playingNow.Property(p => p.StatusText).HasMaxLength(200);
            playingNow.HasOne(p => p.Game)
                .WithMany()
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                playingNow.Property(p => p.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                playingNow.Property(p => p.UpdatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<Game>(game =>
        {
            game.HasKey(g => g.Id);
            game.Property(g => g.Name).HasMaxLength(128).IsRequired();
            game.Property(g => g.CoverImageUrl).HasMaxLength(500);
            game.Property(g => g.Genre).HasMaxLength(64);
            game.HasIndex(g => g.RawgId).IsUnique().HasFilter("\"RawgId\" IS NOT NULL");
            game.HasData(
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000001"), Name = "Apex Legends" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000002"), Name = "Call of Duty" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000003"), Name = "Counter-Strike 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000004"), Name = "Destiny 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000005"), Name = "Elden Ring" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000006"), Name = "Genshin Impact" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000007"), Name = "Hollow Knight" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000008"), Name = "Valorant" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000009"), Name = "Doom" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000000a"), Name = "Dota 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000000b"), Name = "League of Legends" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000000c"), Name = "EA Sports FC 25" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000000d"), Name = "UFC 6" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000000e"), Name = "Hogwarts Legacy" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000000f"), Name = "Unravel Two" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000010"), Name = "Marvel's Spider-Man 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000011"), Name = "God of War Ragnarök" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000012"), Name = "The Last of Us Part II" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000013"), Name = "Horizon Forbidden West" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000014"), Name = "Grand Theft Auto V" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000015"), Name = "Minecraft" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000016"), Name = "Fortnite" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000017"), Name = "Overwatch 2" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000018"), Name = "Rocket League" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000019"), Name = "Rainbow Six Siege" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000001a"), Name = "Pokémon Scarlet/Violet" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000001b"), Name = "Mario Kart 8 Deluxe" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000001c"), Name = "The Legend of Zelda: Tears of the Kingdom" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000001d"), Name = "Baldur's Gate 3" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000001e"), Name = "Stardew Valley" },
                new Game { Id = new Guid("00000001-0000-0000-0000-00000000001f"), Name = "It Takes Two" },
                new Game { Id = new Guid("00000001-0000-0000-0000-000000000020"), Name = "Cyberpunk 2077" }
            );
        });

        builder.Entity<UserGameLibraryEntry>(entry =>
        {
            entry.HasKey(e => new { e.UserId, e.GameId });
            entry.Property(e => e.ReviewText).HasMaxLength(1000);
            entry.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                entry.Property(e => e.AddedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                entry.Property(e => e.UpdatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
        });

        builder.Entity<Post>(post =>
        {
            post.HasKey(p => p.Id);
            post.Property(p => p.TextContent).HasMaxLength(1000).IsRequired();
            post.Property(p => p.Mood)
                .HasConversion<string>()
                .HasMaxLength(16);
            post.Property(p => p.Scope)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(PostScope.Feed)
                .IsRequired();
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

        builder.Entity<PostMention>(mention =>
        {
            mention.HasKey(m => m.Id);
            mention.Property(m => m.UsernameAtTimeOfPosting).HasMaxLength(32).IsRequired();
            mention.HasOne(m => m.Post)
                .WithMany()
                .HasForeignKey(m => m.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            mention.HasOne(m => m.MentionedUser)
                .WithMany()
                .HasForeignKey(m => m.MentionedUserId)
                .OnDelete(DeleteBehavior.Cascade);
            mention.HasIndex(m => m.PostId);
        });

        builder.Entity<CommentMention>(mention =>
        {
            mention.HasKey(m => m.Id);
            mention.Property(m => m.UsernameAtTimeOfPosting).HasMaxLength(32).IsRequired();
            mention.HasOne(m => m.Comment)
                .WithMany()
                .HasForeignKey(m => m.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
            mention.HasOne(m => m.MentionedUser)
                .WithMany()
                .HasForeignKey(m => m.MentionedUserId)
                .OnDelete(DeleteBehavior.Cascade);
            mention.HasIndex(m => m.CommentId);
        });

        builder.Entity<Notification>(notification =>
        {
            notification.HasKey(n => n.Id);
            notification.Property(n => n.Type)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            notification.HasOne(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
            notification.HasOne(n => n.Actor)
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            notification.HasIndex(n => new { n.RecipientUserId, n.CreatedAt });
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                notification.Property(n => n.CreatedAt)
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

        builder.Entity<FriendRequest>(friendRequest =>
        {
            friendRequest.HasKey(r => r.Id);
            friendRequest.Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(FriendRequestStatus.Pending)
                .IsRequired();
            friendRequest.HasOne(r => r.Sender)
                .WithMany()
                .HasForeignKey(r => r.SenderUserId)
                .OnDelete(DeleteBehavior.Cascade);
            friendRequest.HasOne(r => r.Recipient)
                .WithMany()
                .HasForeignKey(r => r.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            friendRequest.HasIndex(r => new { r.SenderUserId, r.RecipientUserId, r.Status });
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                friendRequest.Property(r => r.CreatedAt)
                    .HasConversion(
                        v => v.ToUnixTimeMilliseconds(),
                        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                friendRequest.Property(r => r.RespondedAt)
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

        builder.Entity<UserFollow>(follow =>
        {
            follow.HasKey(f => f.Id);
            follow.HasOne(f => f.Follower)
                .WithMany()
                .HasForeignKey(f => f.FollowerUserId)
                .OnDelete(DeleteBehavior.Cascade);
            follow.HasOne(f => f.Following)
                .WithMany()
                .HasForeignKey(f => f.FollowingUserId)
                .OnDelete(DeleteBehavior.Restrict);
            follow.HasIndex(f => new { f.FollowerUserId, f.FollowingUserId }).IsUnique();
            follow.HasIndex(f => f.FollowingUserId);
            if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                follow.Property(f => f.CreatedAt)
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
            message.Property(m => m.MediaUrl).HasMaxLength(500);
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
