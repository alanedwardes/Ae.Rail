using System;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Data;
using Ae.Rail.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Services
{
	public sealed class MvRefreshService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<MvRefreshService> _logger;
		private readonly IConfiguration _configuration;

		public MvRefreshService(IServiceProvider serviceProvider, ILogger<MvRefreshService> logger, IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_configuration = configuration;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var intervalSeconds = Math.Max(5, _configuration.GetValue<int?>("MaterializedViewRefresh:Seconds") ?? 30);
			DateTime lastMaxReceivedAt = DateTime.MinValue;

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using var scope = _serviceProvider.CreateScope();
					var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

					// Gate refresh: only if new data since last run
					var maxReceivedAt = await db.Set<MessageEnvelope>()
						.MaxAsync(e => (DateTime?)e.ReceivedAt, stoppingToken) ?? DateTime.MinValue;

					if (maxReceivedAt > lastMaxReceivedAt)
					{
					try
					{
						await db.Database.ExecuteSqlRawAsync("refresh materialized view trainservice_v1;", stoppingToken);
						_logger.LogDebug("Refreshed materialized view trainservice_v1 at {TimeUtc}", DateTime.UtcNow);

						try
						{
							await db.Database.ExecuteSqlRawAsync("refresh materialized view vehicle_v1;", stoppingToken);
							_logger.LogDebug("Refreshed materialized view vehicle_v1 at {TimeUtc}", DateTime.UtcNow);
						}
						catch (Exception ex2)
						{
							_logger.LogWarning(ex2, "Failed to refresh materialized view vehicle_v1 (will retry)");
						}

						try
						{
							await db.Database.ExecuteSqlRawAsync("refresh materialized view service_vehicle_v1;", stoppingToken);
							_logger.LogDebug("Refreshed materialized view service_vehicle_v1 at {TimeUtc}", DateTime.UtcNow);
						}
						catch (Exception ex3)
						{
							_logger.LogWarning(ex3, "Failed to refresh materialized view service_vehicle_v1 (will retry)");
						}
						lastMaxReceivedAt = maxReceivedAt;
					}
						catch (Exception ex)
						{
							_logger.LogWarning(ex, "Failed to refresh materialized view trainservice_v1 (will retry)");
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogDebug(ex, "MV refresher loop error (non-fatal)");
				}

				try
				{
					await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
				}
				catch (TaskCanceledException) { }
			}
		}
	}
}


