using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderServiceAPI.Models;
using Shared.Events.Interfaces;
using UserServiceAPI.Data;
using OrderSvc = OrderServiceAPI.Services.OrderService;

namespace OrderService.Tests
{
    public class OrderServiceTests
    {
        private static DbContextOptions<AppDbContext> CreateOptions(string dbName)
        {
            return new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        }

        [Fact]
        public async Task CreateAsync_Success_PublishesEventAndSavesOrder()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);

                var req = new CreateOrderRequest
                {
                    UserId = Guid.NewGuid(),
                    Product = "P",
                    Quantity = 1,
                    Price = 9.99m,
                };
                var result = await service.CreateAsync(req);

                Assert.True(result.Success);
                Assert.NotNull(result.Data);

                var saved = await db.Orders.FirstOrDefaultAsync(o => o.Product == "P");
                Assert.NotNull(saved);

                mockProducer.Verify(
                    p => p.PublishAsync("order-created", It.IsAny<object>()),
                    Times.Once
                );
            }
        }

        [Fact]
        public async Task CreateAsync_NullRequest_ReturnsError()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);
                var result = await service.CreateAsync(null!);
                Assert.False(result.Success);
                Assert.Contains(
                    "Request cannot be null",
                    result.ErrorMessage,
                    StringComparison.OrdinalIgnoreCase
                );
                mockProducer.Verify(
                    p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()),
                    Times.Never
                );
            }
        }

        [Fact]
        public async Task CreateAsync_MissingUserId_ReturnsError()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);
                var req = new CreateOrderRequest
                {
                    UserId = Guid.Empty,
                    Product = "P",
                    Quantity = 1,
                    Price = 1,
                };
                var result = await service.CreateAsync(req);
                Assert.False(result.Success);
                Assert.Contains(
                    "UserId is required",
                    result.ErrorMessage,
                    StringComparison.OrdinalIgnoreCase
                );
                mockProducer.Verify(
                    p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()),
                    Times.Never
                );
            }
        }

        private class FaultyAppDbContext : AppDbContext
        {
            public FaultyAppDbContext(DbContextOptions<AppDbContext> options)
                : base(options) { }

            public override Task<int> SaveChangesAsync(
                CancellationToken cancellationToken = default
            ) => throw new DbUpdateException("simulated db failure");
        }

        [Fact]
        public async Task CreateAsync_DbUpdateException_ReturnsError()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new FaultyAppDbContext(options))
            {
                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);
                var req = new CreateOrderRequest
                {
                    UserId = Guid.NewGuid(),
                    Product = "P",
                    Quantity = 1,
                    Price = 1,
                };
                var result = await service.CreateAsync(req);
                Assert.False(result.Success);
                Assert.Contains(
                    "Database error",
                    result.ErrorMessage,
                    StringComparison.OrdinalIgnoreCase
                );
                mockProducer.Verify(
                    p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()),
                    Times.Never
                );
            }
        }

        [Fact]
        public async Task CreateAsync_PublishThrows_ReturnsUnexpectedError_ButOrderSaved()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();
            mockProducer
                .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ThrowsAsync(new Exception("kafka fail"));

            using (var db = new AppDbContext(options))
            {
                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);
                var req = new CreateOrderRequest
                {
                    UserId = Guid.NewGuid(),
                    Product = "Q",
                    Quantity = 2,
                    Price = 5,
                };
                var result = await service.CreateAsync(req);
                Assert.False(result.Success);
                Assert.Contains(
                    "unexpected",
                    result.ErrorMessage,
                    StringComparison.OrdinalIgnoreCase
                );

                var saved = await db.Orders.FirstOrDefaultAsync(o => o.Product == "Q");
                Assert.NotNull(saved);
                mockProducer.Verify(
                    p => p.PublishAsync("order-created", It.IsAny<object>()),
                    Times.Once
                );
            }
        }

        [Fact]
        public async Task GetByIdAsync_FoundAndNotFound()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var order = new Order
                {
                    UserId = Guid.NewGuid(),
                    Product = "Z",
                    Quantity = 1,
                    Price = 1,
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync();

                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);
                var found = await service.GetByIdAsync(order.Id);
                Assert.NotNull(found);
                var notFound = await service.GetByIdAsync(Guid.NewGuid());
                Assert.Null(notFound);
            }
        }

        [Fact]
        public async Task GetOrdersAndGetUsers_ReturnsSeededLists()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<OrderSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                db.Users.Add(new User { Name = "OU1", Email = "ou1@example.com" });
                db.Orders.Add(
                    new Order
                    {
                        UserId = Guid.NewGuid(),
                        Product = "P1",
                        Quantity = 1,
                        Price = 1,
                    }
                );
                await db.SaveChangesAsync();

                var service = new OrderSvc(mockLogger.Object, mockProducer.Object, db);
                var users = await service.GetUsers();
                var orders = await service.GetOrders();
                Assert.NotEmpty(users);
                Assert.NotEmpty(orders);
            }
        }
    }
}
