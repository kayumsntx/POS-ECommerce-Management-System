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
        private readonly IWebHostEnvironment    _env;

        public AccountController(
            UserManager<AppUser>   userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration         config,
            IWebHostEnvironment    env)
        {
            _userManager   = userManager;
            _signInManager = signInManager;
            _config        = config;
            _env           = env;
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
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(new { errors, message = string.Join(" ", errors) });
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
                FullName  = user.FullName ?? user.UserName!,
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
            var currentCallerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                               ?? User.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(currentCallerId) && int.TryParse(currentCallerId, out int callerId) && callerId == id)
            {
                return BadRequest(new { message = "You cannot delete your own account." });
            }

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
        // PROFILE & ACCOUNT MANAGEMENT (For logged-in User/Admin/Cashier)
        // ──────────────────────────────────────────────────────────────────────

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { message = "User not found or unauthorized." });

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new UserProfileDto
            {
                Id             = user.Id,
                UserName       = user.UserName ?? string.Empty,
                FullName       = user.FullName ?? user.UserName ?? string.Empty,
                Email          = user.Email ?? string.Empty,
                PhoneNumber    = user.PhoneNumber ?? string.Empty,
                Role           = roles.FirstOrDefault() ?? "User",
                DefaultAddress = user.DefaultAddress ?? string.Empty,
                AvatarUrl      = user.AvatarUrl ?? string.Empty
            });
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { message = "User not found or unauthorized." });

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                user.PhoneNumber = dto.PhoneNumber.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email.Trim() != user.Email)
            {
                var email = dto.Email.Trim();
                user.Email = email;
                user.NormalizedEmail = email.ToUpperInvariant();
            }

            if (dto.DefaultAddress != null)
                user.DefaultAddress = dto.DefaultAddress.Trim();

            if (dto.AvatarUrl != null)
                user.AvatarUrl = dto.AvatarUrl.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                message = "Profile updated successfully!",
                profile = new UserProfileDto
                {
                    Id             = user.Id,
                    UserName       = user.UserName ?? string.Empty,
                    FullName       = user.FullName ?? user.UserName ?? string.Empty,
                    Email          = user.Email ?? string.Empty,
                    PhoneNumber    = user.PhoneNumber ?? string.Empty,
                    Role           = roles.FirstOrDefault() ?? "User",
                    DefaultAddress = user.DefaultAddress ?? string.Empty,
                    AvatarUrl      = user.AvatarUrl ?? string.Empty
                }
            });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { message = "User not found or unauthorized." });

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Failed to update password.", errors });
            }

            return Ok(new { message = "Password updated successfully!" });
        }

        [HttpPost("upload-avatar")]
        [Authorize]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { message = "User not found or unauthorized." });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(new { message = "File size must not exceed 2 MB." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Only JPG, PNG, and WEBP image files are allowed." });

            var avatarsFolder = Path.Combine(_env.WebRootPath, "images", "avatars");
            if (!Directory.Exists(avatarsFolder))
                Directory.CreateDirectory(avatarsFolder);

            var fileName = $"avatar_{user.Id}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(avatarsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.AvatarUrl = $"/images/avatars/{fileName}";
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Avatar uploaded successfully!", avatarUrl = user.AvatarUrl });
        }

        // ──────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────────────

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(idClaim))
            {
                var nameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (!string.IsNullOrEmpty(nameClaim))
                {
                    return await _userManager.FindByNameAsync(nameClaim);
                }
                return null;
            }

            return await _userManager.FindByIdAsync(idClaim);
        }

        private string GenerateJwtToken(AppUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier,     user.Id.ToString()),
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
