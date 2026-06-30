# PLAYR Backend MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the PLAYR backend MVP with ASP.NET Core Web API, Identity-based auth, JWT, PostgreSQL, Docker Compose, and basic public/authenticated profile endpoints.

**Architecture:** Use a simple layered architecture with `Playr.Api`, `Playr.Application`, `Playr.Domain`, and `Playr.Infrastructure`. Controllers stay thin, application services orchestrate use cases, domain models hold core data, and infrastructure owns EF Core, Identity, PostgreSQL, and migrations.

**Tech Stack:** ASP.NET Core Web API, ASP.NET Core Identity, JWT bearer auth, Entity Framework Core, PostgreSQL via Docker Compose, xUnit, FluentAssertions.

## Global Constraints

- Build backend first only; Angular is out of scope for this plan.
- Use simple layered architecture, not full Clean Architecture.
- Use ASP.NET Core Identity for users and password handling.
- Use JWT bearer tokens for API authentication.
- Use PostgreSQL via Docker Compose for local development.
- Use REST endpoints from the approved spec.
- Track deferred product/technical ideas in `docs/FUTURE.md`.
- Do not implement posts, media upload, discovery, discussions, friends, private chat, external login, refresh tokens, password reset, or email confirmation.
- Do not commit unless the user explicitly asks for commits.

---

## File Structure

Create this structure:

```text
Playr.sln
docker-compose.yml
src/Playr.Api/Playr.Api.csproj
src/Playr.Api/Program.cs
src/Playr.Api/appsettings.json
src/Playr.Api/appsettings.Development.json
src/Playr.Api/Controllers/AuthController.cs
src/Playr.Api/Controllers/ProfilesController.cs
src/Playr.Api/Extensions/ClaimsPrincipalExtensions.cs
src/Playr.Api/Models/Auth/LoginRequest.cs
src/Playr.Api/Models/Auth/LoginResponse.cs
src/Playr.Api/Models/Auth/RegisterRequest.cs
src/Playr.Api/Models/Auth/UserResponse.cs
src/Playr.Api/Models/Profiles/ProfileResponse.cs
src/Playr.Api/Models/Profiles/UpdateProfileRequest.cs
src/Playr.Application/Playr.Application.csproj
src/Playr.Application/Auth/AuthResult.cs
src/Playr.Application/Auth/IAuthService.cs
src/Playr.Application/Auth/JwtOptions.cs
src/Playr.Application/Auth/JwtTokenGenerator.cs
src/Playr.Application/Auth/RegisterUserCommand.cs
src/Playr.Application/Profiles/IProfileService.cs
src/Playr.Application/Profiles/ProfileDto.cs
src/Playr.Application/Profiles/UpdateProfileCommand.cs
src/Playr.Domain/Playr.Domain.csproj
src/Playr.Domain/Identity/ApplicationUser.cs
src/Playr.Domain/Profiles/UserProfile.cs
src/Playr.Infrastructure/Playr.Infrastructure.csproj
src/Playr.Infrastructure/Auth/AuthService.cs
src/Playr.Infrastructure/Data/PlayrDbContext.cs
src/Playr.Infrastructure/DependencyInjection.cs
src/Playr.Infrastructure/Profiles/ProfileService.cs
tests/Playr.Application.Tests/Playr.Application.Tests.csproj
tests/Playr.Application.Tests/Auth/JwtTokenGeneratorTests.cs
tests/Playr.IntegrationTests/Playr.IntegrationTests.csproj
```

Responsibilities:

- `Playr.Api`: HTTP boundary, model validation, auth middleware, controller responses.
- `Playr.Application`: service interfaces, commands, DTOs, JWT generation logic that can be unit-tested without HTTP.
- `Playr.Domain`: Identity user type and profile entity.
- `Playr.Infrastructure`: EF Core, Identity stores, PostgreSQL, concrete services.
- `tests/Playr.Application.Tests`: fast unit tests.
- `tests/Playr.IntegrationTests`: endpoint tests using `WebApplicationFactory` and a replaceable test database strategy.

---

### Task 1: Scaffold Solution, Projects, and Docker Compose

