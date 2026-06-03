using Hangfire;
using Hangfire.Storage.SQLite;
using System.Text.Json.Serialization;
using ObsStreamingOpener.Application;
using ObsStreamingOpener.Application.Hangfire;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDirectory);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfire((_, configuration) =>
{
    var hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire")
        ?? "Data Source=data/hangfire.db";
    var hangfireDatabasePath = ResolveSqliteDatabasePath(hangfireConnectionString);

    configuration
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSQLiteStorage(hangfireDatabasePath);
});
builder.Services.AddHangfireServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    HangfireJobRegistrar.RegisterRecurringJobs(scope.ServiceProvider.GetRequiredService<IRecurringJobManager>());
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHangfireDashboard("/hangfire");
app.MapHealthChecks("/health");
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/widgets/stats.html"));

app.Run();

static string ResolveSqliteDatabasePath(string value)
{
    const string dataSourcePrefix = "Data Source=";
    if (!value.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
    {
        return value;
    }

    var databasePath = value[dataSourcePrefix.Length..];
    var semicolonIndex = databasePath.IndexOf(';', StringComparison.Ordinal);
    return semicolonIndex >= 0 ? databasePath[..semicolonIndex] : databasePath;
}

public partial class Program;
