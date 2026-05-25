using Knovault.Api;
using Knovault.Api.Endpoints;
using Knovault.Api.Hosting;
using Knovault.Application.Covers;
using Knovault.Application.Files;
using Knovault.Application.Library;
using Knovault.Application.Metadata;
using Knovault.Application.Parsing;
using Knovault.Infrastructure.Covers;
using Knovault.Infrastructure.Files;
using Knovault.Infrastructure.Library;
using Knovault.Infrastructure.Metadata;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var paths = new AppPaths();
builder.Services.AddSingleton(paths);

var isTesting = builder.Environment.IsEnvironment("Testing");
var serverUrl = "";
if (!isTesting)
{
    var port = NetworkPorts.FindFreePort(5279);
    serverUrl = $"http://localhost:{port}";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddDbContext<KnovaultDbContext>(o =>
    o.UseSqlite($"Data Source={paths.DbPath};Default Timeout=30")
     .AddInterceptors(new SqliteWalInterceptor()));

builder.Services.AddScoped<IFileHasher, FileHasher>();
builder.Services.AddSingleton<ICoverStore>(_ => new CoverStorage(paths.CoversDir));
builder.Services.AddScoped<IBookFileParser, EpubMetadataParser>();
builder.Services.AddScoped<IBookFileParser, PdfMetadataParser>();
builder.Services.AddScoped<BookParsingService>();
builder.Services.AddScoped<ILibraryScanService, LibraryScanService>();
builder.Services.AddHttpClient<IIsbnMetadataProvider, OpenLibraryIsbnProvider>(c =>
    c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient<ICoverFetcher, HttpCoverFetcher>(c =>
    c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<KnovaultDbContext>().Database.Migrate();

app.MapHealthEndpoints();
app.MapBookEndpoints();
app.MapCopyEndpoints();
app.MapTagEndpoints();
app.MapLibraryEndpoints();
app.MapMetadataEndpoints();
app.MapFallbackToFile("index.html");

if (!isTesting)
    app.Lifetime.ApplicationStarted.Register(() => BrowserLauncher.TryOpen(serverUrl));

app.Run();

public partial class Program; // 供 WebApplicationFactory 使用
