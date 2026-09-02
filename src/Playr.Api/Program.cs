using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Playr.Api.Chat;
using Playr.Api.Hubs;
using Playr.Application.Auth;
using Playr.Application.Chat;
using Playr.Application.Invitations;
using Playr.Infrastructure;
using Playr.Api.Steam;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<SteamLinkStateSigner>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, ChatUserIdProvider>();
builder.Services.AddSingleton<Playr.Api.Hubs.IUserConnectionTracker, Playr.Api.Hubs.UserConnectionTracker>();
builder.Services.AddScoped<IChatNotifier, SignalRChatNotifier>();
builder.Services.AddScoped<IInvitationNotifier, Playr.Api.Invitations.SignalRInvitationNotifier>();
builder.Services.AddScoped<Playr.Application.Friends.IFriendRequestNotifier, Playr.Api.Friends.SignalRFriendRequestNotifier>();
builder.Services.AddScoped<Playr.Application.Follows.IFollowNotifier, Playr.Api.Follows.SignalRFollowNotifier>();
builder.Services.AddScoped<Playr.Application.Notifications.INotificationNotifier, Playr.Api.Notifications.SignalRNotificationNotifier>();
builder.Services.AddScoped<Playr.Application.Profiles.IProfilePresenceNotifier, Playr.Api.Profiles.SignalRProfilePresenceNotifier>();
builder.Services.AddScoped<Playr.Application.Lfg.ILfgGroupNotifier, Playr.Api.Lfg.SignalRLfgGroupNotifier>();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:5173", "http://127.0.0.1:5173" };

    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .AllowCredentials();
    });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
JwtOptionsValidator.ValidateForStartup(jwtOptions);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Serve uploaded files (avatars, post media, etc.) from a configurable, persistent
// location that survives redeploys. Falls back to <content root>/wwwroot/uploads
// when FileStorage:RootPath isn't set (matches LocalFileStorageService's default).
var fileStorageRoot = builder.Configuration["FileStorage:RootPath"];
var uploadsRoot = string.IsNullOrWhiteSpace(fileStorageRoot)
    ? Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads")
    : Path.Combine(fileStorageRoot, "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});

app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true);
app.Run();

public partial class Program;
