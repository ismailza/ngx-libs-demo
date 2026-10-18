using Demo.Api.Data;
using Demo.Api.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Demo.Api.Auth.Local;

public static class LocalAuthSetup
{
    public static WebApplicationBuilder AddLocalAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RefreshTokenOptions>()
            .BindConfiguration(RefreshTokenOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // No '@' (or '+'): login treats anything with '@' as an email, so a username
                // must never look like one, or it could claim someone else's email as a login.
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                // The JWT carries the user id in `sub`; UserManager.GetUserAsync reads it from there.
                options.ClaimsIdentity.UserIdClaimType = "sub";
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((options, jwt, clock) =>
            {
                // Keep claim names as issued (`sub`, `roles`) instead of mapping them to long URIs.
                options.MapInboundClaims = false;
                options.TimeProvider = clock;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = AccessTokenIssuer.KeyFrom(jwt.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles",
                    // The default 5 minutes would keep a 5-minute token alive for 10.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddScoped<AccessTokenIssuer>();
        services.AddScoped<RefreshTokenStore>();
        services.AddSingleton<RefreshCookie>();
        return builder;
    }
}
