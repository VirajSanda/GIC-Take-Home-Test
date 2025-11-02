namespace Shared.Test
{
    // These are integration tests which require a Kafka broker available.
    // They run only when the environment variable ENABLE_KAFKA_INTEGRATION_TESTS=true is present.
    public class KafkaIntegrationTests
    {
        private readonly string _bootstrapServers;
        private readonly string _testTopic = "connectivity-test";
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;

        private string GetBootstrapServers() =>
            Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")
            ?? Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")
            ?? "localhost:29092";

        public KafkaIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            _bootstrapServers = GetBootstrapServers();
            _output.WriteLine($"Using Kafka bootstrap servers: {_bootstrapServers}");
        }

        private static bool IsEnabled() =>
            string.Equals(
                Environment.GetEnvironmentVariable("ENABLE_KAFKA_INTEGRATION_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase
            );

        [Fact(DisplayName = "Integration: Kafka producer connectivity")]
        public async Task KafkaProducerConnectivity_WhenEnabled()
        {
            if (!IsEnabled())
            {
                _output.WriteLine(
                    "Kafka integration tests are disabled. Set ENABLE_KAFKA_INTEGRATION_TESTS=true to run."
                );
                return;
            }

            try
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = _bootstrapServers,
                    Acks = Acks.Leader,
                    MessageTimeoutMs = 5000,
                };

                using var producer = new ProducerBuilder<Null, string>(config).Build();
                var dr = await producer.ProduceAsync(
                    _testTopic,
                    new Message<Null, string> { Value = "integration-test" }
                );
                Assert.NotNull(dr);
                Assert.Equal(_testTopic, dr.Topic);
                _output.WriteLine($"Message delivered to {dr.TopicPartitionOffset}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error connecting to Kafka: {ex.Message}");
                throw;
            }
        }

        [Fact(DisplayName = "Integration: Kafka consumer connectivity")]
        public void KafkaConsumerConnectivity_WhenEnabled()
        {
            if (!IsEnabled())
            {
                _output.WriteLine(
                    "Kafka integration tests are disabled. Set ENABLE_KAFKA_INTEGRATION_TESTS=true to run."
                );
                return;
            }

            try
            {
                var config = new ConsumerConfig
                {
                    BootstrapServers = _bootstrapServers,
                    GroupId = "integration-test-group",
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false,
                };

                using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
                consumer.Subscribe(_testTopic);
                _output.WriteLine("Successfully connected to Kafka consumer");
                consumer.Close();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error connecting to Kafka consumer: {ex.Message}");
                throw;
            }
        }
    }
}
