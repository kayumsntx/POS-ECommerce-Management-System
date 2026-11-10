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
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CategoryController(AppDbContext db)
        {
            _db = db;
        }

      
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
           
            var categories = await _db.Database
                .SqlQueryRaw<CategoryResponseDto>("EXEC dbo.sp_GetAllCategories")
                .ToListAsync();

            return Ok(categories);
        }


        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
           
            var category = (await _db.Database
                .SqlQueryRaw<CategoryResponseDto>("EXEC dbo.sp_GetCategoryById @CategoryId",
                    new SqlParameter("@CategoryId", id))
                .ToListAsync())
                .FirstOrDefault();

            if (category == null)
                return NotFound(new { message = $"Category with ID {id} not found." });

            return Ok(category);
        }


       
        [HttpPost]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Create([FromBody] CategoryDto dto)
        {
           
            bool nameExists = await _db.Categories
                .AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower());

            if (nameExists)
                return BadRequest(new { message = $"Category '{dto.Name}' already exists." });

          
            var category = new Category
            {
                Name        = dto.Name,
                Description = dto.Description
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = category.CategoryId },
                new { category.CategoryId, category.Name, category.Description });
        }

      
        [HttpPut("update/{id:int}")]
        [Authorize(Roles = "Admin,Cashier")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryDto dto)
        {
            var category = await _db.Categories.FindAsync(id);

            if (category == null)
                return NotFound(new { message = $"Category with ID {id} not found." });

          
            bool nameExists = await _db.Categories
                .AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower() && c.CategoryId != id);

            if (nameExists)
                return BadRequest(new { message = $"Another category named '{dto.Name}' already exists." });

          
            category.Name        = dto.Name;
            category.Description = dto.Description;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message  = $"Category '{category.Name}' updated successfully.",
                category = new { category.CategoryId, category.Name, category.Description }
            });
        }

      
        [HttpDelete("delete/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
                return NotFound(new { message = $"Category with ID {id} not found." });

          
            if (category.Products.Any())
                return BadRequest(new { message = $"Cannot delete '{category.Name}' because it has {category.Products.Count} linked product(s). Reassign or delete the products first." });

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Category '{category.Name}' deleted successfully." });
        }
    }
}
