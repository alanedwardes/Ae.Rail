using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on multiple ports
builder.WebHost.ConfigureKestrel(serverOptions =>
{
	var mainPort = builder.Configuration.GetValue<int?>("Ports:Main") ?? 8080;
	var backofficePort = builder.Configuration.GetValue<int?>("Ports:Backoffice") ?? 8081;
	
	// Main API port
	serverOptions.ListenAnyIP(mainPort, listenOptions =>
	{
		listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
	});
	
	// Backoffice API port
	serverOptions.ListenAnyIP(backofficePort, listenOptions =>
	{
		listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
	});
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers().AddNewtonsoftJson(o =>
{
	o.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
});

builder.Services.AddDbContext<Ae.Rail.Data.PostgresDbContext>((sp, options) =>
{
	var cs = sp.GetRequiredService<IConfiguration>().GetSection("postgres").GetValue<string>("connectionString");
	options.UseNpgsql(cs);
});

builder.Services.AddScoped<Ae.Rail.Services.IPostgresRawEventWriter, Ae.Rail.Services.PostgresRawEventWriter>();
builder.Services.AddScoped<Ae.Rail.Services.ITrainDataParser, Ae.Rail.Services.TrainDataParser>();
builder.Services.AddScoped<Ae.Rail.Services.IReprocessingService, Ae.Rail.Services.ReprocessingService>();
builder.Services.AddHostedService<Ae.Rail.Services.TrainsConsumerService>();
// Materialized view refresh service (DEPRECATED - replaced by real-time parsing)
// builder.Services.AddHostedService<Ae.Rail.Services.MvRefreshService>();
builder.Services.AddSingleton<Ae.Rail.Services.ITiplocLookup, Ae.Rail.Services.TiplocLookup>();
builder.Services.AddSingleton<Ae.Rail.Services.IStationCodeLookup, Ae.Rail.Services.StationCodeLookup>();
builder.Services.AddSingleton<Ae.Rail.Services.IStationFinder, Ae.Rail.Services.StationFinder>();

// Configure forwarded headers to read X-Forwarded-For from proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownNetworks.Clear();
	options.KnownProxies.Clear();
});

// Configure rate limiting by IP address
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	
	// Add a fixed window rate limiter for IP addresses
	options.AddFixedWindowLimiter("fixed", limiterOptions =>
	{
		limiterOptions.PermitLimit = 100; // 100 requests
		limiterOptions.Window = TimeSpan.FromMinutes(1); // per minute
		limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		limiterOptions.QueueLimit = 0; // No queue, reject immediately when limit is exceeded
	});
	
	// Global rate limiter that partitions by IP address
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
	{
		// Get the client IP address (respects X-Forwarded-For)
		var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		
		return RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: ipAddress,
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 100, // 100 requests
				Window = TimeSpan.FromMinutes(1), // per minute
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = 0
			});
	});
});

// Register National Rail API Client
builder.Services.AddHttpClient<Ae.Rail.Services.INationalRailApiClient, Ae.Rail.Services.NationalRailApiClient>((sp, client) =>
{
	var config = sp.GetRequiredService<IConfiguration>();
	var baseUrl = config.GetValue<string>("NationalRail:BaseUrl") ?? throw new InvalidOperationException("NationalRail:BaseUrl configuration is required");
	var apiKey = config.GetValue<string>("NationalRail:ApiKey") ?? throw new InvalidOperationException("NationalRail:ApiKey configuration is required");
	
	client.BaseAddress = new Uri(baseUrl);
	client.Timeout = TimeSpan.FromSeconds(30);
	client.DefaultRequestHeaders.Add("x-apikey", apiKey);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<Ae.Rail.Data.PostgresDbContext>();
	db.Database.Migrate();
}

// IMPORTANT: UseForwardedHeaders must be called before other middleware
app.UseForwardedHeaders();

// Configure middleware conditionally based on port
var mainPort = builder.Configuration.GetValue<int?>("Ports:Main") ?? 8080;
var backofficePort = builder.Configuration.GetValue<int?>("Ports:Backoffice") ?? 8081;

app.UseWhen(
	context => context.Connection.LocalPort == mainPort,
	appBuilder =>
	{
		// Apply rate limiting only to main API
		appBuilder.UseRateLimiter();
	}
);

app.UseStaticFiles();

app.MapControllers();

app.Logger.LogInformation("Main API listening on port {MainPort}", mainPort);
app.Logger.LogInformation("Backoffice API listening on port {BackofficePort}", backofficePort);

app.Run();