**Files:**
- Create: `Playr.sln`
- Create: `docker-compose.yml`
- Create: `src/Playr.Api/Playr.Api.csproj`
- Create: `src/Playr.Application/Playr.Application.csproj`
- Create: `src/Playr.Domain/Playr.Domain.csproj`
- Create: `src/Playr.Infrastructure/Playr.Infrastructure.csproj`
- Create: `tests/Playr.Application.Tests/Playr.Application.Tests.csproj`
- Create: `tests/Playr.IntegrationTests/Playr.IntegrationTests.csproj`

**Interfaces:**
- Produces: buildable .NET solution with project references and PostgreSQL service.
- Consumes: approved design in `docs/superpowers/specs/2026-06-29-playr-backend-mvp-design.md`.

- [ ] **Step 1: Verify SDK availability**

Run:

```powershell
dotnet --version
```

Expected: version output. Prefer .NET 8 or later.

- [ ] **Step 2: Create the solution and projects**

Run from `C:\NoBackup\development\Playr`:

```powershell
dotnet new sln -n Playr
dotnet new webapi -n Playr.Api -o src/Playr.Api --use-controllers
dotnet new classlib -n Playr.Application -o src/Playr.Application
dotnet new classlib -n Playr.Domain -o src/Playr.Domain
dotnet new classlib -n Playr.Infrastructure -o src/Playr.Infrastructure
dotnet new xunit -n Playr.Application.Tests -o tests/Playr.Application.Tests
dotnet new xunit -n Playr.IntegrationTests -o tests/Playr.IntegrationTests
dotnet sln Playr.sln add src/Playr.Api/Playr.Api.csproj src/Playr.Application/Playr.Application.csproj src/Playr.Domain/Playr.Domain.csproj src/Playr.Infrastructure/Playr.Infrastructure.csproj tests/Playr.Application.Tests/Playr.Application.Tests.csproj tests/Playr.IntegrationTests/Playr.IntegrationTests.csproj
```

Expected: each command succeeds.

- [ ] **Step 3: Add project references**

Run:

```powershell
dotnet add src/Playr.Api/Playr.Api.csproj reference src/Playr.Application/Playr.Application.csproj src/Playr.Infrastructure/Playr.Infrastructure.csproj
dotnet add src/Playr.Application/Playr.Application.csproj reference src/Playr.Domain/Playr.Domain.csproj
dotnet add src/Playr.Infrastructure/Playr.Infrastructure.csproj reference src/Playr.Application/Playr.Application.csproj src/Playr.Domain/Playr.Domain.csproj
dotnet add tests/Playr.Application.Tests/Playr.Application.Tests.csproj reference src/Playr.Application/Playr.Application.csproj src/Playr.Domain/Playr.Domain.csproj
dotnet add tests/Playr.IntegrationTests/Playr.IntegrationTests.csproj reference src/Playr.Api/Playr.Api.csproj
```

Expected: references are added.

- [ ] **Step 4: Add NuGet packages**

Run:

```powershell
dotnet add src/Playr.Api/Playr.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Playr.Infrastructure/Playr.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add src/Playr.Infrastructure/Playr.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/Playr.Infrastructure/Playr.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add tests/Playr.Application.Tests/Playr.Application.Tests.csproj package FluentAssertions
dotnet add tests/Playr.IntegrationTests/Playr.IntegrationTests.csproj package FluentAssertions
dotnet add tests/Playr.IntegrationTests/Playr.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

Expected: package restore succeeds.

- [ ] **Step 5: Create Docker Compose file**

Create `docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: playr-postgres
    environment:
      POSTGRES_DB: playr
      POSTGRES_USER: playr
      POSTGRES_PASSWORD: playr_dev_password
    ports:
      - "5432:5432"
    volumes:
      - playr-postgres-data:/var/lib/postgresql/data

volumes:
  playr-postgres-data:
