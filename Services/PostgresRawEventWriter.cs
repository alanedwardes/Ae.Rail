using Ae.Rail.Data;
using Ae.Rail.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Models.TafTsi;

namespace Ae.Rail.Services
{
	public interface IPostgresRawEventWriter
	{
		Task WriteAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken);
	}

	public sealed class PostgresRawEventWriter : IPostgresRawEventWriter
	{
		private readonly PostgresDbContext _dbContext;
		private readonly ILogger<PostgresRawEventWriter> _logger;

		public PostgresRawEventWriter(PostgresDbContext dbContext, ILogger<PostgresRawEventWriter> logger)
		{
			_dbContext = dbContext;
			_logger = logger;
		}

		public async Task WriteAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
		{
			if (result == null || result.Message == null || string.IsNullOrWhiteSpace(result.Message.Value))
			{
				return;
			}

			// Persist JSON when possible; otherwise convert TAFTSI XML to JSON; otherwise wrap raw
			JsonDocument payload;
			var isTafTsi = result.Message.Value.IndexOf("<PassengerTrainConsistMessage", StringComparison.OrdinalIgnoreCase) >= 0;

			if (TryParseJson(result.Message.Value, out payload))
			{
				// ok
			}
			else if (TryConvertTafTsiXmlToJson(result.Message.Value, out payload))
			{
				// converted
			}
			else
			{
				// fallback: wrap raw content so it's still valid jsonb
				var fallback = new { format = "raw", content = result.Message.Value };
				payload = JsonDocument.Parse(JsonSerializer.Serialize(fallback));

				if (isTafTsi)
				{
					_logger.LogWarning("Failed to convert TAFTSI XML to JSON; storing raw wrapper. Topic={Topic}, Partition={Partition}, Offset={Offset}",
						result.Topic, result.Partition.Value, result.Offset.Value);
				}
				else
				{
					_logger.LogDebug("Non-JSON payload stored as raw wrapper. Topic={Topic}, Partition={Partition}, Offset={Offset}",
						result.Topic, result.Partition.Value, result.Offset.Value);
				}
			}

			var envelope = new MessageEnvelope
			{
				ReceivedAt = DateTime.UtcNow,
				Payload = payload
			};

			await _dbContext.MessageEnvelopes.AddAsync(envelope, cancellationToken);
			try
			{
				await _dbContext.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateException ex)
			{
				_logger.LogError(ex, "Failed to save message envelope to Postgres. Topic={Topic}, Partition={Partition}, Offset={Offset}",
					result.Topic, result.Partition.Value, result.Offset.Value);
				// Swallow to avoid blocking commits; consumer will still commit later
			}
		}

		private static bool TryParseJson(string value, out JsonDocument doc)
		{
			try
			{
				doc = JsonDocument.Parse(value);
				return true;
			}
			catch
			{
				doc = null;
				return false;
			}
		}

		private static bool TryConvertTafTsiXmlToJson(string value, out JsonDocument doc)
		{
			doc = null;
			try
			{
				if (string.IsNullOrWhiteSpace(value) || !value.Contains("<PassengerTrainConsistMessage", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}

				var serializer = new XmlSerializer(typeof(PassengerTrainConsistMessage));
				using var reader = new StringReader(value);
				var dto = (PassengerTrainConsistMessage)serializer.Deserialize(reader);
				if (dto == null)
				{
					return false;
				}

				var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
				{
					DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
					WriteIndented = false
				});
				doc = JsonDocument.Parse(json);
				return true;
			}
			catch
			{
				doc = null;
				return false;
			}
		}
	}
}

