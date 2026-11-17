using AdaManagement.Server.Api;
using AdaManagement.Server.Auth;
using AdaManagement.Server.Data;
using AdaManagement.Server.Domain;
using AdaManagement.Server.Tournaments.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
    builder.Configuration.GetConnectionString("Default"),
    // Az Azure SQL free offer serverless: szünetelés után ébredni kell neki, addig újrapróbálunk.
    sql => sql.EnableRetryOnFailure(maxRetryCount: 8, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("AdaManagement");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddOwnerAuth(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddTournaments();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Az App Service front-endje mögött a https sémát a fejlécből kell visszaállítani (Google redirect_uri miatt).
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Biztonsági fejlécek minden válaszra: idegen oldal nem ágyazhatja be (clickjacking), nincs MIME-találgatás.
app.Use((ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        var h = ctx.Response.Headers;
        h["Content-Security-Policy"] = "frame-ancestors 'none'";
        h["X-Frame-Options"] = "DENY";
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "same-origin";
        return Task.CompletedTask;
    });
    return next(ctx);
});
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseWebAssemblyDebugging();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseApiCsrfHeader();

app.MapAccountEndpoints(app.Configuration);
app.MapApi();
// A kliens összes statikus fájlja (a _framework és az ujjlenyomatos pl. js/ambient.<hash>.js is) – a UseStaticFiles ezeket nem ismeri.
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