```

- [ ] **Step 6: Verify solution builds**

Run:

```powershell
dotnet build Playr.sln
```

Expected: build succeeds with 0 errors.

- [ ] **Step 7: Inspect changes**

Run:

```powershell
git status --short
```

Expected: new solution, source, test, and Docker files are listed. Do not commit unless the user explicitly asks.

---

### Task 2: Add Domain Models and Application Contracts

**Files:**
- Create: `src/Playr.Domain/Identity/ApplicationUser.cs`
- Create: `src/Playr.Domain/Profiles/UserProfile.cs`
- Create: `src/Playr.Application/Auth/AuthResult.cs`
- Create: `src/Playr.Application/Auth/IAuthService.cs`
- Create: `src/Playr.Application/Auth/JwtOptions.cs`
- Create: `src/Playr.Application/Auth/RegisterUserCommand.cs`
- Create: `src/Playr.Application/Profiles/IProfileService.cs`
- Create: `src/Playr.Application/Profiles/ProfileDto.cs`
- Create: `src/Playr.Application/Profiles/UpdateProfileCommand.cs`

**Interfaces:**
- Produces: `ApplicationUser`, `UserProfile`, `IAuthService`, `IProfileService`, command/DTO records.
- Consumes: project structure from Task 1.

- [ ] **Step 1: Write domain entities**

Create `src/Playr.Domain/Identity/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Playr.Domain.Profiles;

namespace Playr.Domain.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UserProfile? Profile { get; set; }
}
```

Create `src/Playr.Domain/Profiles/UserProfile.cs`:

```csharp
using Playr.Domain.Identity;

