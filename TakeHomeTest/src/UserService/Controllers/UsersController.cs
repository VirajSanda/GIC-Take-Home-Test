using Microsoft.AspNetCore.Mvc;
using UserServiceAPI.Data;
using UserServiceAPI.Interfaces;
using UserServiceAPI.Models;

namespace UserServiceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new user.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<User>> Create([FromBody] CreateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new { errors });
            }
            var result = await _userService.CreateAsync(request);

            if (!result.Success)
            {
                _logger.LogWarning("User creation failed: {Error}", result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("User created successfully: {UserId}", result.Data!.Id);
            return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
        }

        /// <summary>
        /// Gets a user by ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<User>> GetById(Guid id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        /// <summary>
        /// Gets all users.
        /// </summary>
        /// <returns></returns>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsersAsync()
        {
            var user = await _userService.GetUsers();
            if (user.Count == 0)
                return NotFound("No cached users found");
            return Ok(user);
        }

        /// <summary>
        /// Gets all orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet("existing-orders")]
        public async Task<IActionResult> GetOrdersAsync()
        {
            var order = await _userService.GetOrders();
            if (order.Count == 0)
                return NotFound("No cached users found");
            return Ok(order);
        }
    }
}
