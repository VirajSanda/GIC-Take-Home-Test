using OrderServiceAPI.Models;
using Shared.Models;

namespace OrderServiceAPI.Interfaces
{
    public interface IOrderService
    {
        Task<Order?> GetByIdAsync(Guid id);
        Task<ServiceResult<Order>> CreateAsync(CreateOrderRequest request);
        Task<List<User>> GetUsers();
        Task<List<Order>> GetOrders();
    }
}