namespace Playr.Domain.Profiles;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Region { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> Platforms { get; set; } = [];
    public Dictionary<string, string> ExternalLinks { get; set; } = [];
    public List<string> CurrentlyPlayingGames { get; set; } = [];
    public bool LookingForPlayers { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 2: Write application auth contracts**

Create `src/Playr.Application/Auth/RegisterUserCommand.cs`:

```csharp
namespace Playr.Application.Auth;

public sealed record RegisterUserCommand(string Email, string Username, string Password);
```

Create `src/Playr.Application/Auth/AuthResult.cs`:

```csharp
namespace Playr.Application.Auth;

public sealed record AuthUserDto(Guid Id, string Email, string Username, string DisplayName);

public sealed record AuthResult(string AccessToken, DateTimeOffset ExpiresAt);
```

Create `src/Playr.Application/Auth/JwtOptions.cs`:

```csharp
namespace Playr.Application.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
```

Create `src/Playr.Application/Auth/IAuthService.cs`:

```csharp
namespace Playr.Application.Auth;

public interface IAuthService
{
    Task<AuthUserDto> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken);
    Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Write profile contracts**

Create `src/Playr.Application/Profiles/ProfileDto.cs`:

```csharp
namespace Playr.Application.Profiles;

public sealed record ProfileDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? Region,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Platforms,
    IReadOnlyDictionary<string, string> ExternalLinks,
    IReadOnlyList<string> CurrentlyPlayingGames,
    bool LookingForPlayers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Create `src/Playr.Application/Profiles/UpdateProfileCommand.cs`:

```csharp
namespace Playr.Application.Profiles;

public sealed record UpdateProfileCommand(
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? Region,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Platforms,
    IReadOnlyDictionary<string, string> ExternalLinks,
    IReadOnlyList<string> CurrentlyPlayingGames,
    bool LookingForPlayers);
```

Create `src/Playr.Application/Profiles/IProfileService.cs`:

```csharp
namespace Playr.Application.Profiles;

public interface IProfileService
{
    Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build Playr.sln
```

Expected: build succeeds with 0 errors.

---

### Task 3: Configure EF Core, Identity, PostgreSQL, and API Startup

**Files:**
- Create: `src/Playr.Infrastructure/Data/PlayrDbContext.cs`
- Create: `src/Playr.Infrastructure/DependencyInjection.cs`
- Modify: `src/Playr.Api/Program.cs`
- Modify: `src/Playr.Api/appsettings.json`
- Modify: `src/Playr.Api/appsettings.Development.json`

**Interfaces:**
- Consumes: `ApplicationUser`, `UserProfile`, `JwtOptions` from Task 2.
- Produces: configured DI for `PlayrDbContext`, Identity, PostgreSQL, JWT bearer auth.

- [ ] **Step 1: Write failing infrastructure build target**

Run before implementation:

```powershell
dotnet build src/Playr.Infrastructure/Playr.Infrastructure.csproj
```

Expected before code exists: build fails because `PlayrDbContext` is missing if referenced by the next step's API configuration. If it still builds, continue and rely on Task 3 final build as the verification gate.

- [ ] **Step 2: Create DbContext**

Create `src/Playr.Infrastructure/Data/PlayrDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;

namespace Playr.Infrastructure.Data;

public sealed class PlayrDbContext(DbContextOptions<PlayrDbContext> options)
    : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
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
```

- [ ] **Step 3: Create infrastructure DI**

Create `src/Playr.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Domain.Identity;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<PlayrDbContext>(options => options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PlayrDbContext>();

        return services;
    }
}
```

- [ ] **Step 4: Configure appsettings**

Update `src/Playr.Api/appsettings.json`:

```json
{
  "Jwt": {
    "Issuer": "PLAYR",
    "Audience": "PLAYR",
    "SigningKey": "replace-this-development-key-with-user-secrets-before-production",
    "ExpirationMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Update `src/Playr.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=playr;Username=playr;Password=playr_dev_password"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

- [ ] **Step 5: Configure Program.cs**

Replace `src/Playr.Api/Program.cs` with:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Playr.Application.Auth;
using Playr.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
```

- [ ] **Step 6: Verify build**

Run:

```powershell
dotnet build Playr.sln
```

Expected: build succeeds with 0 errors.

---

### Task 4: Add JWT Token Generator with Unit Tests

**Files:**
- Create: `src/Playr.Application/Auth/JwtTokenGenerator.cs`
- Create: `tests/Playr.Application.Tests/Auth/JwtTokenGeneratorTests.cs`

**Interfaces:**
- Consumes: `JwtOptions`, `ApplicationUser`.
- Produces: `JwtTokenGenerator.Generate(ApplicationUser user): AuthResult`.

- [ ] **Step 1: Write failing unit test**

Create `tests/Playr.Application.Tests/Auth/JwtTokenGeneratorTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Options;
using Playr.Application.Auth;
using Playr.Domain.Identity;

namespace Playr.Application.Tests.Auth;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void Generate_ReturnsTokenAndExpiration()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "PLAYR",
            Audience = "PLAYR",
            SigningKey = "this-is-a-development-test-key-with-enough-length",
            ExpirationMinutes = 60
        });
        var generator = new JwtTokenGenerator(options);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            UserName = "playerOne"
        };

        var result = generator.Generate(user);

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(55));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/Playr.Application.Tests/Playr.Application.Tests.csproj --filter JwtTokenGeneratorTests
```

Expected: fail because `JwtTokenGenerator` does not exist.

- [ ] **Step 3: Implement token generator**

Create `src/Playr.Application/Auth/JwtTokenGenerator.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Playr.Domain.Identity;

namespace Playr.Application.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public AuthResult Generate(ApplicationUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("username", user.UserName ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AuthResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
```

- [ ] **Step 4: Ensure Application has token packages**

Run if compile errors show missing JWT namespaces:

```powershell
dotnet add src/Playr.Application/Playr.Application.csproj package System.IdentityModel.Tokens.Jwt
dotnet add src/Playr.Application/Playr.Application.csproj package Microsoft.Extensions.Options
```

Expected: packages added and restored.

- [ ] **Step 5: Run test to verify it passes**

Run:

```powershell
dotnet test tests/Playr.Application.Tests/Playr.Application.Tests.csproj --filter JwtTokenGeneratorTests
```

Expected: test passes.

---

### Task 5: Implement Auth Service and Auth Controller

**Files:**
- Create: `src/Playr.Infrastructure/Auth/AuthService.cs`
- Modify: `src/Playr.Infrastructure/DependencyInjection.cs`
- Create: `src/Playr.Api/Models/Auth/RegisterRequest.cs`
- Create: `src/Playr.Api/Models/Auth/LoginRequest.cs`
- Create: `src/Playr.Api/Models/Auth/LoginResponse.cs`
- Create: `src/Playr.Api/Models/Auth/UserResponse.cs`
- Create: `src/Playr.Api/Extensions/ClaimsPrincipalExtensions.cs`
- Create: `src/Playr.Api/Controllers/AuthController.cs`

**Interfaces:**
- Consumes: `IAuthService`, `JwtTokenGenerator`, Identity, `PlayrDbContext`.
- Produces: `/api/auth/register`, `/api/auth/login`, `/api/auth/me`.

- [ ] **Step 1: Write request/response models**

Create `src/Playr.Api/Models/Auth/RegisterRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(32, MinimumLength = 3)] string Username,
    [Required, MinLength(8)] string Password);
