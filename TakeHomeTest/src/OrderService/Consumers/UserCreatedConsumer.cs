using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using OrderServiceAPI.Models;
using Shared.Events;
using UserServiceAPI.Data;

namespace OrderServiceAPI.Consumers
{
    public class UserCreatedConsumer : BackgroundService
    {
        private readonly ILogger<UserCreatedConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public UserCreatedConsumer(
            ILogger<UserCreatedConsumer> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            IHostApplicationLifetime hostApplicationLifetime
        )
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        /// <summary>
        /// Executes the Kafka consumer to process UserCreatedEvent messages.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait until the application has fully started
            await WaitForAppStartup(_hostApplicationLifetime, stoppingToken);

            string bootstrapServers = _config["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092";
            string topic = _config["KAFKA_CONSUME_TOPICS"] ?? "user-created";
            string groupId = _config["KAFKA_GROUP_ID"] ?? "order-service-group";

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
                "OrderService consuming topic {Topic} from {BootstrapServers}",
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

                        _logger.LogInformation("Received message: {Message}", result.Message.Value);

                        var userEvent = JsonSerializer.Deserialize<UserCreatedEvent>(
                            result.Message.Value,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (userEvent == null)
                        {
                            _logger.LogError("Failed to deserialize UserCreatedEvent");
                            continue;
                        }

                        await ProcessUserEvent(userEvent, stoppingToken);

                        await Task.Delay(1000, stoppingToken);

                        // Commit the offset to mark message as processed
                        consumer.Commit(result);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message from Kafka");
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing JSON message");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in Kafka consumer");
            }
            finally
            {
                consumer.Close();
            }
        }

        /// <summary>
        /// Processes the received UserCreatedEvent by checking user existence and saving the user.
        /// </summary>
        /// <param name="userEvent"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ProcessUserEvent(
            UserCreatedEvent userEvent,
            CancellationToken cancellationToken
        )
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Check if user already exists in cache
            var existingUser = await dbContext.Users.FirstOrDefaultAsync(
                u => u.Name == userEvent.Name && u.Email == userEvent.Email,
                cancellationToken
            );

            if (existingUser != null)
            {
                _logger.LogInformation("User {UserId} already saved in OrderService", userEvent.Id);
                return;
            }

            // Add user to cache
            var user = new User
            {
                UserId = userEvent.Id,
                Name = userEvent.Name,
                Email = userEvent.Email,
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "User {UserId} saved in OrderService: {UserName}",
                userEvent.Id,
                userEvent.Name
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
                    _logger.LogInformation("Kafka is now available");
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
