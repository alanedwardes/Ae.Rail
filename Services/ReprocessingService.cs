using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Services
{
	public sealed class ReprocessingService : IReprocessingService
	{
		private readonly PostgresDbContext _dbContext;
		private readonly ITrainDataParser _parser;
		private readonly ILogger<ReprocessingService> _logger;

		public ReprocessingService(
			PostgresDbContext dbContext,
			ITrainDataParser parser,
			ILogger<ReprocessingService> logger)
		{
			_dbContext = dbContext;
			_parser = parser;
			_logger = logger;
		}

		public async Task<ReprocessingResult> ReprocessMessagesAsync(
			DateTime? startTime = null, 
			DateTime? endTime = null, 
			CancellationToken cancellationToken = default)
		{
			var result = new ReprocessingResult
			{
				StartTime = startTime,
				EndTime = endTime
			};

			var startTimeUtc = DateTime.UtcNow;

			try
			{
				_logger.LogInformation(
					"Starting reprocessing of message_envelopes (StartTime: {StartTime}, EndTime: {EndTime})",
					startTime?.ToString("o") ?? "null",
					endTime?.ToString("o") ?? "null");

				var batchSize = 1000;
				var saveBatchSize = 1000;

				// Build query with optional time filters
				var query = _dbContext.MessageEnvelopes.AsQueryable();
				
				if (startTime.HasValue)
				{
					query = query.Where(e => e.ReceivedAt >= startTime.Value);
				}
				
				if (endTime.HasValue)
				{
					query = query.Where(e => e.ReceivedAt <= endTime.Value);
				}

				// Get total count
				result.TotalCount = await query.CountAsync(cancellationToken);
				_logger.LogInformation(
					"Reprocessing {TotalCount} messages (fetching in batches of {BatchSize}, saving every {SaveBatchSize})",
					result.TotalCount, batchSize, saveBatchSize);

				if (result.TotalCount == 0)
				{
					_logger.LogInformation("No messages to reprocess");
					result.Duration = DateTime.UtcNow - startTimeUtc;
					return result;
				}

				long lastId = 0;
				int pendingSaves = 0;

				while (!cancellationToken.IsCancellationRequested)
				{
					// Build batch query
					var batchQuery = query.Where(e => e.Id > lastId).OrderBy(e => e.Id);
					
					// Fetch batch
					var batch = await batchQuery
						.Take(batchSize)
						.Select(e => new { e.Id, e.Payload })
						.ToListAsync(cancellationToken);

					if (batch.Count == 0)
						break;

					// Process batch
					foreach (var envelope in batch)
					{
						try
						{
							// Parse WITHOUT auto-saving (accumulate in DbContext)
							var success = await _parser.ParseAndSaveAsync(envelope.Payload, cancellationToken);
							if (success)
								result.SuccessCount++;
							else
								result.ErrorCount++;
							pendingSaves++;
						}
						catch (Exception ex)
						{
							_logger.LogWarning(ex, "Failed to parse message ID {Id}", envelope.Id);
							result.ErrorCount++;
						}

						lastId = envelope.Id;
						result.ProcessedCount++;

						// Save every saveBatchSize messages
						if (pendingSaves >= saveBatchSize)
						{
							await _dbContext.SaveChangesAsync(cancellationToken);
							pendingSaves = 0;
						}
					}

					// Log progress every 10k records
					if (result.ProcessedCount % 10000 == 0)
					{
						var progress = (double)result.ProcessedCount / result.TotalCount * 100;
						_logger.LogInformation(
							"Progress {ProcessedCount}/{TotalCount} ({Progress:F1}%), {SuccessCount} successful, {ErrorCount} errors",
							result.ProcessedCount, result.TotalCount, progress, result.SuccessCount, result.ErrorCount);
					}
				}

				// Final save for any remaining changes
				if (pendingSaves > 0)
				{
					await _dbContext.SaveChangesAsync(cancellationToken);
				}

				result.Duration = DateTime.UtcNow - startTimeUtc;

				_logger.LogInformation(
					"Reprocessing completed in {ElapsedSeconds:F1}s - processed {ProcessedCount}, successful {SuccessCount}, errors {ErrorCount}",
					result.Duration.TotalSeconds, result.ProcessedCount, result.SuccessCount, result.ErrorCount);

				return result;
			}
			catch (Exception ex)
			{
				result.Duration = DateTime.UtcNow - startTimeUtc;
				_logger.LogError(ex, "Failed to reprocess messages");
				throw;
			}
		}
	}
}

