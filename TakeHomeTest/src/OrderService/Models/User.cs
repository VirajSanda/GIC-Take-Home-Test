using Shared.Models;

namespace OrderServiceAPI.Models
{
    public class User : BaseModel
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
