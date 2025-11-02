using Confluent.Kafka;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Shared.Extensions;
using UserServiceAPI.Consumers;
using UserServiceAPI.Data;
using UserServiceAPI.Interfaces;
using UserServiceAPI.Services;
using UserServiceAPI.Utils;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configuration for Kafka - default fallback
builder.Configuration.AddEnvironmentVariables();
var kafkaBootstrap = builder.Configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "localhost:9092";
var kafkaTopic = builder.Configuration["KAFKA_TOPIC"] ?? "user-created";

// EF Core InMemory
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("UserDb"));

// Add controllers (if you use controllers) or minimal endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UserService API", Version = "v1" });
});

// Confluent.Kafka producer as a singleton IProducer<string, string>
var producerConfig = new ProducerConfig { BootstrapServers = kafkaBootstrap };
builder.Services.AddSingleton(producerConfig);
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<ProducerConfig>();
    return new ProducerBuilder<string, string>(cfg).Build();
});

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddKafkaProducer();

builder.Services.AddHostedService<OrderCreatedConsumer>();

builder
    .Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("Database")
    .AddKafka(producerConfig, name: "Kafka");

var app = builder.Build();

// Ensure Kafka topic exists before starting background consumers
await KafkaTopicInitializer.EnsureTopicExistsAsync(kafkaBootstrap, kafkaTopic);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "UserService API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapControllers();
app.UseAuthorization();

// Health check endpoint
app.MapHealthChecks(
    "/health",
    new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse }
);

app.Run();
