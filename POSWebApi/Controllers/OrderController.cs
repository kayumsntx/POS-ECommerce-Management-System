using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using POSWebApi.DTOs;
using POSWebApi.Models;
using POSWebApi.Services;
using System.Globalization;
using System.Security.Claims;

namespace POSWebApi.Controllers
{
    [Route("api/[controller]")]   
    [ApiController]
    [Authorize]          
    public class OrderController : ControllerBase
    {
      
        private const decimal OnlineShippingFee = 70m;

        private const string UserRole = "User";

        private readonly AppDbContext              _db;
        private readonly UserManager<AppUser>      _userManager;
        private readonly ISslCommerzService        _sslCommerz;
        private readonly ILogger<OrderController>  _logger;

        public OrderController(
            AppDbContext db,
            UserManager<AppUser> userManager,
            ISslCommerzService sslCommerz,
            ILogger<OrderController> logger)
        {
            _db          = db;
            _userManager = userManager;
            _sslCommerz  = sslCommerz;
            _logger      = logger;
        }

        
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto, CancellationToken ct)
        {
           
            if (!TryGetCallerId(out int userId))
                return Unauthorized(new { message = "Unable to identify the authenticated user." });

            var roles = GetCallerRoles();
            if (roles.Count == 0)
                return Forbid();   

            
            bool isOnline = IsOnlineUser();
            var orderStatus = isOnline ? OrderStatus.Pending    
                                       : OrderStatus.Approved;

           
            if (!Enum.IsDefined(dto.PaymentMethod))
                return BadRequest(new { message = "PaymentMethod must be 1 (Cash) or 2 (Online)." });

            if (isOnline)
            {
                if (dto.DeliveryAddress == null)
                    return BadRequest(new { message = "A delivery address is required for online orders." });

                if (string.IsNullOrWhiteSpace(dto.DeliveryAddress.RecipientName) ||
                    string.IsNullOrWhiteSpace(dto.DeliveryAddress.Phone)          ||
                    string.IsNullOrWhiteSpace(dto.DeliveryAddress.Address))
                {
                    return BadRequest(new { message = "Delivery address must include RecipientName, Phone, and Address." });
                }

                if (dto.DiscountPercent != 0)
                    return BadRequest(new { message = "Discounts can only be applied to POS orders." });
            }

            if (dto.DiscountPercent < 0 || dto.DiscountPercent > 100)
                return BadRequest(new { message = "DiscountPercent must be between 0 and 100." });

            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest(new { message = "An order must contain at least one item." });

            var duplicateProductIds = dto.Items
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateProductIds.Any())
                return BadRequest(new
                {
                    message = $"Duplicate product IDs found: {string.Join(", ", duplicateProductIds)}. " +
                              "Merge quantities for the same product into one line item."
                });

          
            int paymentMethodId = (int)dto.PaymentMethod;
            if (!await _db.PaymentMethods.AnyAsync(pm => pm.PaymentMethodId == paymentMethodId, ct))
            {
                _logger.LogError("PaymentMethods row {Id} ({Method}) is missing.", paymentMethodId, dto.PaymentMethod);
                return StatusCode(500, new { message = $"Payment method '{dto.PaymentMethod}' is not configured on the server." });
            }

     
            var productIds = dto.Items.Select(i => i.ProductId).ToList();

            var products = await _db.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync(ct);

            var inventories = await _db.Inventories
                .Where(inv => productIds.Contains(inv.ProductId))
                .ToListAsync(ct);

            var missingIds = productIds.Except(products.Select(p => p.ProductId)).ToList();
            if (missingIds.Any())
                return BadRequest(new { message = $"Product(s) not found: {string.Join(", ", missingIds)}." });

            var stockErrors = new List<string>();
            foreach (var item in dto.Items)
            {
                var available = inventories
                    .Where(inv => inv.ProductId == item.ProductId)
                    .Sum(inv => inv.Quantity);

                if (item.Quantity > available)
                    stockErrors.Add($"Product ID {item.ProductId}: requested {item.Quantity}, available {available}.");
            }

            if (stockErrors.Any())
                return BadRequest(new
                {
                    message = "Insufficient stock for one or more products.",
                    details = stockErrors
                });

            // ── 5. Build order items & calculate totals ───────────────────────
            var orderItems = new List<OrderItem>();
            decimal totalPrice = 0m;

