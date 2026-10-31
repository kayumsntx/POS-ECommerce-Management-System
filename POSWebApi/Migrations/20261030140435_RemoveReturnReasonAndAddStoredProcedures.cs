using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSWebApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReturnReasonAndAddStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Returns");

            // ── Stored procedures used by every GET endpoint ─────────────────
            foreach (var sql in StoredProcedureScripts.Create)
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in StoredProcedureScripts.Names)
                migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS dbo.{name};");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Returns",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    RefundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    ReturnId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.RefundId);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Returns_ReturnId",
                        column: x => x.ReturnId,
                        principalTable: "Returns",
                        principalColumn: "ReturnId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReturnId",
                table: "Refunds",
                column: "ReturnId");
        }
    }

    /// <summary>
    /// T-SQL for all read stored procedures. Column names must match the *SpRow classes in DTOs/StoredProcedureRows.cs.
    /// </summary>
    internal static class StoredProcedureScripts
    {
        // Shared SELECT for order procedures: one row per order item (LEFT JOINs keep orders without items/address).
        private const string OrderSelect = @"
    SELECT  o.OrderId, o.UserId, o.Phone, o.IsOnlineOrder, o.Status, o.OrderDate,
            o.TotalAmount, o.DiscountPercent, o.DiscountAmount, o.ShippingAmount, o.PayableAmount,
            o.PaymentMethod, o.PaymentStatus, o.PaymentTransactionId,
            da.DeliveryAddressId, da.RecipientName, da.Phone AS DeliveryPhone, da.Address,
            oi.OrderItemId, oi.ProductId, p.ProductName, oi.Quantity, oi.UnitPrice, oi.Discount, oi.Subtotal
    FROM dbo.Orders o
    LEFT JOIN dbo.DeliveryAddresses da ON da.OrderId   = o.OrderId
    LEFT JOIN dbo.OrderItems        oi ON oi.OrderId   = o.OrderId
    LEFT JOIN dbo.Products          p  ON p.ProductId  = oi.ProductId";

        private const string ReturnSelect = @"
    SELECT  r.ReturnId, r.OrderId, r.ReturnDate,
            ri.ReturnItemId, ri.OrderItemId, oi.ProductId, p.ProductName,
            ri.Quantity, oi.UnitPrice, ri.ReturnReason
    FROM dbo.Returns r
    LEFT JOIN dbo.ReturnItems ri ON ri.ReturnId    = r.ReturnId
    LEFT JOIN dbo.OrderItems  oi ON oi.OrderItemId = ri.OrderItemId
    LEFT JOIN dbo.Products    p  ON p.ProductId    = oi.ProductId";

        private const string ProductSelect = @"
    SELECT  p.ProductId, p.ProductName, p.Description, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, p.Cost, p.Price, p.ImageUrl, p.CreatedAt
    FROM dbo.Products p
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId";

        private const string InventorySelect = @"
    SELECT  i.InventoryId, i.ProductId, p.ProductName, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, i.Quantity, i.UpdatedAt
    FROM dbo.Inventories i
    INNER JOIN dbo.Products   p ON p.ProductId  = i.ProductId
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId";

        private const string UserRoleSelect = @"
    SELECT  u.Id AS UserId, u.UserName, u.Email, u.PhoneNumber, r.Name AS RoleName
    FROM dbo.AspNetUsers u
    LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
    LEFT JOIN dbo.AspNetRoles     r  ON r.Id      = ur.RoleId";

        private static readonly (string Name, string Body)[] Procedures =
        {
            // ── Category ──
            ("sp_GetAllCategories", @"
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, Name, Description FROM dbo.Categories ORDER BY CategoryId;
END"),
            ("sp_GetCategoryById", @"
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, Name, Description FROM dbo.Categories WHERE CategoryId = @CategoryId;
END"),

            // ── PaymentMethod ──
            ("sp_GetAllPaymentMethods", @"
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PaymentMethodId, PayMethodType FROM dbo.PaymentMethods ORDER BY PaymentMethodId;
END"),
            ("sp_GetPaymentMethodById", @"
    @PaymentMethodId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PaymentMethodId, PayMethodType FROM dbo.PaymentMethods WHERE PaymentMethodId = @PaymentMethodId;
END"),

            // ── Products ──
            ("sp_GetAllProducts", $@"
AS
BEGIN
    SET NOCOUNT ON;{ProductSelect}
    ORDER BY p.ProductId;
END"),
            ("sp_GetProductById", $@"
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;{ProductSelect}
    WHERE p.ProductId = @ProductId;
END"),

            // ── Inventory ──
            ("sp_GetAllInventories", $@"
AS
BEGIN
    SET NOCOUNT ON;{InventorySelect}
    ORDER BY p.ProductName;
END"),
            ("sp_GetInventoryById", $@"
    @InventoryId INT
AS
BEGIN
    SET NOCOUNT ON;{InventorySelect}
    WHERE i.InventoryId = @InventoryId;
END"),
            ("sp_GetInventoryByProductId", $@"
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;{InventorySelect.Replace("SELECT  i.InventoryId", "SELECT TOP (1) i.InventoryId")}
    WHERE i.ProductId = @ProductId
    ORDER BY i.InventoryId;
END"),
            ("sp_GetLowStockInventories", $@"
    @Threshold INT = 10
AS
BEGIN
    SET NOCOUNT ON;{InventorySelect}
    WHERE i.Quantity <= @Threshold
    ORDER BY i.Quantity;
END"),

            // ── Orders ──
            ("sp_GetAllOrders", $@"
AS
BEGIN
    SET NOCOUNT ON;{OrderSelect}
    ORDER BY o.OrderDate DESC, o.OrderId DESC, oi.OrderItemId;
END"),
            ("sp_GetOrderById", $@"
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;{OrderSelect}
    WHERE o.OrderId = @OrderId
    ORDER BY oi.OrderItemId;
END"),
            ("sp_GetOrdersByUserId", $@"
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;{OrderSelect}
    WHERE o.UserId = @UserId
    ORDER BY o.OrderDate DESC, o.OrderId DESC, oi.OrderItemId;
END"),
            // @EndDate is EXCLUSIVE (API passes endDate + 1 day). @UserId NULL = all users.
            ("sp_GetOrdersByDateRange", $@"
    @StartDate DATETIME2,
    @EndDate   DATETIME2,
    @UserId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;{OrderSelect}
    WHERE o.OrderDate >= @StartDate
      AND o.OrderDate <  @EndDate
      AND (@UserId IS NULL OR o.UserId = @UserId)
    ORDER BY o.OrderDate DESC, o.OrderId DESC, oi.OrderItemId;
END"),

            // ── Returns ──
            ("sp_GetAllReturns", $@"
AS
BEGIN
    SET NOCOUNT ON;{ReturnSelect}
    ORDER BY r.ReturnDate DESC, r.ReturnId DESC, ri.ReturnItemId;
END"),
            ("sp_GetReturnById", $@"
    @ReturnId INT
AS
BEGIN
    SET NOCOUNT ON;{ReturnSelect}
    WHERE r.ReturnId = @ReturnId
    ORDER BY ri.ReturnItemId;
END"),
            ("sp_GetReturnsByOrderId", $@"
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;{ReturnSelect}
    WHERE r.OrderId = @OrderId
    ORDER BY r.ReturnDate DESC, r.ReturnId DESC, ri.ReturnItemId;
END"),

            // ── Roles / Users ──
            ("sp_GetAllRoles", @"
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, ISNULL(Name, '') AS Name FROM dbo.AspNetRoles ORDER BY Id;
END"),
            ("sp_GetUserRolesByPhone", $@"
    @PhoneNumber NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;{UserRoleSelect}
    WHERE u.Id = (SELECT TOP (1) Id FROM dbo.AspNetUsers WHERE PhoneNumber = @PhoneNumber ORDER BY Id);
END"),
            ("sp_GetAllUsersWithRoles", $@"
AS
BEGIN
    SET NOCOUNT ON;{UserRoleSelect}
    ORDER BY u.Id, r.Name;
END"),
        };

        public static IEnumerable<string> Names => Procedures.Select(p => p.Name);

        public static IEnumerable<string> Create =>
            Procedures.Select(p => $"CREATE OR ALTER PROCEDURE dbo.{p.Name}{p.Body}");
    }
}
