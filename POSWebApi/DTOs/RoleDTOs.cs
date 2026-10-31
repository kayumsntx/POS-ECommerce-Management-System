using System.ComponentModel.DataAnnotations;

namespace POSWebApi.DTOs
{
    // ── Create Role ───────────────────────────────────────────────────────────

   
    public class CreateRoleDto
    {
        [Required]
        public string RoleName { get; set; } = string.Empty;
    }

    // ── Assign / Remove Role ──────────────────────────────────────────────────

  
    public class UserRoleDto
    {
      
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string RoleName { get; set; } = string.Empty;
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public class RoleResponseDto
    {
        public string Id       { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }


    public class UserWithRolesDto
    {
        public string UserName    { get; set; } = string.Empty;
        public string Email       { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();
    }

    // ── SP flat row shapes ────────────────────────────────────────────────────
    // Used internally by RoleController to read SP results before mapping to
    // RoleResponseDto / UserWithRolesDto. Not exposed in API responses.

    public class RoleSpRow
    {
        public int    Id   { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class UserRoleSpRow
    {
        public int     UserId      { get; set; }
        public string? UserName    { get; set; }
        public string? Email       { get; set; }
        public string? PhoneNumber { get; set; }
        public string? RoleName    { get; set; }
    }
}
