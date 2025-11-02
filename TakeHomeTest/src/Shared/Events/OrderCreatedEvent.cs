using Shared.Models;

namespace Shared.Events
{
    public class OrderCreatedEvent : BaseModel
    {
        public Guid UserId { get; set; }
        public Guid OrderId { get; set; }
        public string Product { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
