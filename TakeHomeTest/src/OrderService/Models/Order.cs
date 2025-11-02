using Shared.Models;

namespace OrderServiceAPI.Models
{
    public class Order : BaseModel
    {
        public Guid UserId { get; set; }
        public string Product { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class CreateOrderRequest
    {
        public Guid UserId { get; set; }
        public string Product { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
