using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSWebApi.Migrations
{
    /// <inheritdoc />
    public partial class @new : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourierTrackingId",
                table: "Orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CourierTrackingId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
