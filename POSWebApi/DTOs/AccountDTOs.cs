using System.ComponentModel.DataAnnotations;

namespace POSWebApi.DTOs
{
    // ── Register ──────────────────────────────────────────────────────────────

   
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

      
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    // ── Login ─────────────────────────────────────────────────────────────────

  
    public class LoginDto
    {
      
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    
    public class LoginResponseDto
    {
        public string Token   { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string Email   { get; set; } = string.Empty;
        public string Role    { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    // ── Profile DTOs ──────────────────────────────────────────────────────────

    public class UserProfileDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? DefaultAddress { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class UpdateProfileDto
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? DefaultAddress { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    // ── Password Recovery ─────────────────────────────────────────────────────

    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

   
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
