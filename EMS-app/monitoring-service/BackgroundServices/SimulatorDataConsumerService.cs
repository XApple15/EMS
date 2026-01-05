using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using monitoring_service.Infrastructure.Messaging;
using monitoring_service.Model;
using monitoring_service.Data;

namespace monitoring_service.BackgroundServices
{
    /// <summary>
    /// Background service that consumes SimulatorData events from RabbitMQ ingest queue
    /// and aggregates them hourly in-memory, flushing hourly totals to the DB.
    ///
    /// Consumes from per-replica ingest queue (e.g., ingest-queue-1)
    /// </summary>
    public class SimulatorDataConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventConsumer _eventConsumer;
        private readonly ILogger<SimulatorDataConsumerService> _logger;
        private readonly IEventPublisher _eventPublisher;
        private readonly RabbitMqSettings _settings;

        private ConcurrentDictionary<Guid, double> _currentHourSums = new();
        private DateTime _currentHourStartUtc;
        private readonly object _hourLock = new();

        private readonly ConcurrentDictionary<Guid, bool> _deviceExistenceCache = new();

        public SimulatorDataConsumerService(
            IServiceProvider serviceProvider,
            IEventConsumer eventConsumer,
            ILogger<SimulatorDataConsumerService> logger,
            IEventPublisher _publisher,
            Microsoft.Extensions.Options.IOptions<RabbitMqSettings> settings)
        {
            _serviceProvider = serviceProvider;
            _eventConsumer = eventConsumer;
            _logger = logger;
            _eventPublisher = _publisher;
            _settings = settings.Value;

            _currentHourStartUtc = TruncateToHour(DateTime.UtcNow);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Determine ingest queue name based on replica ID
            var ingestQueueName = string.Format(_settings.IngestQueuePattern, _settings.ReplicaId);
            
            _logger.LogInformation(
                "SimulatorDataConsumerService starting for replica {ReplicaId}, consuming from queue: {QueueName}",
                _settings.ReplicaId, ingestQueueName);

            try
            {
                var aggregatorTask = Task.Run(() => HourlyAggregatorLoopAsync(stoppingToken), stoppingToken);

                // Consume from the per-replica ingest queue
                await _eventConsumer.StartConsumingFromQueueAsync<SimulatorDataEvent>(
                    queueName: ingestQueueName,
                    handler: HandleSimulatorDataEventAsync,
                    cancellationToken: stoppingToken);

                await aggregatorTask;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in SimulatorDataConsumerService");
            }
        }

      
        private async Task<bool> HandleSimulatorDataEventAsync(SimulatorDataEvent @event)
        {
            try
            {
                var deviceGuid = @event.DeviceId;
                
                if (deviceGuid == Guid.Empty)
                    return false;

                
                if (!_deviceExistenceCache.TryGetValue(deviceGuid, out var exists))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MonitorDbUtils>();

                    exists = await db.Devices.AnyAsync(d => d.Id == deviceGuid);
                    _deviceExistenceCache[deviceGuid] = exists;
                }

                if (!exists)
                {
                    _logger.LogWarning("Received SimulatorDataEvent for unknown DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                        @event.DeviceId, @event.CorrelationId);
                    return true;
                }

                var timestamp = @event.Timestamp != default ? @event.Timestamp : DateTime.UtcNow;
                var eventHour = TruncateToHour(timestamp);

                if (eventHour != _currentHourStartUtc)
                {
                    lock (_hourLock)
                    {
                        if (eventHour != _currentHourStartUtc)
                        {
                            var toFlush = _currentHourSums;
                            var hourToFlush = _currentHourStartUtc;

                            _currentHourSums = new ConcurrentDictionary<Guid, double>();
                            _currentHourStartUtc = eventHour;

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await FlushSumsToDatabaseAsync(toFlush, hourToFlush);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error flushing hourly sums for hour {Hour}", hourToFlush);
                                }
                            });
                        }
                    }
                }
                var energy = @event.ConsumptionValue;

                _currentHourSums.AddOrUpdate(deviceGuid, energy, (_, old) => old + energy);

                _logger.LogInformation("Accumulated reading: DeviceId={DeviceId}, Added={Energy}, HourStartUtc={Hour}",
                    deviceGuid, energy, _currentHourStartUtc);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process SimulatorData event: DeviceId={DeviceId}, CorrelationId={CorrelationId}",
                    @event.DeviceId, @event.CorrelationId);

                return false;
            }
        }

       

        private async Task HourlyAggregatorLoopAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var nextTopOfHour = TruncateToHour(now).AddHours(1);
                    var delay = nextTopOfHour - now;

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, stoppingToken);
                    }

                    ConcurrentDictionary<Guid, double> toFlush;
                    DateTime hourToFlush;

                    lock (_hourLock)
                    {
                        hourToFlush = _currentHourStartUtc;
                        toFlush = _currentHourSums;
                        _currentHourSums = new ConcurrentDictionary<Guid, double>();
                        _currentHourStartUtc = TruncateToHour(DateTime.UtcNow);
                    }

                    try
                    {
                        await FlushSumsToDatabaseAsync(toFlush, hourToFlush);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing hourly sums for hour {Hour}", hourToFlush);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await FinalFlushOnShutdownAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in HourlyAggregatorLoopAsync");
            }
        }


        private async Task FlushSumsToDatabaseAsync(ConcurrentDictionary<Guid, double> sumsSnapshot, DateTime hourStartUtc)
        {
            if (sumsSnapshot == null || sumsSnapshot.IsEmpty)
            {
                _logger.LogInformation("No readings to flush for hour {Hour}", hourStartUtc);
                return;
            }

            _logger.LogInformation("Flushing {Count} device totals for hour {Hour}", sumsSnapshot.Count, hourStartUtc);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MonitorDbUtils>();
            using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                foreach (var kv in sumsSnapshot)
                {
                    var deviceId = kv.Key;
                    var totalEnergy = kv.Value;

                    var deviceModel = await db.Devices.FindAsync(deviceId);
                    if (deviceModel !=null)
                    {
                        var deviceConsumption = deviceModel.Consumption;
                        var deviceUserId = deviceModel.UserId;
                        var deviceConsumptionDouble = Convert.ToDouble(deviceConsumption);
                        if (totalEnergy > deviceConsumptionDouble)
                        {
                            await _eventPublisher.PublishAsync(new Notification
                            {
                                UserId = deviceUserId.ToString(),
                                Title = "Consumption Limit Exceeded",
                                Message = $"Device {deviceId} exceeded consumption limit of {deviceConsumption} with total {totalEnergy} at {hourStartUtc}.",
                                Type = "Warning",
                                Timestamp = DateTime.UtcNow
                            }, "user_notifications");
                        }
                    }

                    var consumption = new Consumption
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceId,
                        Timestamp = hourStartUtc,
                        ConsumptionValue = totalEnergy
                    };

                    db.Add(consumption);
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("Successfully flushed {Count} device totals for hour {Hour}", sumsSnapshot.Count, hourStartUtc);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task FinalFlushOnShutdownAsync()
        {
            ConcurrentDictionary<Guid, double> toFlush;
            DateTime hourToFlush;

            lock (_hourLock)
            {
                toFlush = _currentHourSums;
                hourToFlush = _currentHourStartUtc;
                _currentHourSums = new ConcurrentDictionary<Guid, double>();
            }

            try
            {
                await FlushSumsToDatabaseAsync(toFlush, hourToFlush);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing final flush on shutdown for hour {Hour}", hourToFlush);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SimulatorDataConsumerService stopping...");
            return base.StopAsync(cancellationToken);
        }

        private static DateTime TruncateToHour(DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);
        }
    }
}