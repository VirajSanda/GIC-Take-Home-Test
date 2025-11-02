using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using UserServiceAPI.Data;
using UserServiceAPI.Models;

namespace UserServiceAPI.Consumers
{
    public class OrderCreatedConsumer : BackgroundService
    {
        private readonly ILogger<OrderCreatedConsumer> _logger;
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public OrderCreatedConsumer(
            ILogger<OrderCreatedConsumer> logger,
            IConfiguration config,
            IServiceScopeFactory scopeFactory,
            IHostApplicationLifetime hostApplicationLifetime
        )
        {
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait until the application has fully started
            await WaitForAppStartup(_hostApplicationLifetime, stoppingToken);

            string bootstrapServers = _config["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092";
            string topic = _config["KAFKA_CONSUME_TOPICS"] ?? "order-created";
            string groupId = _config["KAFKA_GROUP_ID"] ?? "user-service-group";

            // Wait for Kafka to be available with retries
            await WaitForKafka(bootstrapServers, stoppingToken);

            var conf = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = true,
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(conf).Build();
            consumer.Subscribe(topic);

            _logger.LogInformation(
                "UserService consuming topic: {Topic} from {BootstrapServers}",
                topic,
                bootstrapServers
            );

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(stoppingToken);

                        if (result?.Message?.Value == null)
                        {
                            _logger.LogWarning("Received null message from Kafka");
                            continue;
                        }

                        _logger.LogInformation(
                            "Received order message: {Message}",
                            result.Message.Value
                        );

                        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(
                            result.Message.Value,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (orderEvent == null)
                        {
                            _logger.LogError("Failed to deserialize OrderCreatedEvent");
                            continue;
                        }

                        await ProcessOrderEvent(orderEvent, stoppingToken);

                        await Task.Delay(1000, stoppingToken);

                        // Commit the offset to mark message as processed
                        consumer.Commit(result);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming order message from Kafka");
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing order JSON message");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error processing order event");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Order consumer stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in order Kafka consumer");
            }
            finally
            {
                consumer.Close();
            }
        }

        /// <summary>
        /// Processes the received OrderCreatedEvent by checking user existence and saving the order.
        /// </summary>
        /// <param name="orderEvent"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ProcessOrderEvent(
            OrderCreatedEvent orderEvent,
            CancellationToken cancellationToken
        )
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Check if user exists
            var userExists = await dbContext.Users.AnyAsync(
                u => u.Id == orderEvent.UserId,
                cancellationToken
            );

            if (!userExists)
            {
                _logger.LogWarning("Received order for unknown user {UserId}", orderEvent.UserId);
                return;
            }

            // Check if order already exists to avoid duplicates
            var existingOrder = await dbContext.Orders.FirstOrDefaultAsync(
                o => o.OrderId == orderEvent.Id,
                cancellationToken
            );

            if (existingOrder != null)
            {
                _logger.LogInformation(
                    "Order {OrderId} already exists in UserService",
                    orderEvent.OrderId
                );
                return;
            }

            // Add order to UserService's database
            var userOrder = new Order
            {
                OrderId = orderEvent.Id,
                UserId = orderEvent.UserId,
                Product = orderEvent.Product,
                Quantity = orderEvent.Quantity,
                Price = orderEvent.Price,
            };

            dbContext.Orders.Add(userOrder);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderId} saved for User {UserId} - {Product} (Qty: {Quantity})",
                orderEvent.OrderId,
                orderEvent.UserId,
                orderEvent.Product,
                orderEvent.Quantity
            );
        }

        /// <summary>
        /// Waits for Kafka to become available with retries.
        /// </summary>
        /// <param name="bootstrapServers"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async Task WaitForKafka(string bootstrapServers, CancellationToken stoppingToken)
        {
            const int maxRetries = 10;
            const int delayMs = 3000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var adminClient = new AdminClientBuilder(
                        new AdminClientConfig { BootstrapServers = bootstrapServers }
                    ).Build();

                    var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                    _logger.LogInformation("✅ Kafka is now available");
                    return;
                }
                catch (KafkaException ex)
                {
                    _logger.LogWarning(
                        "Kafka not ready yet (attempt {Attempt}/{MaxRetries}): {Message}",
                        i + 1,
                        maxRetries,
                        ex.Message
                    );

                    if (i == maxRetries - 1)
                    {
                        _logger.LogError(
                            "Kafka failed to become available after {MaxRetries} attempts",
                            maxRetries
                        );
                    }

                    await Task.Delay(delayMs, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Waits for the application to fully start before proceeding.
        /// </summary>
        /// <param name="hostApplicationLifetime"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private static async Task WaitForAppStartup(
            IHostApplicationLifetime hostApplicationLifetime,
            CancellationToken stoppingToken
        )
        {
            var startedSource = new TaskCompletionSource();
            await using var registration = hostApplicationLifetime.ApplicationStarted.Register(() =>
                startedSource.SetResult()
            );

            await startedSource.Task;
            await Task.Delay(2000, stoppingToken); // Additional safety delay
        }
    }
}
