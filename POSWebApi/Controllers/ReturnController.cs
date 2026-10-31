using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using POSWebApi.DTOs;
using POSWebApi.Models;

namespace POSWebApi.Controllers
{
    [Route("api/[controller]")]  
    [ApiController]
    [Authorize(Roles = "Admin,PosOperator")]  
    public class ReturnController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ReturnController(AppDbContext db)
        {
            _db = db;
        }

       
        [HttpGet]
        public async Task<IActionResult> GetAllReturns()
        {
          
            var rows = await _db.Database
                .SqlQueryRaw<ReturnSpRow>("EXEC dbo.sp_GetAllReturns")
                .ToListAsync();

            return Ok(GroupReturns(rows));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
           
            var rows = await _db.Database
                .SqlQueryRaw<ReturnSpRow>("EXEC dbo.sp_GetReturnById @ReturnId",
                    new SqlParameter("@ReturnId", id))
                .ToListAsync();

            var ret = GroupReturns(rows).FirstOrDefault();
            if (ret == null)
                return NotFound(new { message = $"Return {id} not found." });

            return Ok(ret);
        }

       
        [HttpGet("order/{orderId:int}")]
        public async Task<IActionResult> GetByOrderId(int orderId)
        {
          
            var rows = await _db.Database
                .SqlQueryRaw<ReturnSpRow>("EXEC dbo.sp_GetReturnsByOrderId @OrderId",
                    new SqlParameter("@OrderId", orderId))
                .ToListAsync();

            return Ok(GroupReturns(rows));
        }

       
        [HttpPost]
        public async Task<IActionResult> CreateReturn([FromBody] CreateReturnDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest(new { message = "A return must contain at least one return item." });

          
            var duplicateOrderItemIds = dto.Items
                .GroupBy(i => i.OrderItemId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateOrderItemIds.Any())
            {
                return BadRequest(new
                {
                    message = $"Duplicate OrderItemIds found: {string.Join(", ", duplicateOrderItemIds)}. Merge quantities for the same order item."
                });
            }

          
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ReturnItems)
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);

            if (order == null)
                return NotFound(new { message = $"Order {dto.OrderId} not found." });

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest(new { message = $"Cannot create a return for cancelled Order {dto.OrderId}." });

          
            var validationErrors = new List<string>();
            foreach (var itemDto in dto.Items)
            {
                var orderItem = order.OrderItems.FirstOrDefault(oi => oi.OrderItemId == itemDto.OrderItemId);
                if (orderItem == null)
                {
                    validationErrors.Add($"OrderItemId {itemDto.OrderItemId} does not belong to Order {dto.OrderId}.");
                    continue;
                }

                int alreadyReturned = orderItem.ReturnItems.Sum(ri => ri.Quantity);
                int remainingReturnable = orderItem.Quantity - alreadyReturned;

                if (itemDto.Quantity > remainingReturnable)
                {
                    validationErrors.Add(
                        $"OrderItemId {itemDto.OrderItemId} (Product '{orderItem.Product?.ProductName}'): " +
                        $"requested return quantity {itemDto.Quantity}, but only {remainingReturnable} remaining returnable (ordered {orderItem.Quantity}, already returned {alreadyReturned}).");
                }
            }

            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = "Return validation failed.",
                    details = validationErrors
                });
            }

          
            var returnEntity = new Return
            {
                OrderId     = dto.OrderId,
                ReturnDate  = DateTime.UtcNow,
                ReturnItems = dto.Items.Select(i => new ReturnItem
                {
                    OrderItemId  = i.OrderItemId,
                    Quantity     = i.Quantity,
                    ReturnReason = i.ReturnReason
                }).ToList()
            };

            _db.Returns.Add(returnEntity);


            var productIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
            var inventories = await _db.Inventories
                .Where(inv => productIds.Contains(inv.ProductId))
                .OrderBy(inv => inv.InventoryId)
                .ToListAsync();

            foreach (var itemDto in dto.Items)
            {
                var orderItem = order.OrderItems.First(oi => oi.OrderItemId == itemDto.OrderItemId);
                var inv = inventories.FirstOrDefault(i => i.ProductId == orderItem.ProductId);
                if (inv != null)
                {
                    inv.Quantity += itemDto.Quantity;
                }
            }

            await _db.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = returnEntity.ReturnId },
                MapToResponse(returnEntity));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateReturn(int id, [FromBody] UpdateReturnDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest(new { message = "A return must contain at least one return item." });

            var duplicateOrderItemIds = dto.Items
                .GroupBy(i => i.OrderItemId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateOrderItemIds.Any())
            {
                return BadRequest(new
                {
                    message = $"Duplicate OrderItemIds found: {string.Join(", ", duplicateOrderItemIds)}. Merge quantities for the same order item."
                });
            }

            var returnEntity = await _db.Returns
                .Include(r => r.ReturnItems)
                .FirstOrDefaultAsync(r => r.ReturnId == id);

            if (returnEntity == null)
                return NotFound(new { message = $"Return {id} not found." });

            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ReturnItems)
                .FirstOrDefaultAsync(o => o.OrderId == returnEntity.OrderId);

            if (order == null)
                return NotFound(new { message = $"Associated Order {returnEntity.OrderId} not found." });

           
            var validationErrors = new List<string>();
            foreach (var itemDto in dto.Items)
            {
                var orderItem = order.OrderItems.FirstOrDefault(oi => oi.OrderItemId == itemDto.OrderItemId);
                if (orderItem == null)
                {
                    validationErrors.Add($"OrderItemId {itemDto.OrderItemId} does not belong to Order {order.OrderId}.");
                    continue;
                }

                int returnedInOtherReturns = orderItem.ReturnItems
                    .Where(ri => ri.ReturnId != id)
                    .Sum(ri => ri.Quantity);

                int maxReturnable = orderItem.Quantity - returnedInOtherReturns;

                if (itemDto.Quantity > maxReturnable)
                {
                    validationErrors.Add(
                        $"OrderItemId {itemDto.OrderItemId} (Product '{orderItem.Product?.ProductName}'): " +
                        $"requested return quantity {itemDto.Quantity}, but maximum returnable is {maxReturnable}.");
                }
            }

            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = "Return update validation failed.",
                    details = validationErrors
                });
            }

            // Load inventories for this order's products
            var productIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
            var inventories = await _db.Inventories
                .Where(inv => productIds.Contains(inv.ProductId))
                .OrderBy(inv => inv.InventoryId)
                .ToListAsync();

            // 1. Revert inventory additions from the old ReturnItems
            foreach (var oldItem in returnEntity.ReturnItems)
            {
                var orderItem = order.OrderItems.First(oi => oi.OrderItemId == oldItem.OrderItemId);
                var inv = inventories.FirstOrDefault(i => i.ProductId == orderItem.ProductId);
                if (inv != null)
                {
                    inv.Quantity = Math.Max(0, inv.Quantity - oldItem.Quantity);
                }
            }

            // 2. Replace ReturnItems with the updated list and apply new inventory additions
            _db.ReturnItems.RemoveRange(returnEntity.ReturnItems);

            returnEntity.ReturnItems = dto.Items.Select(i => new ReturnItem
            {
                ReturnId     = returnEntity.ReturnId,
                OrderItemId  = i.OrderItemId,
                Quantity     = i.Quantity,
                ReturnReason = i.ReturnReason
            }).ToList();

            foreach (var newItem in dto.Items)
            {
                var orderItem = order.OrderItems.First(oi => oi.OrderItemId == newItem.OrderItemId);
                var inv = inventories.FirstOrDefault(i => i.ProductId == orderItem.ProductId);
                if (inv != null)
                {
                    inv.Quantity += newItem.Quantity;
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Return {id} updated successfully.",
                data    = MapToResponse(returnEntity)
            });
        }

       
        [HttpPut("item/{returnItemId:int}")]
        public async Task<IActionResult> UpdateReturnItem(int returnItemId, [FromBody] UpdateReturnItemDto dto)
        {
            var returnItem = await _db.ReturnItems
                .Include(ri => ri.OrderItem)
                    .ThenInclude(oi => oi.Product)
                .Include(ri => ri.OrderItem)
                    .ThenInclude(oi => oi.ReturnItems)
                .FirstOrDefaultAsync(ri => ri.ReturnItemId == returnItemId);

            if (returnItem == null)
                return NotFound(new { message = $"ReturnItem {returnItemId} not found." });

            var orderItem = returnItem.OrderItem;
            int returnedInOtherItems = orderItem.ReturnItems
                .Where(ri => ri.ReturnItemId != returnItemId)
                .Sum(ri => ri.Quantity);

            int maxReturnable = orderItem.Quantity - returnedInOtherItems;
            if (dto.Quantity > maxReturnable)
            {
                return BadRequest(new
                {
                    message = $"Requested return quantity {dto.Quantity} exceeds maximum returnable quantity {maxReturnable} for OrderItemId {orderItem.OrderItemId}."
                });
            }

            int diff = dto.Quantity - returnItem.Quantity;
            returnItem.Quantity     = dto.Quantity;
            returnItem.ReturnReason = dto.ReturnReason;

            if (diff != 0)
            {
                var inv = await _db.Inventories
                    .Where(i => i.ProductId == orderItem.ProductId)
                    .OrderBy(i => i.InventoryId)
                    .FirstOrDefaultAsync();

                if (inv != null)
                {
                    inv.Quantity = Math.Max(0, inv.Quantity + diff);
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"ReturnItem {returnItemId} updated successfully.",
                item    = new ReturnItemResponseDto
                {
                    ReturnItemId = returnItem.ReturnItemId,
                    OrderItemId  = returnItem.OrderItemId,
                    ProductId    = orderItem.ProductId,
                    ProductName  = orderItem.Product?.ProductName ?? string.Empty,
                    Quantity     = returnItem.Quantity,
                    UnitPrice    = orderItem.UnitPrice,
                    ReturnReason = returnItem.ReturnReason
                }
            });
        }

      
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteReturn(int id)
        {
            var returnEntity = await _db.Returns
                .Include(r => r.ReturnItems)
                    .ThenInclude(ri => ri.OrderItem)
                .FirstOrDefaultAsync(r => r.ReturnId == id);

            if (returnEntity == null)
                return NotFound(new { message = $"Return {id} not found." });

            var productIds = returnEntity.ReturnItems
                .Select(ri => ri.OrderItem.ProductId)
                .Distinct()
                .ToList();

            var inventories = await _db.Inventories
                .Where(inv => productIds.Contains(inv.ProductId))
                .OrderBy(inv => inv.InventoryId)
                .ToListAsync();

            foreach (var item in returnEntity.ReturnItems)
            {
                var inv = inventories.FirstOrDefault(i => i.ProductId == item.OrderItem.ProductId);
                if (inv != null)
                {
                    inv.Quantity = Math.Max(0, inv.Quantity - item.Quantity);
                }
            }

            _db.Returns.Remove(returnEntity);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Return {id} and its items have been deleted." });
        }

       
        [HttpDelete("item/{returnItemId:int}")]
        public async Task<IActionResult> DeleteReturnItem(int returnItemId)
        {
            var returnItem = await _db.ReturnItems
                .Include(ri => ri.OrderItem)
                .FirstOrDefaultAsync(ri => ri.ReturnItemId == returnItemId);

            if (returnItem == null)
                return NotFound(new { message = $"ReturnItem {returnItemId} not found." });

            var inv = await _db.Inventories
                .Where(i => i.ProductId == returnItem.OrderItem.ProductId)
                .OrderBy(i => i.InventoryId)
                .FirstOrDefaultAsync();

            if (inv != null)
            {
                inv.Quantity = Math.Max(0, inv.Quantity - returnItem.Quantity);
            }

            _db.ReturnItems.Remove(returnItem);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"ReturnItem {returnItemId} has been deleted." });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helper
        // ──────────────────────────────────────────────────────────────────────
        private static ReturnResponseDto MapToResponse(Return ret) => new()
        {
            ReturnId   = ret.ReturnId,
            OrderId    = ret.OrderId,
            ReturnDate = ret.ReturnDate,
            Items      = ret.ReturnItems.Select(ri => new ReturnItemResponseDto
            {
                ReturnItemId = ri.ReturnItemId,
                OrderItemId  = ri.OrderItemId,
                ProductId    = ri.OrderItem?.ProductId ?? 0,
                ProductName  = ri.OrderItem?.Product?.ProductName ?? string.Empty,
                Quantity     = ri.Quantity,
                UnitPrice    = ri.OrderItem?.UnitPrice ?? 0m,
                ReturnReason = ri.ReturnReason
            }).ToList()
        };

      
        private static List<ReturnResponseDto> GroupReturns(IEnumerable<ReturnSpRow> rows) =>
            rows.GroupBy(r => r.ReturnId)
                .Select(g =>
                {
                    var head = g.First();
                    return new ReturnResponseDto
                    {
                        ReturnId   = head.ReturnId,
                        OrderId    = head.OrderId,
                        ReturnDate = head.ReturnDate,
                        Items      = g.Where(r => r.ReturnItemId.HasValue)
                                      .Select(r => new ReturnItemResponseDto
                                      {
                                          ReturnItemId = r.ReturnItemId!.Value,
                                          OrderItemId  = r.OrderItemId ?? 0,
                                          ProductId    = r.ProductId ?? 0,
                                          ProductName  = r.ProductName ?? string.Empty,
                                          Quantity     = r.Quantity ?? 0,
                                          UnitPrice    = r.UnitPrice ?? 0m,
                                          ReturnReason = r.ReturnReason
                                      }).ToList()
                    };
                })
                .ToList();
    }
}