```

Create `src/Playr.Api/Models/Auth/LoginRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record LoginRequest(
    [Required] string UsernameOrEmail,
    [Required] string Password);
```

Create `src/Playr.Api/Models/Auth/LoginResponse.cs`:

```csharp
namespace Playr.Api.Models.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
```

Create `src/Playr.Api/Models/Auth/UserResponse.cs`:

```csharp
namespace Playr.Api.Models.Auth;

public sealed record UserResponse(Guid Id, string Email, string Username, string DisplayName);
```

- [ ] **Step 2: Implement AuthService**

Create `src/Playr.Infrastructure/Auth/AuthService.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Auth;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    PlayrDbContext dbContext,
    JwtTokenGenerator tokenGenerator) : IAuthService
{
    public async Task<AuthUserDto> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var usernameExists = await userManager.Users.AnyAsync(u => u.NormalizedUserName == command.Username.ToUpperInvariant(), cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            UserName = command.Username
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        var profile = new UserProfile
        {
            UserId = user.Id,
            Username = command.Username,
            DisplayName = command.Username
        };

        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthUserDto(user.Id, user.Email!, user.UserName!, profile.DisplayName);
    }

    public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken)
    {
        var normalized = usernameOrEmail.ToUpperInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized,
            cancellationToken);

        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedAccessException("Invalid username/email or password.");
        }

        return tokenGenerator.Generate(user);
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null || user.Profile is null
            ? null
            : new AuthUserDto(user.Id, user.Email!, user.UserName!, user.Profile.DisplayName);
    }
}
```

- [ ] **Step 3: Register services in DI**

Modify `src/Playr.Infrastructure/DependencyInjection.cs` and add these registrations before `return services;`:

```csharp
services.AddScoped<Playr.Application.Auth.JwtTokenGenerator>();
services.AddScoped<Playr.Application.Auth.IAuthService, Playr.Infrastructure.Auth.AuthService>();
```

- [ ] **Step 4: Create claims extension**

Create `src/Playr.Api/Extensions/ClaimsPrincipalExtensions.cs`:

```csharp
using System.Security.Claims;

namespace Playr.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User id claim is missing.");
        return Guid.Parse(value);
    }
}
```

- [ ] **Step 5: Create AuthController**

Create `src/Playr.Api/Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Auth;
using Playr.Application.Auth;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await authService.RegisterAsync(new RegisterUserCommand(request.Email, request.Username, request.Password), cancellationToken);
            return Ok(new UserResponse(user.Id, user.Email, user.Username, user.DisplayName));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.LoginAsync(request.UsernameOrEmail, request.Password, cancellationToken);
            return Ok(new LoginResponse(result.AccessToken, result.ExpiresAt));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);
        return user is null
            ? Unauthorized(new { error = "Current user was not found." })
            : Ok(new UserResponse(user.Id, user.Email, user.Username, user.DisplayName));
    }
}
```

- [ ] **Step 6: Verify build**

Run:

```powershell
dotnet build Playr.sln
```

Expected: build succeeds with 0 errors.

---

### Task 6: Implement Profile Service and Profiles Controller

**Files:**
- Create: `src/Playr.Infrastructure/Profiles/ProfileService.cs`
- Modify: `src/Playr.Infrastructure/DependencyInjection.cs`
- Create: `src/Playr.Api/Models/Profiles/ProfileResponse.cs`
- Create: `src/Playr.Api/Models/Profiles/UpdateProfileRequest.cs`
- Create: `src/Playr.Api/Controllers/ProfilesController.cs`

**Interfaces:**
- Consumes: `IProfileService`, `UpdateProfileCommand`, `ProfileDto`.
- Produces: `GET /api/profiles/{username}` and `PUT /api/profiles/me`.

- [ ] **Step 1: Create profile API models**

Create `src/Playr.Api/Models/Profiles/ProfileResponse.cs`:

```csharp
namespace Playr.Api.Models.Profiles;

