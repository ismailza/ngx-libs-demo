using Demo.Api.Auth;
using Demo.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Data;

public static class DatabaseSetup
{
    public static IServiceCollection AddDemoDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
        return services;
    }

    /// <summary>
    /// Applies pending migrations, then (Local provider only) creates the roles and the demo
    /// accounts. Migrating on startup suits a demo; a production deployment would run a
    /// migration bundle before rolling out instead.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var config = app.Configuration;

        if (config.GetValue("Database:MigrateOnStartup", true))
        {
            await services.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        if (config.GetAuthProvider() != AuthProvider.Local)
        {
            return;
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.User, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (!config.GetValue("Seed:Enabled", false))
        {
            return;
        }

        var password = config["Seed:Password"] ?? "Passw0rd!";
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        await EnsureUserAsync(users, new AppUser { UserName = "alice", Email = "alice@demo.local", FirstName = "Alice", LastName = "Martin", CreatedAt = now }, password, [Roles.User]);
        await EnsureUserAsync(users, new AppUser { UserName = "admin", Email = "admin@demo.local", FirstName = "Ada", LastName = "Admin", CreatedAt = now }, password, [Roles.User, Roles.Admin]);
    }

    private static async Task EnsureUserAsync(UserManager<AppUser> users, AppUser user, string password, string[] roles)
    {
        if (await users.FindByNameAsync(user.UserName!) is not null)
        {
            return;
        }

        user.EmailConfirmed = true;
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding '{user.UserName}' failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await users.AddToRolesAsync(user, roles);
    }
}
