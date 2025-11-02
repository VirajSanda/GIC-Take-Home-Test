using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Events.Interfaces;

namespace Shared.Events.Services
{
    public class KafkaProducerService : IKafkaProducerService
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;

        public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService> logger)
        {
            var kafkaConfig = new ProducerConfig
            {
                BootstrapServers = config["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
            };
            _producer = new ProducerBuilder<string, string>(kafkaConfig).Build();
            _logger = logger;
        }

        /// <summary>
        /// Publishes a message to the specified Kafka topic.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="topic"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task PublishAsync<T>(string topic, T message)
        {
            try
            {
                var payload = JsonSerializer.Serialize(message);
                await _producer.ProduceAsync(
                    topic,
                    new Message<string, string> { Key = Guid.NewGuid().ToString(), Value = payload }
                );
                _logger.LogInformation("Published to Kafka topic {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing Kafka message");
            }
        }
    }
}
