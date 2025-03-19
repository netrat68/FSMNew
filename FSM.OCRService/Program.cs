using FSM.OCRService;
using FSM.OCRService.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Information()
	.WriteTo.Console()
	.WriteTo.File("logs/ocrservicelog.txt", rollingInterval: RollingInterval.Day )
	.CreateLogger();

builder.Host.UseSerilog();

builder.WebHost.ConfigureKestrel(options =>
{
	// Setup a HTTP/2 endpoint without TLS.
	options.ListenAnyIP(81, o => o.Protocols = HttpProtocols.Http2);
});

var confInstance = new Conf();

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
builder.Configuration
	.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
	.AddJsonFile("appsettings.Development.json", optional: true)
	.AddJsonFile("appsettings.json", optional: true)
	.AddEnvironmentVariables("app_")
	.AddEnvironmentVariables("DEVELOPMENT_")
	.AddEnvironmentVariables("TESTING_")
	.AddEnvironmentVariables("STAGING_")
	.AddEnvironmentVariables("PRODUCTION_")
	.AddEnvironmentVariables("ASPNETCORE_");
builder.Configuration.GetSection("MinIoSensitive").Bind(confInstance);
builder.Configuration.GetSection("UserStoreSettings").Bind(confInstance);
builder.Configuration.GetSection("TokenSettings").Bind(confInstance);
builder.Configuration.GetSection("OmniLockSettings").Bind(confInstance);
builder.Configuration.Bind(confInstance);

// Add services to the container.
builder.Services.AddScoped<IImageStorageService, ImageStorageService>(); // Use interface for mocking purposes while unit testing.
builder.Services.AddScoped<OcrService>();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<IdCardReaderService>();//.AllowAnonymous().DisableRateLimiting();

app.MapGrpcReflectionService();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
