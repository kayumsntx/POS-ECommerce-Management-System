using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using POSWebApi.DTOs;
using POSWebApi.Models;

namespace POSWebApi.Controllers
{
    [Route("api/[controller]")]  
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<AppRole>  _roleManager;
        private readonly UserManager<AppUser>  _userManager;
        private readonly AppDbContext          _db;

        public RoleController(
            RoleManager<AppRole> roleManager,
            UserManager<AppUser> userManager,
            AppDbContext db)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _db          = db;
        }

       
        [HttpGet]
        public async Task<IActionResult> GetAllRoles()
        {
            // Stored procedure: dbo.sp_GetAllRoles
            var rows = await _db.Database
                .SqlQueryRaw<RoleSpRow>("EXEC dbo.sp_GetAllRoles")
                .ToListAsync();

            var roles = rows.Select(r => new RoleResponseDto
            {
                Id       = r.Id.ToString(),
                RoleName = r.Name
            }).ToList();

            return Ok(roles);
        }

        
        [HttpPost("create")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto)
        {
           
            if (await _roleManager.RoleExistsAsync(dto.RoleName))
                return BadRequest(new { message = $"Role '{dto.RoleName}' already exists." });

            // 2. Create the role
            var role   = new AppRole { Name = dto.RoleName };
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            return Ok(new { message = $"Role '{dto.RoleName}' created successfully." });
        }

      
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
          
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound(new { message = $"Role with ID '{id}' not found." });

            // 2. Delete the role
            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            return Ok(new { message = $"Role '{role.Name}' deleted successfully." });
        }

        
        [HttpPost]
        public async Task<IActionResult> AssignRole([FromBody] UserRoleDto dto)
        {
           
            var user = _userManager.Users
                           .FirstOrDefault(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null)
                return NotFound(new { message = $"User with phone '{dto.PhoneNumber}' not found." });

            
            if (!await _roleManager.RoleExistsAsync(dto.RoleName))
                return NotFound(new { message = $"Role '{dto.RoleName}' does not exist." });


           
            if (await _userManager.IsInRoleAsync(user, dto.RoleName))
                return BadRequest(new { message = $"User already has the role '{dto.RoleName}'." });

            // 4. Assign the role
            var result = await _userManager.AddToRoleAsync(user, dto.RoleName);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            return Ok(new { message = $"Role '{dto.RoleName}' assigned to user '{user.UserName}' successfully." });
        }

      
        [HttpPost("remove")]
        public async Task<IActionResult> RemoveRole([FromBody] UserRoleDto dto)
        {
           
            var user = _userManager.Users
                           .FirstOrDefault(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null)
                return NotFound(new { message = $"User with phone '{dto.PhoneNumber}' not found." });

            
            if (!await _userManager.IsInRoleAsync(user, dto.RoleName))
                return BadRequest(new { message = $"User does not have the role '{dto.RoleName}'." });

            
            var result = await _userManager.RemoveFromRoleAsync(user, dto.RoleName);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            return Ok(new { message = $"Role '{dto.RoleName}' removed from user '{user.UserName}' successfully." });
        }

       
        [HttpGet("user/{phoneNumber}")]
        public async Task<IActionResult> GetUserRoles(string phoneNumber)
        {
          
            var rows = await _db.Database
                .SqlQueryRaw<UserRoleSpRow>("EXEC dbo.sp_GetUserRolesByPhone @PhoneNumber",
                    new SqlParameter("@PhoneNumber", phoneNumber))
                .ToListAsync();

            if (rows.Count == 0)
                return NotFound(new { message = $"User with phone '{phoneNumber}' not found." });

            return Ok(GroupUsers(rows).First());
        }

     
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsersWithRoles()
        {
            
            var rows = await _db.Database
                .SqlQueryRaw<UserRoleSpRow>("EXEC dbo.sp_GetAllUsersWithRoles")
                .ToListAsync();

            return Ok(GroupUsers(rows));
        }

     
        private static List<UserWithRolesDto> GroupUsers(IEnumerable<UserRoleSpRow> rows) =>
            rows.GroupBy(r => r.UserId)
                .Select(g =>
                {
                    var u = g.First();
                    return new UserWithRolesDto
                    {
                        UserName    = u.UserName    ?? string.Empty,
                        Email       = u.Email       ?? string.Empty,
                        PhoneNumber = u.PhoneNumber ?? string.Empty,
                        Roles       = g.Where(r => r.RoleName != null).Select(r => r.RoleName!).ToList()
                    };
                })
                .ToList();
    }
}
