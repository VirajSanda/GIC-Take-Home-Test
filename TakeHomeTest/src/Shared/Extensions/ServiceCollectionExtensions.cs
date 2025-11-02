using Microsoft.Extensions.DependencyInjection;
using Shared.Events.Interfaces;
using Shared.Events.Services;

namespace Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKafkaProducer(this IServiceCollection services)
        {
            services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
            return services;
        }
    }
}
