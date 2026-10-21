using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Serilog;
using System.Text.Json.Serialization;
using TradeIngestion.Core.Ingestion;
using TradeIngestion.Core.Snapshot;
using TradeIngestion.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' not found.");
// EF / LocalDB. Ingest + snapshot implementations live in Infrastructure.
builder.Services.AddInfrastructure(connectionString);
// No database of its own — one instance of the duplicate/correction rule is enough.
builder.Services.AddSingleton<IngestionDecisionService>();

// Structured logs to stdout. In production the host (Azure/IIS/Docker) captures console; do not log into the ledger DB.
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // So JSON shows "Accepted" not 1 (Blazor and Swagger).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFeatureManagement();
// Interface → implementation. Scoped = one per HTTP request (needs DbContext).
builder.Services.AddScoped<ITradeIngestionService, TradeIngestionService>();
builder.Services.AddScoped<ICashIngestionService, CashIngestionService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();


builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            // Blazor runs on a different port; browser would block API calls without CORS.
            .WithOrigins("https://localhost:7123","http://localhost:5209")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // Creates TradeIngestion + seed on first F5.
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Needed so WebApplicationFactory in tests can start this app.
public partial class Program { }