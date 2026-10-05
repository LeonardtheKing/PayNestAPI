using LoanAPI;
using LoanAPI.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Sentry(builder.Configuration["GlitchTip:Dsn"])
    .CreateLogger();
builder.Host.UseSerilog();

// 2. Configure Sentry / GlitchTip integration
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Glitchtip:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
    options.TracesSampleRate = 0.2;
    options.MinimumBreadcrumbLevel = LogLevel.Information;
    options.MinimumEventLevel = LogLevel.Debug;

    options.SetBeforeSend((sentryEvent, hint) =>
    {
        sentryEvent.Request?.Headers?.Remove("x-paystack-signature");
        sentryEvent.Request?.Data = null;
        return sentryEvent;
    });
});

// 3. Add services to the container
builder.Services.AddControllers();

// === ADD CORS POLICY HERE ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped<ITransactionProcessingService, TransactionProcessingService>();

// Register Entity Framework Core DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient factory
builder.Services.AddHttpClient();

// Built-in .NET OpenAPI support
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<IPaystackVirtualAccountService, PaystackVirtualAccountService>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Mini-Bank API",
            Version = "v1"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// 4. Configure the HTTP request pipeline & Middleware
app.UseHttpsRedirection();

// === USE CORS MIDDLEWARE HERE ===
// Note: app.UseCors() must be placed before UseAuthorization() and before mapping endpoints.
app.UseCors("AllowAll");

app.UseAuthorization();

// Map OpenAPI & Scalar documentation
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Mini bank");
});

// Map Controllers so endpoints become active
app.MapControllers();

// Optional: Test endpoint to verify GlitchTip catches errors instantly
app.MapGet("/debug-glitchtip", () =>
{
    throw new Exception("Test GlitchTip error from Micro-Lending API!");
});

// 5. Run Database Seeder
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await BankDataSeeder.SeedBanksAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database with Nigerian banks.");
    }
}

// 6. Start the Application Web Host (Only one app.Run() at the very end)
try
{
    Log.Information("Starting Micro-Lending API web host...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly!");
}
finally
{
    Log.CloseAndFlush();
}