public sealed record ProfileResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? Region,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Platforms,
    IReadOnlyDictionary<string, string> ExternalLinks,
    IReadOnlyList<string> CurrentlyPlayingGames,
    bool LookingForPlayers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Create `src/Playr.Api/Models/Profiles/UpdateProfileRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Profiles;

public sealed record UpdateProfileRequest(
    [Required, StringLength(64, MinimumLength = 1)] string DisplayName,
    [StringLength(500)] string? Bio,
    [StringLength(500)] string? AvatarUrl,
    [StringLength(64)] string? Region,
    IReadOnlyList<string>? Languages,
    IReadOnlyList<string>? Platforms,
    IReadOnlyDictionary<string, string>? ExternalLinks,
    IReadOnlyList<string>? CurrentlyPlayingGames,
    bool LookingForPlayers);
```

- [ ] **Step 2: Implement ProfileService**

Create `src/Playr.Infrastructure/Profiles/ProfileService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Playr.Application.Profiles;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Profiles;

public sealed class ProfileService(PlayrDbContext dbContext) : IProfileService
{
    public async Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Username.ToUpper() == normalized, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        if (command.CurrentlyPlayingGames.Count > 20)
        {
            throw new InvalidOperationException("Currently playing games cannot contain more than 20 items.");
        }

        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        profile.DisplayName = command.DisplayName.Trim();
        profile.Bio = command.Bio?.Trim();
        profile.AvatarUrl = command.AvatarUrl?.Trim();
        profile.Region = command.Region?.Trim();
        profile.Languages = command.Languages.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
        profile.Platforms = command.Platforms.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
        profile.ExternalLinks = command.ExternalLinks.ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim());
        profile.CurrentlyPlayingGames = command.CurrentlyPlayingGames.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
        profile.LookingForPlayers = command.LookingForPlayers;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    private static ProfileDto ToDto(UserProfile profile) => new(
        profile.UserId,
        profile.Username,
        profile.DisplayName,
        profile.Bio,
        profile.AvatarUrl,
        profile.Region,
        profile.Languages,
        profile.Platforms,
        profile.ExternalLinks,
        profile.CurrentlyPlayingGames,
        profile.LookingForPlayers,
        profile.CreatedAt,
        profile.UpdatedAt);
}
```

- [ ] **Step 3: Register profile service**

Modify `src/Playr.Infrastructure/DependencyInjection.cs` and add before `return services;`:

```csharp
services.AddScoped<Playr.Application.Profiles.IProfileService, Playr.Infrastructure.Profiles.ProfileService>();
```

- [ ] **Step 4: Create ProfilesController**

Create `src/Playr.Api/Controllers/ProfilesController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Profiles;
using Playr.Application.Profiles;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(IProfileService profileService) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<ProfileResponse>> GetByUsername(string username, CancellationToken cancellationToken)
    {
        var profile = await profileService.GetByUsernameAsync(username, cancellationToken);
        return profile is null ? NotFound(new { error = "Profile was not found." }) : Ok(ToResponse(profile));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await profileService.UpdateCurrentUserAsync(
                User.GetUserId(),
                new UpdateProfileCommand(
                    request.DisplayName,
                    request.Bio,
                    request.AvatarUrl,
                    request.Region,
                    request.Languages ?? [],
                    request.Platforms ?? [],
                    request.ExternalLinks ?? new Dictionary<string, string>(),
                    request.CurrentlyPlayingGames ?? [],
                    request.LookingForPlayers),
                cancellationToken);

            return Ok(ToResponse(profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static ProfileResponse ToResponse(ProfileDto profile) => new(
        profile.UserId,
        profile.Username,
        profile.DisplayName,
        profile.Bio,
        profile.AvatarUrl,
        profile.Region,
        profile.Languages,
        profile.Platforms,
        profile.ExternalLinks,
        profile.CurrentlyPlayingGames,
        profile.LookingForPlayers,
        profile.CreatedAt,
        profile.UpdatedAt);
}
```

