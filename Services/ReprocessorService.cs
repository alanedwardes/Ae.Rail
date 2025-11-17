using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Services
{
	/// <summary>
	/// Background service that reprocesses message_envelopes on startup to populate train_services, vehicles, and service_vehicles tables.
	/// </summary>
	public sealed class ReprocessorService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<ReprocessorService> _logger;
		private readonly IConfiguration _configuration;

		public ReprocessorService(IServiceProvider serviceProvider, ILogger<ReprocessorService> logger, IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_configuration = configuration;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Small delay to let app fully initialize
			await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

			try
			{
				if (!await ShouldReprocess(stoppingToken))
				{
					_logger.LogInformation("Reprocessor: No reprocessing needed");
					return;
				}

				_logger.LogInformation("Reprocessor: Starting reprocessing of message_envelopes...");
				var startTime = DateTime.UtcNow;

				await ReprocessAllMessages(stoppingToken);

				var elapsed = DateTime.UtcNow - startTime;
				_logger.LogInformation("Reprocessor: Completed in {ElapsedSeconds:F1}s", elapsed.TotalSeconds);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Reprocessor: Failed to reprocess messages");
			}
		}

	private async Task<bool> ShouldReprocess(CancellationToken cancellationToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

		var config = _configuration.GetSection("Reprocessor");

		// Check if forced - DEFAULT TRUE
		var forceReprocess = config.GetValue<bool?>("ForceReprocess") ?? true;
		_logger.LogInformation("Reprocessor config: ForceReprocess={ForceReprocess}", forceReprocess);
		
		if (forceReprocess)
		{
			_logger.LogInformation("Reprocessor: Force reprocess is enabled");
			return true;
		}

		// Check if reprocess on startup is disabled
		var runOnStartup = config.GetValue<bool?>("RunOnStartup");
		if (runOnStartup == false)
		{
			_logger.LogInformation("Reprocessor: RunOnStartup is disabled");
			return false;
		}

		// Auto-detect if tables are empty
		var reprocessIfEmpty = config.GetValue<bool?>("ReprocessIfEmpty") ?? true;
		if (reprocessIfEmpty)
		{
			var hasTrainServices = await dbContext.Set<Ae.Rail.Models.TrainService>().AnyAsync(cancellationToken);
			if (!hasTrainServices)
			{
				_logger.LogInformation("Reprocessor: Train services table is empty, will reprocess");
				return true;
			}
			else
			{
				_logger.LogInformation("Reprocessor: Train services table has data, skipping");
			}
		}

		return false;
	}

		private async Task ReprocessAllMessages(CancellationToken stoppingToken)
		{
			using var scope = _serviceProvider.CreateScope();
			var dbContext = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
			var parser = scope.ServiceProvider.GetRequiredService<ITrainDataParser>();

			var batchSize = _configuration.GetValue<int?>("Reprocessor:BatchSize") ?? 1000;

			// Get total count
			var totalCount = await dbContext.MessageEnvelopes.CountAsync(stoppingToken);
			_logger.LogInformation("Reprocessor: Processing {TotalCount} messages in batches of {BatchSize}", totalCount, batchSize);

			long processedCount = 0;
			long successCount = 0;
			long lastId = 0;

			while (!stoppingToken.IsCancellationRequested)
			{
				// Fetch batch
				var batch = await dbContext.MessageEnvelopes
					.Where(e => e.Id > lastId)
					.OrderBy(e => e.Id)
					.Take(batchSize)
					.Select(e => new { e.Id, e.Payload })
					.ToListAsync(stoppingToken);

				if (batch.Count == 0)
					break;

				// Process batch
				foreach (var envelope in batch)
				{
					try
					{
						var success = await parser.ParseAndSaveAsync(envelope.Payload, stoppingToken);
						if (success)
							successCount++;
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Reprocessor: Failed to parse message ID {Id}", envelope.Id);
					}

					lastId = envelope.Id;
					processedCount++;
				}

				// Log progress every 10k records
				if (processedCount % 10000 == 0)
				{
					var progress = (double)processedCount / totalCount * 100;
					_logger.LogInformation("Reprocessor: Progress {ProcessedCount}/{TotalCount} ({Progress:F1}%), {SuccessCount} successful",
						processedCount, totalCount, progress, successCount);
				}
			}

			_logger.LogInformation("Reprocessor: Final count - processed {ProcessedCount}, successful {SuccessCount}",
				processedCount, successCount);
		}
	}
}

