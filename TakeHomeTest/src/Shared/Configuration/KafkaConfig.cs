namespace Shared.Configuration
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string ConsumeTopics { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
