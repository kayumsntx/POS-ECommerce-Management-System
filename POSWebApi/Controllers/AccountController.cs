using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using POSWebApi.DTOs;
using POSWebApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace POSWebApi.Controllers
{
    [Route("api/[controller]")]  
    [ApiController]
    public class AccountController : ControllerBase
    {
       
        private readonly UserManager<AppUser>   _userManager;  
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration         _config;        

        public AccountController(
            UserManager<AppUser>   userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration         config)
        {
            _userManager   = userManager;
            _signInManager = signInManager;
            _config        = config;
        }

        
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var email = dto.Email.Trim();

           
            var user = new AppUser
            {
                UserName    = email,
                Email       = email,
                PhoneNumber = dto.PhoneNumber.Trim()
            };



          
            var result = await _userManager.CreateAsync(user, dto.Password);

          
            if (!result.Succeeded)
            {
               
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

           
            await _userManager.AddToRoleAsync(user, "User");

            return Ok(new { message = "Account created successfully!" });
        }

       
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
           
            var user = _userManager.Users
                           .FirstOrDefault(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null)
                return Unauthorized(new { message = "Invalid phone number or password." });

            // 2. Check if the password is correct (false = don't lock out on failure)
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid phone number or password." });

          
            var roles = await _userManager.GetRolesAsync(user);

          
            var token     = GenerateJwtToken(user, roles);
            var expiryMins = int.Parse(_config["Jwt:ExpiryInMinutes"]!);

          
            return Ok(new LoginResponseDto
            {
                Token     = token,
                UserName  = user.UserName!,
                Email     = user.Email!,
                Role      = roles.FirstOrDefault() ?? "User",
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMins)
            });
        }

        
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            // 1. Find the user by email
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user == null)
                return Ok(new { message = "If that email exists, a reset token has been sent." });

            
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            return Ok(new
            {
                message = "Password reset token generated. Use it with the reset-password endpoint.",
                token   = token  
            });
        }

       
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
         
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid request." });

        
            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

         
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            return Ok(new { message = "Password reset successfully. You can now log in." });
        }

   
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAllAccounts()
        {
            var users = _userManager.Users
                .Select(u => new { u.Id, u.UserName, u.Email, u.PhoneNumber })
                .ToList();

            return Ok(users);
        }

      
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = $"User with ID {id} not found." });

            return Ok(new { user.Id, user.UserName, user.Email, user.PhoneNumber });
        }

      
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = $"User with ID {id} not found." });

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            return Ok(new { message = $"User '{user.Email}' deleted successfully." });
        }

        // ──────────────────────────────────────────────────────────────────────
        // PRIVATE HELPER — builds the JWT token
        // ──────────────────────────────────────────────────────────────────────
        private string GenerateJwtToken(AppUser user, IList<string> roles)
        {
           
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(ClaimTypes.Name,               user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };

           
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

         
            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = int.Parse(_config["Jwt:ExpiryInMinutes"]!);

            var token = new JwtSecurityToken(
                issuer:             _config["Jwt:Issuer"],
                audience:           _config["Jwt:Audience"],
                claims:             claims,
                expires:            DateTime.UtcNow.AddMinutes(expiry),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
