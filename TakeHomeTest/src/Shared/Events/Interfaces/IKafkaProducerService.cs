namespace Shared.Events.Interfaces
{
    public interface IKafkaProducerService
    {
        Task PublishAsync<T>(string topic, T message);
    }
}
