using System.ComponentModel.DataAnnotations;
using Shared.Models;

namespace UserServiceAPI.Models
{
    public class User : BaseModel
    {
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }

    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(
            100,
            MinimumLength = 2,
            ErrorMessage = "Name must be between 2 and 100 characters"
        )]
        public string Name { get; set; } = default!;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = default!;
    }
}
