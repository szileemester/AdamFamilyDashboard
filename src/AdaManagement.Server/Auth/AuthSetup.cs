using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AdaManagement.Server.Auth;

/// <summary>
/// BFF: a szerver intézi a Google logint, a böngésző csak egy HttpOnly cookie-t kap.
/// Csak az Auth:AllowedEmails listán szereplő címek léphetnek be.
/// </summary>
public static class AuthSetup
{
    public const string Policy = "Owner";
    public const string CsrfHeader = "X-Ada-Client";

    public static IServiceCollection AddOwnerAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var allowed = config.GetSection("Auth:AllowedEmails").Get<string[]>() ?? [];
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var googleId = config["Auth:Google:ClientId"];
        var googleSecret = config["Auth:Google:ClientSecret"];
        var googleConfigured = !string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret);

        if (!env.IsDevelopment() && (!googleConfigured || allowedSet.Count == 0))
            throw new InvalidOperationException("Auth:Google:ClientId, Auth:Google:ClientSecret és Auth:AllowedEmails kötelező élesben.");

        // A kihívás mindig a cookie-sémáé: az /api így 401-et ad (a WASM kliens erre navigál a loginra), az oldalak pedig
        // az /account/login-ra irányítanak, ami már kifejezetten a Google-t hívja. Google alapértelmezett kihívással
        // az /api is a Google-re irányítana, amit a kliens fetch-e nem tud követni.
        var auth = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "ada.auth";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(180);
                o.SlidingExpiration = true;
                o.LoginPath = "/account/login";
                // API-hívásnál ne átirányítás legyen, hanem 401/403, amit a kliens kezel.
                o.Events.OnRedirectToLogin = ctx => ApiAwareRedirect(ctx, StatusCodes.Status401Unauthorized);
                o.Events.OnRedirectToAccessDenied = ctx => ApiAwareRedirect(ctx, StatusCodes.Status403Forbidden);
            });

        if (googleConfigured)
        {
            auth.AddGoogle(o =>
            {
                o.ClientId = googleId!;
                o.ClientSecret = googleSecret!;
                // Az allowlist itt dől el: a TicketReceived a cookie kiadása ELŐTT fut, és a HandleResponse()
                // valóban megállítja a bejelentkezést (az OnCreatingTicket-beli Fail()-t a handler figyelmen kívül hagyja).
                o.Events.OnTicketReceived = ctx =>
                {
                    var email = ctx.Principal?.FindFirstValue(ClaimTypes.Email);
                    if (email is null || !allowedSet.Contains(email))
                    {
                        ctx.Response.Redirect("/account/denied");
                        ctx.HandleResponse();
                    }
                    return Task.CompletedTask;
                };
                o.Events.OnRemoteFailure = ctx =>
                {
                    ctx.Response.Redirect("/account/denied");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
                ctx.User.FindFirstValue(ClaimTypes.Email) is { } email && allowedSet.Contains(email)));

        return services;
    }

    private static Task ApiAwareRedirect(RedirectContext<CookieAuthenticationOptions> ctx, int status)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = status;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }

    /// <summary>
    /// CSRF-védelem: minden nem-GET API-kérésnek egyedi fejlécet kell küldenie. Böngészőből idegen oldalról ez csak
    /// CORS preflighttal lehetséges, amit a szerver nem engedélyez. A SameSite=Lax cookie mellé második védővonal.
    /// </summary>
    public static IApplicationBuilder UseApiCsrfHeader(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api") &&
                !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) &&
                !ctx.Request.Headers.ContainsKey(CsrfHeader))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            await next();
        });

    public static void MapAccountEndpoints(this WebApplication app, IConfiguration config)
    {
        var g = app.MapGroup("/account");

        g.MapGet("/login", (HttpContext ctx, string? returnUrl) =>
        {
            var target = IsLocal(returnUrl) ? returnUrl! : "/";
            if (ctx.User.Identity?.IsAuthenticated == true) return Results.Redirect(target);
            if (app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(config["Auth:Google:ClientId"]))
                return Results.Redirect($"/account/dev-login?returnUrl={Uri.EscapeDataString(target)}");
            return Results.Challenge(new AuthenticationProperties { RedirectUri = target, IsPersistent = true },
                [GoogleDefaults.AuthenticationScheme]);
        });

        g.MapPost("/logout", async (HttpContext ctx) =>
        {
            // Idegen oldalról beküldött űrlap ne léptessen ki (a böngésző cross-site POST-nál küldi az Origin fejlécet).
            if (!IsSameOrigin(ctx)) return Results.BadRequest();
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // Nem a kezdőlapra: onnan a 401 miatt azonnal újra a bejelentkezésre (és dev-ben/Google-nél vissza) dobna.
            return Results.Redirect("/account/signed-out");
        }).DisableAntiforgery();

        // Kijelentkezés utáni oldal. A mentett dashboardot is törli, hogy kilépve ne látszódjon belőle semmi.
        g.MapGet("/signed-out", () => Results.Content(
            """
            <!doctype html><html lang="hu"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="theme-color" content="#111114"><title>Kijelentkeztél – Ada</title>
            <style>
              body{margin:0;min-height:100vh;display:grid;place-items:center;background:#111114;color:#e8e8ea;font-family:Roboto,'Helvetica Neue',Arial,sans-serif}
              main{text-align:center;padding:2rem}
              h1{font-weight:500;font-size:1.6rem;margin:0 0 .4rem}
              p{color:#9a9aa3;margin:0 0 1.6rem}
              a{display:inline-block;background:#AFA9EC;color:#111114;text-decoration:none;font-weight:500;padding:.7rem 1.4rem;border-radius:10px}
            </style>
            <main><h1>Kijelentkeztél</h1><p>Ezen az eszközön nincs több mentett adat.</p><a href="/account/login">Bejelentkezés</a></main>
            <script>try{['ada.dashboard','ada.weather'].forEach(k=>localStorage.removeItem(k))}catch(e){}</script>
            </html>
            """, "text/html; charset=utf-8"));

        g.MapGet("/denied", () => Results.Content(
            "<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width'>" +
            "<body style='font-family:sans-serif;padding:2rem'><h2>Nincs hozzáférés</h2>" +
            "<p>Ez a Google-fiók nincs engedélyezve.</p><a href='/account/login'>Újra</a></body>", "text/html; charset=utf-8"));

        // Csak fejlesztéskor, ha nincs Google kliens beállítva: belépés az első engedélyezett címmel.
        if (app.Environment.IsDevelopment())
        {
            g.MapGet("/dev-login", async (HttpContext ctx, string? returnUrl) =>
            {
                var email = config.GetSection("Auth:AllowedEmails").Get<string[]>()?.FirstOrDefault() ?? "dev@localhost";
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, "Fejlesztő")],
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                    new AuthenticationProperties { IsPersistent = true });
                return Results.Redirect(IsLocal(returnUrl) ? returnUrl! : "/");
            });
        }
    }

    /// <summary>Csak saját oldalra irányítunk vissza. A beépített ellenőrzés a vezérlőkaraktereket is kiszűri
    /// (pl. „/%09/evil.com” → a böngészőben „//evil.com” lenne).</summary>
    private static bool IsLocal(string? url) => !string.IsNullOrEmpty(url) && RedirectHttpResult.IsLocalUrl(url);

    private static bool IsSameOrigin(HttpContext ctx)
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
            // Origin nélkül a Sec-Fetch-Site dönt (régebbi böngésző vagy nem böngésző: engedjük).
            return ctx.Request.Headers["Sec-Fetch-Site"].ToString() is "" or "same-origin" or "none";
        return Uri.TryCreate(origin, UriKind.Absolute, out var o) &&
               string.Equals(o.Authority, ctx.Request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
