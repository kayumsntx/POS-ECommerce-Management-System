using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSWebApi.Models;
using POSWebApi.Services;
using System.Security.Claims;

namespace POSWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TrackingController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SteadfastService _steadfastService;
        private readonly ILogger<TrackingController> _logger;

        public TrackingController(AppDbContext db, SteadfastService steadfastService, ILogger<TrackingController> logger)
        {
            _db = db;
            _steadfastService = steadfastService;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> TrackOrder(int id, CancellationToken ct)
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.DeliveryAddress)
                .FirstOrDefaultAsync(o => o.OrderId == id, ct);

            if (order == null)
                return NotFound(new { success = false, message = $"Order {id} not found." });

            var isStaff = User.IsInRole("Admin") || User.IsInRole("POSOperator") || User.IsInRole("Salesman");

            if (!isStaff)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                if (!int.TryParse(userIdStr, out int callerId) || order.UserId != callerId)
                {
                    return Forbid();
                }
            }

            string currentStatus = order.Status.ToString();
            string courierStatusMessage = "No tracking info available yet.";

            if (!string.IsNullOrEmpty(order.CourierTrackingId))
            {
                try
                {
                    var liveCourierStatus = await _steadfastService.GetTrackingStatusAsync(order.CourierTrackingId);

                    if (!string.IsNullOrEmpty(liveCourierStatus))
                    {
                        currentStatus = liveCourierStatus.ToUpper();
                        courierStatusMessage = $"Live update from courier: {currentStatus}";

                        if (currentStatus == "DELIVERED" && order.Status != OrderStatus.Delivered)
                        {
                            order.Status = OrderStatus.Delivered;
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch live tracking status for Order {OrderId}", id);
                }
            }

            var response = new
            {
                OrderId = order.OrderId,
                InvoiceId = $"SALE-{order.OrderId}",
                OrderDate = order.OrderDate,
                CustomerName = order.DeliveryAddress?.RecipientName ?? "Customer",
                Status = currentStatus,
                CourierTrackingId = order.CourierTrackingId,
                TrackingMessage = courierStatusMessage,
                TotalAmount = order.TotalAmount,
                Items = order.OrderItems.Select(oi => new
                {
                    ProductName = oi.Product?.ProductName,
                    UnitPrice = oi.UnitPrice,
                    Quantity = oi.Quantity,
                    Subtotal = oi.Subtotal
                })
            };

            return Ok(new { success = true, data = response });
        }
    }
}