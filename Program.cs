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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<Ae.Rail.Data.PostgresDbContext>();
	db.Database.Migrate();
}

app.UseStaticFiles();

app.MapControllers();
app.Run();
