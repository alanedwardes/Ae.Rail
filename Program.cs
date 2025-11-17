using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHostedService<Ae.Rail.Services.TrainsConsumerService>();
builder.Services.AddHostedService<Ae.Rail.Services.MvRefreshService>();
builder.Services.AddSingleton<Ae.Rail.Services.ITiplocLookup, Ae.Rail.Services.TiplocLookup>();
builder.Services.AddSingleton<Ae.Rail.Services.IStationCodeLookup, Ae.Rail.Services.StationCodeLookup>();

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

app.UseStaticFiles();

app.MapControllers();
app.Run();
