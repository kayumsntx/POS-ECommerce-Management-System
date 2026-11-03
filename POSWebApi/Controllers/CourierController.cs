using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSWebApi.Models;
using POSWebApi.Services;
using POSWebApi.DTOs;

namespace POSWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourierController : ControllerBase
    {
        private readonly SteadfastService _steadfastService;
        private readonly AppDbContext _dbContext;

        public CourierController(SteadfastService steadfastService, AppDbContext dbContext)
        {
            _steadfastService = steadfastService;
            _dbContext = dbContext;
        }

        [HttpPost("send-to-courier")]
        public async Task<IActionResult> SendToCourier([FromBody] CourierDeliveryDto request)
        {
            if (request == null) return BadRequest(new { success = false, message = "Invalid Payload" });

            try
            {
                var orderRequest = new SteadfastOrderRequest(
                    request.invoice,
                    request.recipient_name,
                    request.recipient_phone,
                    request.recipient_address,
                    request.amount_to_collect,
                    request.note
                );

                var jsonResponse = await _steadfastService.CreateOrderAsync(orderRequest);

                string trackingId = "";
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("consignment", out JsonElement consignmentElement))
                    {
                        trackingId = consignmentElement.GetProperty("consignment_id").ToString();
                    }
                }

                // Declare 'order' outside the if-block so it can be accessed safely
                Order? order = null;

                if (int.TryParse(request.invoice, out int orderId))
                {
                    order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);

                    if (order != null && !string.IsNullOrEmpty(trackingId))
                    {
                        order.CourierTrackingId = trackingId;

                        // Set the status to Shipped (Assuming OrderStatus is an enum in your project)
                        // If "SHIPPED" is not in your enum, change this to OrderStatus.Approved or your equivalent
                        order.Status = OrderStatus.Shipped;

                        await _dbContext.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true, tracking_id = trackingId, raw_data = jsonResponse });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}