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
    [Authorize(Roles = "Admin,Operator")]
    public class PaymentMethodController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PaymentMethodController(AppDbContext db)
        {
            _db = db;
        }

      
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
           
            var methods = await _db.Database
                .SqlQueryRaw<PaymentMethodResponseDto>("EXEC dbo.sp_GetAllPaymentMethods")
                .ToListAsync();

            return Ok(methods);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
          
            var method = (await _db.Database
                .SqlQueryRaw<PaymentMethodResponseDto>("EXEC dbo.sp_GetPaymentMethodById @PaymentMethodId",
                    new SqlParameter("@PaymentMethodId", id))
                .ToListAsync())
                .FirstOrDefault();

            if (method == null)
                return NotFound(new { message = $"Payment method with ID {id} not found." });

            return Ok(method);
        }

      
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] PaymentMethod method)
        {
           
            bool typeExists = await _db.PaymentMethods
                .AnyAsync(pm => pm.PayMethodType.ToLower() == method.PayMethodType.ToLower());

            if (typeExists)
                return BadRequest(new { message = $"Payment method '{method.PayMethodType}' already exists." });

            
            method.PaymentMethodId = 0;
            method.Payments        = new List<Payment>();

            _db.PaymentMethods.Add(method);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = method.PaymentMethodId },
                new { method.PaymentMethodId, method.PayMethodType });
        }

        [HttpPut("update/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PaymentMethod updated)
        {
            var method = await _db.PaymentMethods.FindAsync(id);

            if (method == null)
                return NotFound(new { message = $"Payment method with ID {id} not found." });

            // Check duplicate type (excluding this record)
            bool typeExists = await _db.PaymentMethods
                .AnyAsync(pm => pm.PayMethodType.ToLower() == updated.PayMethodType.ToLower()
                                && pm.PaymentMethodId != id);

            if (typeExists)
                return BadRequest(new { message = $"Another payment method named '{updated.PayMethodType}' already exists." });

            method.PayMethodType = updated.PayMethodType;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Payment method updated to '{method.PayMethodType}' successfully.",
                method  = new { method.PaymentMethodId, method.PayMethodType }
            });
        }

      
     
        [HttpDelete("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var method = await _db.PaymentMethods
                .Include(pm => pm.Payments)
                .FirstOrDefaultAsync(pm => pm.PaymentMethodId == id);

            if (method == null)
                return NotFound(new { message = $"Payment method with ID {id} not found." });

         
            if (method.Payments.Any())
                return BadRequest(new { message = $"Cannot delete '{method.PayMethodType}' because {method.Payments.Count} payment record(s) reference it." });

            _db.PaymentMethods.Remove(method);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Payment method '{method.PayMethodType}' deleted successfully." });
        }
    }
}
