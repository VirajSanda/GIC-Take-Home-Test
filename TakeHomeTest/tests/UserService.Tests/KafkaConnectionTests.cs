using Confluent.Kafka;

namespace UserService.Tests
{
    public class KafkaConnectionTests
    {
        private readonly string _bootstrapServers = "kafka:9092"; // internal Docker name
        private readonly string _testTopic = "connectivity-test";

        [Fact(
            DisplayName = "✅ Should connect to Kafka and produce a message successfully",
            Skip = "Integration test - requires Kafka broker at 'kafka:9092'"
        )]
        public async Task Should_Connect_To_Kafka_And_Produce_Message()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.Leader,
                MessageTimeoutMs = 5000,
            };

            try
            {
                using var producer = new ProducerBuilder<Null, string>(config).Build();
                var dr = await producer.ProduceAsync(
                    _testTopic,
                    new Message<Null, string> { Value = "Kafka connection test message" }
                );

                Assert.NotNull(dr);
                Assert.Equal(_testTopic, dr.Topic);
            }
            catch (ProduceException<Null, string> ex)
            {
                Assert.False(true, $"❌ Kafka produce failed: {ex.Error.Reason}");
            }
        }

        [Fact(
            DisplayName = "✅ Should connect to Kafka as a consumer",
            Skip = "Integration test - requires Kafka broker at 'kafka:9092'"
        )]
        public void Should_Connect_To_Kafka_As_Consumer()
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "kafka-connection-test-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
            };

            try
            {
                using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
                consumer.Subscribe(_testTopic);
                consumer.Close();
            }
            catch (Exception ex)
            {
                Assert.False(true, $"❌ Kafka consumer connection failed: {ex.Message}");
            }
        }
    }
}
