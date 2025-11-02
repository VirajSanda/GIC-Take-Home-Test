using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace OrderServiceAPI.Utils
{
    public static class KafkaTopicInitializer
    {
        /// <summary>
        /// Ensures that a Kafka topic exists; creates it if it does not.
        /// </summary>
        /// <param name="bootstrapServers"></param>
        /// <param name="topicName"></param>
        /// <param name="numPartitions"></param>
        /// <param name="replicationFactor"></param>
        /// <returns></returns>
        public static async Task EnsureTopicExistsAsync(
            string bootstrapServers,
            string topicName,
            int numPartitions = 1,
            short replicationFactor = 1
        )
        {
            var config = new AdminClientConfig { BootstrapServers = bootstrapServers };

            using var adminClient = new AdminClientBuilder(config).Build();

            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                bool topicExists = metadata.Topics.Any(t =>
                    t.Topic == topicName && t.Error.Code == ErrorCode.NoError
                );

                if (topicExists)
                {
                    Console.WriteLine($"✅ Kafka topic '{topicName}' already exists.");
                    return;
                }

                await adminClient.CreateTopicsAsync(
                    [
                        new TopicSpecification
                        {
                            Name = topicName,
                            NumPartitions = numPartitions,
                            ReplicationFactor = replicationFactor,
                        },
                    ]
                );

                Console.WriteLine($"Kafka topic '{topicName}' created successfully.");
            }
            catch (CreateTopicsException e)
            {
                if (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                    Console.WriteLine(
                        $"Topic '{topicName}' already exists (CreateTopicsException)."
                    );
                else
                    Console.WriteLine(
                        $"Failed to create topic '{topicName}': {e.Results[0].Error.Reason}"
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Kafka topic creation error: {ex.Message}");
            }
        }
    }
}