            foreach (var itemDto in dto.Items)
            {
                var product   = products.First(p => p.ProductId == itemDto.ProductId);
                var unitPrice = product.Price;            
                var subtotal  = unitPrice * itemDto.Quantity;

                totalPrice += subtotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.ProductId,
                    Quantity  = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    Discount  = 0m,
                    Subtotal  = subtotal
                });
            }

            decimal discountAmount = Math.Round(totalPrice * (dto.DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
            decimal shipping       = isOnline ? OnlineShippingFee : 0m;
            decimal payable        = totalPrice - discountAmount + shipping;

            // ── 6. Persist order (+ payment) atomically ───────────────────────
            var order = new Order
            {
                UserId          = userId,
                Phone           = dto.Phone,
                IsOnlineOrder   = isOnline,
                Status          = orderStatus,
                OrderDate       = DateTime.UtcNow,
                TotalAmount     = totalPrice,
                DiscountPercent = dto.DiscountPercent,
                DiscountAmount  = discountAmount,
                ShippingAmount  = shipping,
                PayableAmount   = payable,
                PaymentMethod   = dto.PaymentMethod,
                PaymentStatus   = PaymentStatus.Unpaid,
                OrderItems      = orderItems
            };

            if (isOnline && dto.DeliveryAddress != null)
            {
                order.DeliveryAddress = new DeliveryAddress
                {
                    RecipientName = dto.DeliveryAddress.RecipientName,
                    Phone         = dto.DeliveryAddress.Phone,
                    Address       = dto.DeliveryAddress.Address
                };
            }

            _db.Orders.Add(order);
            DeductStock(dto.Items, inventories);

            string? gatewayUrl = null;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await _db.SaveChangesAsync(ct);  

                if (dto.PaymentMethod == PaymentMethodType.Cash)
                {
                    
                    _db.Payments.Add(new Payment
                    {
                        OrderId         = order.OrderId,
                        PaymentMethodId = paymentMethodId,
                        Amount          = payable,
                        TransactionId   = null,
                        PaymentDate     = DateTime.UtcNow
                    });
                    order.PaymentStatus = PaymentStatus.Paid;
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                   
                    order.PaymentTransactionId = $"ORD{order.OrderId}-{Guid.NewGuid():N}";
                    await _db.SaveChangesAsync(ct);

                    var user = await _userManager.FindByIdAsync(userId.ToString());
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var session = await _sslCommerz.CreateSessionAsync(new SslCommerzSessionRequest
                    {
                        TransactionId    = order.PaymentTransactionId,
                        TotalAmount      = payable,
                        CustomerName     = order.DeliveryAddress?.RecipientName ?? user?.UserName ?? "Customer",
                        CustomerEmail    = user?.Email ?? "customer@example.com",
                        CustomerPhone    = order.DeliveryAddress?.Phone ?? dto.Phone ?? user?.PhoneNumber ?? "01700000000",
                        CustomerAddress  = order.DeliveryAddress?.Address ?? "N/A",
                        ProductName      = $"Order #{order.OrderId}",
                        RequiresShipping = isOnline,
                        CallbackBaseUrl  = baseUrl
                    }, ct);

                    if (!session.IsSuccess)
                    {
                       
                        await tx.RollbackAsync(ct);
                        return StatusCode(StatusCodes.Status502BadGateway, new
                        {
                            message = "Could not start the online payment. The order was not created.",
                            detail  = session.Error
                        });
                    }

                    gatewayUrl = session.GatewayPageUrl;
                }

                await tx.CommitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order for user {UserId}", userId);
                await tx.RollbackAsync(CancellationToken.None);
                return StatusCode(500, new { message = "An error occurred while creating the order." });
            }

          
            var response = MapToResponse(order);
            response.GatewayPageURL = gatewayUrl;

            return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, response);
        }

       
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            // Stored procedure: dbo.sp_GetOrderById (one row per order item)
            var rows = await _db.Database
                .SqlQueryRaw<OrderSpRow>("EXEC dbo.sp_GetOrderById @OrderId",
                    new SqlParameter("@OrderId", id))
                .ToListAsync();

            if (rows.Count == 0)
                return NotFound(new { message = $"Order {id} not found." });

            var head = rows[0];
            if (IsOnlineUser())
            {
                if (!TryGetCallerId(out int callerId) || head.UserId != callerId)
                    return Forbid();
            }

            return Ok(MapRowsToResponse(rows));
        }

      
      
   
        [HttpGet]
        [HttpGet("all")]
        [Authorize(Roles = "Admin,POSOperator,Cashier,Salesman")]
        public async Task<IActionResult> GetAllOrders()
        {
            // Stored procedure: dbo.sp_GetAllOrders
            var rows = await _db.Database
                .SqlQueryRaw<OrderSpRow>("EXEC dbo.sp_GetAllOrders")
                .ToListAsync();

            return Ok(GroupOrderRows(rows));
        }

       
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetOrdersByUserId(int userId)
        {
            // Stored procedure: dbo.sp_GetOrdersByUserId
            var rows = await _db.Database
                .SqlQueryRaw<OrderSpRow>("EXEC dbo.sp_GetOrdersByUserId @UserId",
                    new SqlParameter("@UserId", userId))
                .ToListAsync();

            return Ok(GroupOrderRows(rows));
        }

       
        [HttpGet("date-range")]
        //[Authorize(Roles = "User,Admin,POSOperator,Cashier,Salesman")]
        public async Task<IActionResult> GetOrdersByDateRange(
            [FromQuery] string startDate,
            [FromQuery] string endDate)
        {
            if (!TryParseDateOnly(startDate, out DateOnly start))
            {
                return BadRequest(new
                {
                    message = "Invalid startDate format. Use dd/MM/yyyy (e.g. 10/10/2026) or yyyy-MM-dd."
                });
            }

            if (!TryParseDateOnly(endDate, out DateOnly end))
            {
                return BadRequest(new
                {
                    message = "Invalid endDate format. Use dd/MM/yyyy (e.g. 15/10/2026) or yyyy-MM-dd."
                });
            }

            if (start > end)
                return BadRequest(new { message = "startDate must be earlier than or equal to endDate." });

            DateTime startInclusive = start.ToDateTime(TimeOnly.MinValue);        
            DateTime endExclusive   = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

            // Regular "User" callers only see their own orders; staff pass NULL to see all.
            int? userFilter = null;
            if (IsOnlineUser())
            {
                if (!TryGetCallerId(out int callerId))
                    return Unauthorized();
                userFilter = callerId;
            }

            // Stored procedure: dbo.sp_GetOrdersByDateRange
            var rows = await _db.Database
                .SqlQueryRaw<OrderSpRow>(
                    "EXEC dbo.sp_GetOrdersByDateRange @StartDate, @EndDate, @UserId",
                    new SqlParameter("@StartDate", System.Data.SqlDbType.DateTime2) { Value = startInclusive },
                    new SqlParameter("@EndDate",   System.Data.SqlDbType.DateTime2) { Value = endExclusive },
                    new SqlParameter("@UserId",    System.Data.SqlDbType.Int)       { Value = (object?)userFilter ?? DBNull.Value })
                .ToListAsync();

            return Ok(GroupOrderRows(rows));
        }

        
        [HttpPatch("{id:int}/approve")]
        [Authorize(Roles = "Admin,PosOperator,Salesman")]
        public async Task<IActionResult> ApproveOrder(int id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.DeliveryAddress)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound(new { message = $"Order {id} not found." });

            if (!order.IsOnlineOrder)
                return BadRequest(new
                {
                    message = "Only Online orders require approval. POS orders are approved at creation."
                });

            if (order.Status != OrderStatus.Pending)
                return BadRequest(new
                {
                    message = $"Order {id} cannot be approved because its current status is '{order.Status}'."
                });

            order.Status = OrderStatus.Approved;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Order {id} has been approved.",
                order   = MapToResponse(order)
            });
        }

       
        [HttpPatch("{id:int}/cancel")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound(new { message = $"Order {id} not found." });

            if (!User.IsInRole("Admin"))
            {
                if (!TryGetCallerId(out int callerId) || order.UserId != callerId)
                    return Forbid();
            }

            if (order.Status != OrderStatus.Pending)
                return BadRequest(new
                {
                    message = $"Order {id} cannot be cancelled because its current status is '{order.Status}'."
                });

            order.Status = OrderStatus.Cancelled;
            if (order.PaymentStatus == PaymentStatus.Unpaid)
                order.PaymentStatus = PaymentStatus.Cancelled;

            var productIds  = order.OrderItems.Select(oi => oi.ProductId).ToList();
            var inventories = await _db.Inventories
                .Where(inv => productIds.Contains(inv.ProductId))
                .OrderBy(inv => inv.InventoryId)
                .ToListAsync();

            foreach (var item in order.OrderItems)
            {
                var inv = inventories.FirstOrDefault(i => i.ProductId == item.ProductId);
                if (inv != null)
                    inv.Quantity += item.Quantity;
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = $"Order {id} has been cancelled and stock has been restored." });
        }



      

        [HttpGet("by-type/{type?}")]
        [HttpGet("by-status/{type?}")]
        [HttpGet("search")]
        public async Task<IActionResult> GetOrdersByType(
            [FromRoute] string? type,
            [FromQuery] string? searchType,
            [FromQuery] string? status)
        {
            var filter = type ?? searchType ?? status;
            if (string.IsNullOrWhiteSpace(filter))
                return BadRequest(new { message = "A search type or status parameter is required (e.g. 'Approved', 'Pending', 'Paid', 'Unpaid')." });

            filter = filter.Trim();

            int? userFilter = null;
            if (IsOnlineUser())
            {
                if (!TryGetCallerId(out int callerId))
                    return Unauthorized();
                userFilter = callerId;
            }

            var query = _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.DeliveryAddress)
                .AsQueryable();

            if (userFilter.HasValue)
            {
                query = query.Where(o => o.UserId == userFilter.Value);
            }

          
            if (Enum.TryParse<OrderStatus>(filter, ignoreCase: true, out var orderStatus))
            {
                var orders = await query
                    .Where(o => o.Status == orderStatus)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders.Select(MapToResponse).ToList());
            }

          
            if (Enum.TryParse<PaymentStatus>(filter, ignoreCase: true, out var paymentStatus))
            {
                var orders = await query
                    .Where(o => o.PaymentStatus == paymentStatus)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders.Select(MapToResponse).ToList());
            }

         
            if (filter.Equals("Online", StringComparison.OrdinalIgnoreCase))
            {
                var orders = await query
                    .Where(o => o.IsOnlineOrder)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders.Select(MapToResponse).ToList());
            }

            if (filter.Equals("POS", StringComparison.OrdinalIgnoreCase))
            {
                var orders = await query
                    .Where(o => !o.IsOnlineOrder)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders.Select(MapToResponse).ToList());
            }

            return BadRequest(new
            {
                message = $"Unknown search type or status '{filter}'. Valid options include: Approved, Pending, Confirmed, Cancelled (OrderStatus), Paid, Unpaid (PaymentStatus), or Online, POS (OrderType)."
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        private bool TryGetCallerId(out int userId)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.TryParse(idStr, out userId);
        }

        private List<string> GetCallerRoles() =>
            User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        private bool IsOnlineUser()
        {
            var roles = GetCallerRoles();
            return roles.Count == 1 && roles[0].Equals(UserRole, StringComparison.OrdinalIgnoreCase);
        }

       
        private static void DeductStock(IEnumerable<OrderItemRequestDto> items, List<Inventory> inventories)
        {
            foreach (var itemDto in items)
            {
                int remaining = itemDto.Quantity;

                var locations = inventories
                    .Where(inv => inv.ProductId == itemDto.ProductId)
                    .OrderBy(inv => inv.InventoryId);

                foreach (var inv in locations)
                {
                    if (remaining <= 0) break;
                    int deduct = Math.Min(inv.Quantity, remaining);
                    inv.Quantity -= deduct;
                    remaining    -= deduct;
                }
            }
        }

        private static readonly string[] AllowedDateFormats =
        {
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy"
        };

        private static bool TryParseDateOnly(string? input, out DateOnly date)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                date = default;
                return false;
            }

            return DateOnly.TryParseExact(
                input.Trim(),
                AllowedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        private static List<OrderItemResponseDto> MapItemRows(IEnumerable<OrderSpRow> rows) =>
            rows.Where(r => r.OrderItemId.HasValue)
                .Select(r => new OrderItemResponseDto
                {
                    OrderItemId = r.OrderItemId!.Value,
                    ProductId   = r.ProductId ?? 0,
                    ProductName = r.ProductName ?? string.Empty,
                    Quantity    = r.Quantity ?? 0,
                    UnitPrice   = r.UnitPrice ?? 0m,
                    Discount    = r.Discount ?? 0m,
                    Subtotal    = r.Subtotal ?? 0m
                })
                .ToList();

     
        private static List<OrderSummaryDto> GroupOrderRows(IEnumerable<OrderSpRow> rows) =>
            rows.GroupBy(r => r.OrderId)
                .Select(g =>
                {
                    var o     = g.First();
                    var items = MapItemRows(g);
                    return new OrderSummaryDto
                    {
                        OrderId              = o.OrderId,
                        UserId               = o.UserId,
                        OrderType            = o.IsOnlineOrder ? "Online" : "POS",
                        Status               = ((OrderStatus)o.Status).ToString(),
                        OrderDate            = o.OrderDate,
                        TotalAmount          = o.TotalAmount,
                        DiscountPercent      = o.DiscountPercent,
                        DiscountAmount       = o.DiscountAmount,
                        ShippingAmount       = o.ShippingAmount,
                        PayableAmount        = o.PayableAmount,
                        PaymentMethod        = ((PaymentMethodType)o.PaymentMethod).ToString(),
                        PaymentStatus        = ((PaymentStatus)o.PaymentStatus).ToString(),
                        PaymentTransactionId = o.PaymentTransactionId,
                        ItemCount            = items.Count,
                        Items                = items
                    };
                })
                .ToList();

        private static OrderResponseDto MapRowsToResponse(List<OrderSpRow> rows)
        {
            var o = rows[0];
            return new OrderResponseDto
            {
                OrderId              = o.OrderId,
                UserId               = o.UserId,
                OrderType            = o.IsOnlineOrder ? "Online" : "POS",
                Status               = ((OrderStatus)o.Status).ToString(),
                Phone                = o.Phone,
                OrderDate            = o.OrderDate,
                TotalAmount          = o.TotalAmount,
                DiscountPercent      = o.DiscountPercent,
                DiscountAmount       = o.DiscountAmount,
                ShippingAmount       = o.ShippingAmount,
                PayableAmount        = o.PayableAmount,
                PaymentMethod        = ((PaymentMethodType)o.PaymentMethod).ToString(),
                PaymentStatus        = ((PaymentStatus)o.PaymentStatus).ToString(),
                PaymentTransactionId = o.PaymentTransactionId,
                DeliveryAddress      = o.DeliveryAddressId == null ? null : new DeliveryAddressResponseDto
                {
                    DeliveryAddressId = o.DeliveryAddressId.Value,
                    RecipientName     = o.RecipientName ?? string.Empty,
                    Phone             = o.DeliveryPhone ?? string.Empty,
                    Address           = o.Address ?? string.Empty
                },
                Items = MapItemRows(rows)
            };
        }

        
        private static OrderResponseDto MapToResponse(Order order) => new()
        {
            OrderId         = order.OrderId,
            UserId          = order.UserId,
            OrderType       = order.IsOnlineOrder ? "Online" : "POS",
            Status          = order.Status.ToString(),
            Phone           = order.Phone,
            OrderDate       = order.OrderDate,
            TotalAmount     = order.TotalAmount,
            DiscountPercent = order.DiscountPercent,
            DiscountAmount  = order.DiscountAmount,
            ShippingAmount  = order.ShippingAmount,
            PayableAmount   = order.PayableAmount,
            PaymentMethod   = order.PaymentMethod.ToString(),
            PaymentStatus   = order.PaymentStatus.ToString(),
            PaymentTransactionId = order.PaymentTransactionId,
            DeliveryAddress = order.DeliveryAddress == null ? null : new DeliveryAddressResponseDto
            {
                DeliveryAddressId = order.DeliveryAddress.DeliveryAddressId,
                RecipientName     = order.DeliveryAddress.RecipientName,
                Phone             = order.DeliveryAddress.Phone,
                Address           = order.DeliveryAddress.Address
            },
            Items = order.OrderItems.Select(oi => new OrderItemResponseDto
            {
                OrderItemId = oi.OrderItemId,
                ProductId   = oi.ProductId,
                ProductName = oi.Product?.ProductName ?? string.Empty,
                Quantity    = oi.Quantity,
                UnitPrice   = oi.UnitPrice,
                Discount    = oi.Discount,
                Subtotal    = oi.Subtotal
            }).ToList()
        };
    }
}
