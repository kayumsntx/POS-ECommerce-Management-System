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
        public async Task<IActionResult> Create([FromBody] CreatePaymentMethodDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PayMethodType))
                return BadRequest(new { message = "Payment method type is required." });

            bool typeExists = await _db.PaymentMethods
                .AnyAsync(pm => pm.PayMethodType.ToLower() == dto.PayMethodType.Trim().ToLower());

            if (typeExists)
                return BadRequest(new { message = $"Payment method '{dto.PayMethodType}' already exists." });

            var method = new PaymentMethod
            {
                PayMethodType = dto.PayMethodType.Trim()
            };

            _db.PaymentMethods.Add(method);
            await _db.SaveChangesAsync();

            var response = new PaymentMethodResponseDto
            {
                PaymentMethodId = method.PaymentMethodId,
                PayMethodType = method.PayMethodType
            };

            return CreatedAtAction(nameof(GetById), new { id = method.PaymentMethodId }, response);
        }
        //================================Update=========================
        [HttpPut("update/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreatePaymentMethodDto dto)
        {
            var method = await _db.PaymentMethods.FindAsync(id);

            if (method == null)
                return NotFound(new { message = $"Payment method with ID {id} not found." });

            bool typeExists = await _db.PaymentMethods
                .AnyAsync(pm => pm.PayMethodType.ToLower() == dto.PayMethodType.Trim().ToLower()
                             && pm.PaymentMethodId != id);

            if (typeExists)
                return BadRequest(new { message = $"Another payment method named '{dto.PayMethodType}' already exists." });

            method.PayMethodType = dto.PayMethodType.Trim();
            await _db.SaveChangesAsync();

            var response = new PaymentMethodResponseDto
            {
                PaymentMethodId = method.PaymentMethodId,
                PayMethodType = method.PayMethodType
            };

            return Ok(new
            {
                message = $"Payment method updated to '{method.PayMethodType}' successfully.",
                method = response
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
