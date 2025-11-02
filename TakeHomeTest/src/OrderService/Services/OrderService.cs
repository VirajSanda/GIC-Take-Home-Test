using Microsoft.EntityFrameworkCore;
using OrderServiceAPI.Interfaces;
using OrderServiceAPI.Models;
using Shared.Events;
using Shared.Events.Interfaces;
using Shared.Models;
using UserServiceAPI.Data;

namespace OrderServiceAPI.Services
{
    public class OrderService : IOrderService
    {
        private readonly ILogger<OrderService> _logger;
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly AppDbContext _db;

        public OrderService(
            ILogger<OrderService> logger,
            IKafkaProducerService kafkaProducer,
            AppDbContext db
        )
        {
            _logger = logger;
            _kafkaProducer = kafkaProducer;
            _db = db;
        }

        /// <summary>
        /// Creates a new order based on the provided request.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<ServiceResult<Order>> CreateAsync(CreateOrderRequest request)
        {
            try
            {
                if (request == null)
                    return new ServiceResult<Order>
                    {
                        Success = false,
                        ErrorMessage = "Request cannot be null",
                    };

                if (request.UserId == Guid.Empty)
                    return new ServiceResult<Order>
                    {
                        Success = false,
                        ErrorMessage = "UserId is required",
                    };

                var order = new Order
                {
                    UserId = request.UserId,
                    Product = request.Product,
                    Quantity = request.Quantity,
                    Price = request.Price,
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Creating order {OrderId} for user {UserId}",
                    order.Id,
                    order.UserId
                );

                await _kafkaProducer.PublishAsync(
                    "order-created",
                    new OrderCreatedEvent
                    {
                        Id = order.Id,
                        UserId = order.UserId,
                        Product = order.Product,
                        Quantity = order.Quantity,
                        Price = order.Price,
                    }
                );

                _logger.LogInformation(
                    "Order {OrderId} created and event published to Kafka",
                    order.Id
                );

                return new ServiceResult<Order> { Success = true, Data = order };
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating user");
                return new ServiceResult<Order>
                {
                    Success = false,
                    ErrorMessage = "Database error occurred while creating user",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating user");
                return new ServiceResult<Order>
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred",
                };
            }
        }

        /// <summary>
        /// Gets an order by its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Order?> GetByIdAsync(Guid id)
        {
            // In-memory mock
            _logger.LogInformation("Fetching user {UserId}", id);
            return await _db.Orders.FindAsync(id);
        }

        /// <summary>
        /// Gets all orders.
        /// </summary>
        /// <returns></returns>
        public async Task<List<Order>> GetOrders()
        {
            return await _db.Orders.ToListAsync();
        }

        /// <summary>
        /// Gets all users.
        /// </summary>
        /// <returns></returns>
        public async Task<List<User>> GetUsers()
        {
            return await _db.Users.ToListAsync();
        }
    }
}
