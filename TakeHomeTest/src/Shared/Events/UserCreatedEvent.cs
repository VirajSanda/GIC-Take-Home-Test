using Shared.Models;

namespace Shared.Events
{
    public class UserCreatedEvent : BaseModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
