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
        public string Email   { get; set; } = string.Empty;
        public string Role    { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
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
