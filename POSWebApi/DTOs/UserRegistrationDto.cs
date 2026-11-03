namespace POSWebApi.DTOs
{
    public class UserRegistrationDto
    {
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Password { get; set; }
    }
}
