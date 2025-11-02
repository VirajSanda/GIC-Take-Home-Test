using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Events.Interfaces;
using UserServiceAPI.Data;
using UserServiceAPI.Models;
using UserSvc = UserServiceAPI.Services.UserService;

namespace UserService.Tests
{
    public class UserServiceTests
    {
        private static DbContextOptions<AppDbContext> CreateOptions(string dbName)
        {
            return new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        }

        [Fact]
        public async Task CreateAsync_Success_PublishesEventAndSavesUser()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());

            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var req = new CreateUserRequest { Name = "Alice", Email = "alice@example.com" };
                var result = await service.CreateAsync(req);

                Assert.True(result.Success);
                Assert.NotNull(result.Data);

                // Assert user saved
                var saved = await db.Users.FirstOrDefaultAsync(u => u.Email == "alice@example.com");
                Assert.NotNull(saved);

                // Verify publish called
                mockProducer.Verify(
                    p => p.PublishAsync("user-created", It.IsAny<object>()),
                    Times.Once
                );
            }
        }

        [Fact]
        public async Task CreateAsync_ValidationFailure_MissingNameOrEmail()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var req = new CreateUserRequest { Name = "", Email = "" };
                var result = await service.CreateAsync(req);

                Assert.False(result.Success);
                Assert.Contains(
                    "required",
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
        public async Task CreateAsync_DuplicateEmail_ReturnsError()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                db.Users.Add(new User { Name = "Existing", Email = "dupe@example.com" });
                await db.SaveChangesAsync();

                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var req = new CreateUserRequest { Name = "New", Email = "DuPe@Example.com" };
                var result = await service.CreateAsync(req);

                Assert.False(result.Success);
                Assert.Contains(
                    "already exists",
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
            )
            {
                throw new DbUpdateException("simulated db failure");
            }
        }

        [Fact]
        public async Task CreateAsync_DbUpdateException_ReturnsError()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new FaultyAppDbContext(options))
            {
                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var req = new CreateUserRequest { Name = "Bob", Email = "bob@example.com" };
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
        public async Task CreateAsync_PublishThrows_ReturnsUnexpectedError_ButUserSaved()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();
            mockProducer
                .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ThrowsAsync(new Exception("kafka failed"));

            using (var db = new AppDbContext(options))
            {
                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var req = new CreateUserRequest { Name = "Carol", Email = "carol@example.com" };
                var result = await service.CreateAsync(req);

                Assert.False(result.Success);
                Assert.Contains(
                    "unexpected",
                    result.ErrorMessage,
                    StringComparison.OrdinalIgnoreCase
                );

                // verify user was saved to DB even though publish failed
                var saved = await db.Users.FirstOrDefaultAsync(u => u.Email == "carol@example.com");
                Assert.NotNull(saved);

                mockProducer.Verify(
                    p => p.PublishAsync("user-created", It.IsAny<object>()),
                    Times.Once
                );
            }
        }

        [Fact]
        public async Task GetByIdAsync_FoundAndNotFound()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                var user = new User { Name = "Dave", Email = "dave@example.com" };
                db.Users.Add(user);
                await db.SaveChangesAsync();

                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var found = await service.GetByIdAsync(user.Id);
                Assert.NotNull(found);

                var notFound = await service.GetByIdAsync(Guid.NewGuid());
                Assert.Null(notFound);
            }
        }

        [Fact]
        public async Task GetOrdersAndGetUsers_ReturnsSeededLists()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var mockLogger = new Mock<ILogger<UserSvc>>();
            var mockProducer = new Mock<IKafkaProducerService>();

            using (var db = new AppDbContext(options))
            {
                db.Users.Add(new User { Name = "U1", Email = "u1@example.com" });
                db.Users.Add(new User { Name = "U2", Email = "u2@example.com" });
                db.Orders.Add(
                    new Order
                    {
                        OrderId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        Price = 10,
                        Product = "P1",
                        Quantity = 1,
                    }
                );
                db.Orders.Add(
                    new Order
                    {
                        OrderId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        Price = 20,
                        Product = "P2",
                        Quantity = 2,
                    }
                );
                await db.SaveChangesAsync();

                var service = new UserSvc(mockLogger.Object, mockProducer.Object, db);

                var users = await service.GetUsers();
                var orders = await service.GetOrders();

                Assert.Equal(2, users.Count);
                Assert.Equal(2, orders.Count);
            }
        }
    }
}
