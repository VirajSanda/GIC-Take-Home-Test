using Microsoft.EntityFrameworkCore;
using Shared.Events.Interfaces;
using Shared.Models;
using UserServiceAPI.Data;
using UserServiceAPI.Interfaces;
using UserServiceAPI.Models;

namespace UserServiceAPI.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly AppDbContext _db;

        public UserService(
            ILogger<UserService> logger,
            IKafkaProducerService kafkaProducer,
            AppDbContext db
        )
        {
            _logger = logger;
            _kafkaProducer = kafkaProducer;
            _db = db;
        }

        /// <summary>
        /// Creates a new user based on the provided request.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<ServiceResult<User>> CreateAsync(CreateUserRequest request)
        {
            try
            {
                if (
                    string.IsNullOrWhiteSpace(request.Name)
                    || string.IsNullOrWhiteSpace(request.Email)
                )
                    return new ServiceResult<User>
                    {
                        Success = false,
                        ErrorMessage =
                            $"Name '{request.Name}' and Email '{request.Email}' are required",
                    };

                var existingUser = await _db.Users.FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == request.Email.ToLower().Trim()
                );
                if (existingUser != null)
                    return new ServiceResult<User>
                    {
                        Success = false,
                        ErrorMessage = $"User already exists with the Email '{request.Email}'",
                    };

                var user = new User
                {
                    Name = request.Name.Trim(),
                    Email = request.Email.ToLower().Trim(),
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "User created successfully: {UserId} - {Email}",
                    user.Id,
                    user.Email
                );

                // Publish event to Kafka
                await _kafkaProducer.PublishAsync(
                    "user-created",
                    new
                    {
                        user.Id,
                        user.Name,
                        user.Email,
                    }
                );

                _logger.LogInformation("User {UserId} created and event published", user.Id);

                return new ServiceResult<User> { Success = true, Data = user };
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating user");
                return new ServiceResult<User>
                {
                    Success = false,
                    ErrorMessage = "Database error occurred while creating user",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating user");
                return new ServiceResult<User>
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred",
                };
            }
        }

        /// <summary>
        /// Gets a user by its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<User?> GetByIdAsync(Guid id)
        {
            _logger.LogInformation("Fetching user {UserId}", id);
            return await _db.Users.FindAsync(id);
        }

        /// <summary>
        /// Gets all orders.
        /// </summary>
        /// <returns></returns>
        public async Task<List<Order>> GetOrders()
        {
            return await _db.Orders.AsNoTracking().ToListAsync();
        }

        /// <summary>
        /// Gets all users.
        /// </summary>
        /// <returns></returns>
        public async Task<List<User>> GetUsers()
        {
            return await _db.Users.AsNoTracking().ToListAsync();
        }
    }
}
