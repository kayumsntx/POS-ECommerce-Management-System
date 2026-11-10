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
    [Authorize(Roles = "Admin,Cashier")]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _db;

        public InventoryController(AppDbContext db)
        {
            _db = db;
        }

       
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var inventory = await _db.Database
                    .SqlQueryRaw<InventoryResponseDto>("EXEC dbo.sp_GetAllInventories")
                    .ToListAsync();

                return Ok(inventory);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
          
            var inventory = (await _db.Database
                .SqlQueryRaw<InventoryResponseDto>("EXEC dbo.sp_GetInventoryById @InventoryId",
                    new SqlParameter("@InventoryId", id))
                .ToListAsync())
                .FirstOrDefault();

            if (inventory == null)
                return NotFound(new { message = $"Inventory record with ID {id} not found." });

            return Ok(inventory);
        }

        
        [HttpGet("product/{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByProduct(int productId)
        {
           
            var inventory = (await _db.Database
                .SqlQueryRaw<InventoryResponseDto>("EXEC dbo.sp_GetInventoryByProductId @ProductId",
                    new SqlParameter("@ProductId", productId))
                .ToListAsync())
                .FirstOrDefault();

            if (inventory == null)
                return NotFound(new { message = $"No inventory record found for product ID {productId}." });

            return Ok(inventory);
        }

       
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStock([FromQuery] int threshold = 10)
        {
           
            var lowStock = await _db.Database
                .SqlQueryRaw<InventoryResponseDto>("EXEC dbo.sp_GetLowStockInventories @Threshold",
                    new SqlParameter("@Threshold", threshold))
                .ToListAsync();

            return Ok(new
            {
                threshold,
                count = lowStock.Count,
                items = lowStock
            });
        }

       
        [HttpPost("create")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Create([FromBody] InventoryDto dto)
        {
           
            bool productExists = await _db.Products.AnyAsync(p => p.ProductId == dto.ProductId);
            if (!productExists)
                return BadRequest(new { message = $"Product with ID {dto.ProductId} does not exist." });

           
            bool alreadyExists = await _db.Inventories.AnyAsync(i => i.ProductId == dto.ProductId);
            if (alreadyExists)
                return BadRequest(new
                {
                    message = $"An inventory record for product ID {dto.ProductId} already exists. " +
                              "Use the update or adjust endpoint to modify it."
                });

            var inventory = new Inventory
            {
                ProductId  = dto.ProductId,
                Quantity   = dto.Quantity,
                UpdatedAt  = DateTime.Now
            };

            _db.Inventories.Add(inventory);
            await _db.SaveChangesAsync();

          
            var productName = (await _db.Products.FindAsync(inventory.ProductId))?.ProductName ?? string.Empty;

            return CreatedAtAction(
                nameof(GetById),
                new { id = inventory.InventoryId },
                new
                {
                    inventory.InventoryId,
                    inventory.ProductId,
                    ProductName = productName,
                    inventory.Quantity,
                    inventory.UpdatedAt,
                    message = "Inventory record created successfully."
                });
        }

       

        [HttpPut("update/{id:int}")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryDto dto)
        {
            var inventory = await _db.Inventories.FindAsync(id);
            if (inventory == null)
                return NotFound(new { message = $"Inventory record with ID {id} not found." });

         
            if (inventory.ProductId != dto.ProductId)
            {
                bool productExists = await _db.Products.AnyAsync(p => p.ProductId == dto.ProductId);
                if (!productExists)
                    return BadRequest(new { message = $"Product with ID {dto.ProductId} does not exist." });

               
                bool newProductHasInventory = await _db.Inventories
                    .AnyAsync(i => i.ProductId == dto.ProductId && i.InventoryId != id);
                if (newProductHasInventory)
                    return BadRequest(new
                    {
                        message = $"Product ID {dto.ProductId} already has its own inventory record."
                    });

                inventory.ProductId = dto.ProductId;
            }

            inventory.Quantity  = dto.Quantity;
            inventory.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            var productName = (await _db.Products.FindAsync(inventory.ProductId))?.ProductName ?? string.Empty;

            return Ok(new
            {
                message = "Inventory updated successfully.",
                inventory = new
                {
                    inventory.InventoryId,
                    inventory.ProductId,
                    ProductName = productName,
                    inventory.Quantity,
                    inventory.UpdatedAt
                }
            });
        }

        
        [HttpPatch("adjust/{id:int}")]
        public async Task<IActionResult> AdjustQuantity(int id, [FromBody] AdjustQuantityDto dto)
        {
            var inventory = await _db.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.InventoryId == id);

            if (inventory == null)
                return NotFound(new { message = $"Inventory record with ID {id} not found." });

            int newQuantity = inventory.Quantity + dto.Adjustment;

            if (newQuantity < 0)
                return BadRequest(new
                {
                    message     = $"Adjustment would result in negative stock ({newQuantity}). " +
                                  $"Current quantity: {inventory.Quantity}, Adjustment: {dto.Adjustment}."
                });

            inventory.Quantity  = newQuantity;
            inventory.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Stock adjusted by {dto.Adjustment:+#;-#;0}. New quantity: {inventory.Quantity}.",
                inventory = new
                {
                    inventory.InventoryId,
                    inventory.ProductId,
                    ProductName  = inventory.Product.ProductName,
                    inventory.Quantity,
                    inventory.UpdatedAt
                }
            });
        }

       
        [HttpDelete("delete/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var inventory = await _db.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.InventoryId == id);

            if (inventory == null)
                return NotFound(new { message = $"Inventory record with ID {id} not found." });

            _db.Inventories.Remove(inventory);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Inventory record for product '{inventory.Product.ProductName}' deleted successfully."
            });
        }
    }
}
