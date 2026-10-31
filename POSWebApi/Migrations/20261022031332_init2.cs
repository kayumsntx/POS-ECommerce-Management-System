using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace POSWebApi.Migrations
{
    /// <inheritdoc />
    public partial class init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[,]
                {
                    { 1, 0, "admin-concurrency-stamp", "fahad@gmail.com", true, false, null, "FAHAD@GMAIL.COM", "FAHAD", "AQAAAAIAAYagAAAAEJFDnlRD/B+GLYeh67OvGO4LTI8AutSrLheN9LckfEblPJBlKyP2kTZZYcpWS/E6fA==", null, false, "ADMIN_SECURITY_STAMP", false, "fahad" },
                    { 2, 0, "user-concurrency-stamp", "user1@gmail.com", true, false, null, "USER1@GMAIL.COM", "USER1", "AQAAAAIAAYagAAAAEB+oNQsKO89E0+C1ShLAqmhh4jHwtgJ5/TXIgJ1fpYbhhj+6oAEWQi+UkITyV01HGw==", null, false, "USER_SECURITY_STAMP", false, "user1" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 }
                });
        }
    }
}
