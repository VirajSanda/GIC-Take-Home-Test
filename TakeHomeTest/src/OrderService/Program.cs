using Confluent.Kafka;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderServiceAPI.Consumers;
using OrderServiceAPI.Interfaces;
using OrderServiceAPI.Services;
using OrderServiceAPI.Utils;
using Shared.Extensions;
using UserServiceAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configuration for Kafka - default fallback
builder.Configuration.AddEnvironmentVariables();
var kafkaBootstrap = builder.Configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "localhost:9092";
var kafkaTopic = builder.Configuration["KAFKA_TOPIC"] ?? "order-created";

// EF Core InMemory
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("OrderDb"));

// Add controllers (if you use controllers) or minimal endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add CORS support
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
    );
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OrderService API", Version = "v1" });
});

// Confluent.Kafka producer as a singleton IProducer<string, string>
var producerConfig = new ProducerConfig { BootstrapServers = kafkaBootstrap };
builder.Services.AddSingleton(producerConfig);
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<ProducerConfig>();
    return new ProducerBuilder<string, string>(cfg).Build();
});

// DI
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddKafkaProducer();

builder.Services.AddHostedService<UserCreatedConsumer>();

builder
    .Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("Database")
    .AddKafka(producerConfig, name: "Kafka");

var app = builder.Build();

// Use CORS
app.UseCors("AllowAll");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OrderService API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapControllers();
app.UseAuthorization();

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse }
);

// Start listening first
await app.StartAsync();

// Then ensure Kafka topic exists (this way the health endpoint is available)
await KafkaTopicInitializer.EnsureTopicExistsAsync(kafkaBootstrap, kafkaTopic);

await app.WaitForShutdownAsync();