- [ ] **Step 5: Verify build**

Run:

```powershell
dotnet build Playr.sln
```

Expected: build succeeds with 0 errors.

---

### Task 7: Add Migrations and Manual Endpoint Verification

**Files:**
- Create: `src/Playr.Infrastructure/Migrations/*`
- Modify: generated migration files only if EF generates invalid mappings.

**Interfaces:**
- Consumes: `PlayrDbContext`.
- Produces: PostgreSQL schema for Identity and profiles.

- [ ] **Step 1: Start PostgreSQL**

Run:

```powershell
docker compose up -d
```

Expected: `playr-postgres` container starts.

- [ ] **Step 2: Install EF tool if needed**

Run:

```powershell
dotnet tool list --global
```

If `dotnet-ef` is absent, run:

```powershell
dotnet tool install --global dotnet-ef
```

Expected: `dotnet-ef` is available.

- [ ] **Step 3: Add initial migration**

Run:

```powershell
dotnet ef migrations add InitialIdentityAndProfiles --project src/Playr.Infrastructure/Playr.Infrastructure.csproj --startup-project src/Playr.Api/Playr.Api.csproj --context PlayrDbContext
```

Expected: migration files are created under `src/Playr.Infrastructure/Migrations`.

- [ ] **Step 4: Apply migration**

Run:

```powershell
dotnet ef database update --project src/Playr.Infrastructure/Playr.Infrastructure.csproj --startup-project src/Playr.Api/Playr.Api.csproj --context PlayrDbContext
```

Expected: database update succeeds.

- [ ] **Step 5: Run API**

Run:

```powershell
dotnet run --project src/Playr.Api/Playr.Api.csproj
```

Expected: API starts and Swagger is available in Development.

- [ ] **Step 6: Verify auth/profile manually**

Use Swagger or HTTP client to verify:

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "username": "playerOne",
  "password": "StrongPassword123!"
}
```

Expected: `200 OK` with `id`, `email`, `username`, `displayName`.

```http
POST /api/auth/login
Content-Type: application/json

{
  "usernameOrEmail": "playerOne",
  "password": "StrongPassword123!"
}
```

Expected: `200 OK` with `accessToken` and `expiresAt`.

```http
GET /api/auth/me
Authorization: Bearer <accessToken>
```

Expected: `200 OK` with current user.

```http
PUT /api/profiles/me
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "displayName": "Viktor",
  "bio": "Mostly RPGs and co-op games.",
  "avatarUrl": "https://example.com/avatar.png",
  "region": "EU",
  "languages": ["Swedish", "English"],
  "platforms": ["PC", "PlayStation"],
  "externalLinks": {
    "steam": "https://steamcommunity.com/example"
  },
  "currentlyPlayingGames": ["Helldivers 2", "Baldur's Gate 3"],
  "lookingForPlayers": true
}
```

Expected: `200 OK` and response includes `currentlyPlayingGames`.

```http
GET /api/profiles/playerOne
```

Expected: `200 OK` with public profile.

- [ ] **Step 7: Final verification**

Run:

```powershell
dotnet test Playr.sln
dotnet build Playr.sln
```

Expected: both commands succeed with 0 errors.

---

## Self-Review Notes

- Spec coverage: the plan covers backend solution, Docker Compose PostgreSQL, EF Core, Identity, JWT auth, register/login/me, public profile read, authenticated profile update, currently playing games, and deferred future work.
- Out-of-scope features remain excluded: Angular, posts, media, discovery, discussions, friends, chat, external login, password reset, email confirmation, and refresh tokens.
- Placeholder scan: no placeholder markers or intentionally vague implementation steps are used.
- Type consistency: endpoint models map to application commands/DTOs; services depend on the interfaces defined earlier; controllers consume those interfaces.
