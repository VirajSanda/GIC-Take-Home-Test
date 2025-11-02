using Shared.Models;
using UserServiceAPI.Models;

namespace UserServiceAPI.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<ServiceResult<User>> CreateAsync(CreateUserRequest request);
        Task<List<User>> GetUsers();
        Task<List<Order>> GetOrders();
    }
}
