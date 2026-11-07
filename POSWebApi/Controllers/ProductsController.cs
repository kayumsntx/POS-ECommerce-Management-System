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
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext        _db;
        private readonly IWebHostEnvironment _env;

        private const string NoImage       = "noimage.png";
        private const long   MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

        public ProductsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
        }

     
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
           
            var products = await _db.Database
                .SqlQueryRaw<ProductResponseDto>("EXEC dbo.sp_GetAllProducts")
                .ToListAsync();

            return Ok(products);
        }

        
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
           
            var product = (await _db.Database
                .SqlQueryRaw<ProductResponseDto>("EXEC dbo.sp_GetProductById @ProductId",
                    new SqlParameter("@ProductId", id))
                .ToListAsync())
                .FirstOrDefault();

            if (product == null)
                return NotFound(new { message = $"Product with ID {id} not found." });

            return Ok(product);
        }

       

        // ── GET api/products/barcode/{barcode} ────────────────────────────────
        /// <summary>
        /// Find a product by its barcode (e.g. scanned via a barcode reader at POS).
        /// </summary>
        [HttpGet("barcode/{barcode}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return BadRequest(new { message = "Barcode must not be empty." });

            var product = await _db.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Barcode == barcode.Trim());

            if (product == null)
                return NotFound(new { message = $"No product found with barcode '{barcode}'." });

            return Ok(new ProductResponseDto
            {
                ProductId    = product.ProductId,
                ProductName  = product.ProductName,
                Description  = product.Description,
                Barcode      = product.Barcode,
                CategoryId   = product.CategoryId,
                CategoryName = product.Category?.Name ?? string.Empty,
                Cost         = product.Cost,
                Price        = product.Price,
                ImageUrl     = product.ImageUrl,
                CreatedAt    = product.CreatedAt
            });
        }
        [HttpPost("create")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Create([FromBody] ProductDto dto)
        {
          
            bool categoryExists = await _db.Categories.AnyAsync(c => c.CategoryId == dto.CategoryId);
            if (!categoryExists)
                return BadRequest(new { message = $"Category with ID {dto.CategoryId} does not exist." });

            // Prevent duplicate barcode
            bool barcodeExists = await _db.Products.AnyAsync(p => p.Barcode == dto.Barcode);
            if (barcodeExists)
                return BadRequest(new { message = $"A product with barcode '{dto.Barcode}' already exists." });

            var product = new Product
            {
                ProductName = dto.ProductName,
                Description = dto.Description,
                Barcode     = dto.Barcode,
                CategoryId  = dto.CategoryId,
                Cost        = dto.Cost,
                Price       = dto.Price,
                ImageUrl    = NoImage,         
                CreatedAt   = DateTime.Now
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

           
            var categoryName = (await _db.Categories.FindAsync(product.CategoryId))?.Name ?? string.Empty;
            return CreatedAtAction(
                nameof(GetById),
                new { id = product.ProductId },
                new
                {
                    product.ProductId,
                    product.ProductName,
                    product.Description,
                    product.Barcode,
                    product.CategoryId,
                    CategoryName = categoryName,
                    product.Cost,
                    product.Price,
                    product.ImageUrl,
                    product.CreatedAt
                });
        }

       
        [HttpPut("update/{id:int}")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductDto dto)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { message = $"Product with ID {id} not found." });

         
            bool categoryExists = await _db.Categories.AnyAsync(c => c.CategoryId == dto.CategoryId);
            if (!categoryExists)
                return BadRequest(new { message = $"Category with ID {dto.CategoryId} does not exist." });

           
            bool barcodeExists = await _db.Products
                .AnyAsync(p => p.Barcode == dto.Barcode && p.ProductId != id);
            if (barcodeExists)
                return BadRequest(new { message = $"Another product with barcode '{dto.Barcode}' already exists." });

            
            product.ProductName = dto.ProductName;
            product.Description = dto.Description;
            product.Barcode     = dto.Barcode;
            product.CategoryId  = dto.CategoryId;
            product.Cost        = dto.Cost;
            product.Price       = dto.Price;

            await _db.SaveChangesAsync();

            var categoryName = (await _db.Categories.FindAsync(product.CategoryId))?.Name ?? string.Empty;
            return Ok(new
            {
                message = $"Product '{product.ProductName}' updated successfully.",
                product = new
                {
                    product.ProductId,
                    product.ProductName,
                    product.Description,
                    product.Barcode,
                    product.CategoryId,
                    CategoryName = categoryName,
                    product.Cost,
                    product.Price,
                    product.ImageUrl,
                    product.CreatedAt
                }
            });
        }

      
        [HttpDelete("delete/{id:int}")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products
                .Include(p => p.OrderItems)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound(new { message = $"Product with ID {id} not found." });

           
            if (product.OrderItems.Any())
                return BadRequest(new
                {
                    message = $"Cannot delete '{product.ProductName}' because it is referenced by " +
                              $"{product.OrderItems.Count} order item(s)."
                });

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Product '{product.ProductName}' deleted successfully." });
        }

       
        [HttpPost("upload/{id:int}")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<ActionResult<UploadResponse>> Upload(int id, IFormFile file)
        {
           
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { message = $"Product with ID {id} not found." });

           
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            if (file.Length > MaxFileSizeBytes)
                return BadRequest(new { message = "File size must not exceed 2 MB." });

            string imageFolder = Path.Combine(_env.WebRootPath, "images");
            if (!Directory.Exists(imageFolder))
                Directory.CreateDirectory(imageFolder);

            string ext         = Path.GetExtension(file.FileName);
            string newFileName = Guid.NewGuid().ToString() + ext;
            string newFilePath = Path.Combine(imageFolder, newFileName);

           
            string fullImagesFolder = Path.GetFullPath(imageFolder);
            string fullNewFilePath  = Path.GetFullPath(newFilePath);
            if (!fullNewFilePath.StartsWith(fullImagesFolder, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Invalid file name." });

            
            string? oldFileName = product.ImageUrl;

         
            try
            {
                using (var fileStream = new FileStream(newFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }
            }
            catch (IOException)
            {
                return StatusCode(500, new { message = "An error occurred while saving the file." });
            }

            // 6. Update the database reference
            product.ImageUrl = newFileName;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                // DB update failed — clean up the newly uploaded file and preserve the old one
                TryDeleteFile(imageFolder, newFileName);
                return StatusCode(500, new { message = "Failed to update the product image in the database." });
            }

            // 7. Delete the old image only after the DB update succeeded, and never delete noimage.jpg
            if (!string.IsNullOrWhiteSpace(oldFileName) &&
                !oldFileName.Equals(NoImage, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(imageFolder, oldFileName);
            }

            return Ok(new UploadResponse { FileName = product.ImageUrl });
        }



        // ── Private helper ────────────────────────────────────────────────────

       
        private static void TryDeleteFile(string imageFolder, string fileName)
        {
            try
            {
                string fullFolder   = Path.GetFullPath(imageFolder);
                string fullFilePath = Path.GetFullPath(Path.Combine(imageFolder, fileName));

            
                if (!fullFilePath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase))
                    return;

                if (System.IO.File.Exists(fullFilePath))
                    System.IO.File.Delete(fullFilePath);
            }
            catch
            {
               
            }
        }
    }
}
