using Ae.Rail.Data;
using Ae.Rail.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Xml.Serialization;
using Ae.Rail.Models.TafTsi;
using System.IO;
using System.Linq.Expressions;

namespace Ae.Rail.Services
{
    public class TrainsConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrainsConsumerService> _logger;
        private readonly IConfiguration _configuration;
        private IConsumer<string, string> _consumer;
        private Task _consumerLoopTask;
        private long _processedCount;
        private DateTime _lastSummaryLogUtc = DateTime.UtcNow;
        private const int SummaryLogEveryCount = 100;
        private static readonly TimeSpan SummaryLogEveryTime = TimeSpan.FromSeconds(30);

        public TrainsConsumerService(
            IServiceProvider serviceProvider,
            ILogger<TrainsConsumerService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Kafka consumer service starting");

            // Run the blocking consume loop on a dedicated thread so host startup (Kestrel) is not delayed.
            _consumerLoopTask = Task.Factory.StartNew(
                () => RunConsumerLoopAsync(stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();

            return _consumerLoopTask;
        }

        private async Task RunConsumerLoopAsync(CancellationToken stoppingToken)
        {
            var trainsConfig = _configuration.GetSection("trains");
            var bootstrapServers = trainsConfig.GetValue<string>("bootstrapServers");
            var username = trainsConfig.GetValue<string>("username");
            var password = trainsConfig.GetValue<string>("password");
            var certificatePath = trainsConfig.GetValue<string>("certificatePath");
            var topic = trainsConfig.GetValue<string>("topic");
            var consumerGroup = trainsConfig.GetValue<string>("consumerGroup");

            if (string.IsNullOrEmpty(bootstrapServers) || string.IsNullOrEmpty(topic))
            {
                _logger.LogError("Kafka configuration is missing. Please configure bootstrapServers and topic in config.json");
                return;
            }

            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = consumerGroup ?? "default-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = username,
                SaslPassword = password,
            };

            if (!string.IsNullOrEmpty(certificatePath))
            {
                config.SslCaLocation = certificatePath;
            }

            try
            {
                _consumer = new ConsumerBuilder<string, string>(config)
                    .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Error}", e.Reason))
                    .SetPartitionsAssignedHandler((_, partitions) => _logger.LogInformation("Kafka partitions assigned: {Partitions}", string.Join(",", partitions)))
                    .SetPartitionsRevokedHandler((_, partitions) => _logger.LogInformation("Kafka partitions revoked: {Partitions}", string.Join(",", partitions)))
                    .SetLogHandler((_, m) => _logger.LogDebug("Kafka log: {Message}", m.Message))
                    .Build();

                _consumer.Subscribe(topic);
                _logger.LogInformation("Subscribed to topic: {Topic}", topic);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = _consumer.Consume(stoppingToken);

                        if (result != null && result.Message != null)
                        {
                            await ProcessMessageAsync(result, stoppingToken);

                            _processedCount++;
                            if (_processedCount % SummaryLogEveryCount == 0 || (DateTime.UtcNow - _lastSummaryLogUtc) > SummaryLogEveryTime)
                            {
                                _lastSummaryLogUtc = DateTime.UtcNow;
                                _logger.LogInformation("Kafka consumed {ProcessedCount} messages. Last at {TopicPartitionOffset}", _processedCount, result.TopicPartitionOffset);
                            }
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message from Kafka");
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error in Kafka consumer");
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka consumer");
            }
            finally
            {
                _consumer?.Close();
                _consumer?.Dispose();
                _logger.LogInformation("Kafka consumer service stopped");
            }
        }

        private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
			var rawWriter = scope.ServiceProvider.GetRequiredService<IPostgresRawEventWriter>();
            var parser = scope.ServiceProvider.GetRequiredService<ITrainDataParser>();
            var dbContext = scope.ServiceProvider.GetRequiredService<Ae.Rail.Data.PostgresDbContext>();

            try
            {
			// Write raw Kafka message to Postgres (audit trail)
			await rawWriter.WriteAsync(result, cancellationToken);

                // Parse and write to structured tables (real-time)
                try
                {
                    await parser.ParseAndSaveAsync(result.Message.Value, cancellationToken);
                    // Save immediately for real-time processing
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "Failed to parse message for structured tables (will continue): topic {Topic}, partition {Partition}, offset {Offset}",
                        result.Topic, result.Partition, result.Offset);
                    // Continue processing - raw message is saved, parsing can be retried via reprocessor
                }

                // Commit the offset after successfully saving/upserting to database
                try
                {
                    _consumer.Commit(result);
                }
                catch (KafkaException ex)
                {
                    _logger.LogWarning(ex, "Kafka commit failed for {TopicPartitionOffset}", result.TopicPartitionOffset);
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Database error while saving message (may be duplicate): topic {Topic}, partition {Partition}, offset {Offset}",
                    result.Topic, result.Partition, result.Offset);
                // Still commit to avoid reprocessing
                try
                {
                    _consumer.Commit(result);
                }
                catch (KafkaException commitEx)
                {
                    _logger.LogWarning(commitEx, "Kafka commit failed after DB error for {TopicPartitionOffset}", result.TopicPartitionOffset);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from topic {Topic}, partition {Partition}, offset {Offset}",
                    result.Topic, result.Partition, result.Offset);
                // Don't commit on error so message can be reprocessed
                throw;
            }
        }

        public override void Dispose()
        {
            _consumer?.Close();
            _consumer?.Dispose();
            base.Dispose();
        }

        private static PassengerTrainConsistMessage DeserializePassengerTrainConsistMessage(string xml)
        {
            var serializer = new XmlSerializer(typeof(PassengerTrainConsistMessage));
            using var reader = new StringReader(xml);
            return (PassengerTrainConsistMessage)serializer.Deserialize(reader);
        }
    }
}

