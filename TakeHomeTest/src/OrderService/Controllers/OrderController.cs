using Microsoft.AspNetCore.Mvc;
using OrderServiceAPI.Interfaces;
using OrderServiceAPI.Models;

namespace OrderServiceAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        /// <summary>
        /// Gets an order by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Order>> GetByIdAsync(Guid id)
        {
            var order = await _orderService.GetByIdAsync(id);
            if (order == null)
                return NotFound();

            return Ok(order);
        }

        /// <summary>
        /// Gets all orders.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersAsync()
        {
            var orders = await _orderService.GetOrders();
            if (orders == null || !orders.Any())
                return NotFound("No orders found");
            return Ok(orders);
        }

        /// <summary>
        /// Creates a new order.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Order>> CreateAsync([FromBody] CreateOrderRequest request)
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

            var result = await _orderService.CreateAsync(request);
            if (!result.Success)
            {
                _logger.LogWarning("Order creation failed: {Error}", result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("Order created successfully: {OrderId}", result.Data!.Id);
            return result.Data!;
        }
    }
}
