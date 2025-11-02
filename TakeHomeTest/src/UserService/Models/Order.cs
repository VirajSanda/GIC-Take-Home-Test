using Shared.Models;

namespace UserServiceAPI.Models
{
    public class Order : BaseModel
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal Price { get; set; }
        public string Product { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
