-- =====================================================================
-- POSWebApi Database Stored Procedures
-- Use this file to execute or view all stored procedures in SSMS.
-- =====================================================================

USE [testPOSDb];
GO

-- ─────────────────────────────────────────────────────────────────────
-- 1. CATEGORY STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, Name, Description 
    FROM dbo.Categories 
    ORDER BY CategoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetCategoryById
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, Name, Description 
    FROM dbo.Categories 
    WHERE CategoryId = @CategoryId;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 2. PAYMENT METHOD STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllPaymentMethods
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PaymentMethodId, PayMethodType 
    FROM dbo.PaymentMethods 
    ORDER BY PaymentMethodId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetPaymentMethodById
    @PaymentMethodId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PaymentMethodId, PayMethodType 
    FROM dbo.PaymentMethods 
    WHERE PaymentMethodId = @PaymentMethodId;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 3. PRODUCTS STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllProducts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  p.ProductId, p.ProductName, p.Description, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, p.Cost, p.Price, p.ImageUrl, p.CreatedAt
    FROM dbo.Products p
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    ORDER BY p.ProductId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetProductById
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  p.ProductId, p.ProductName, p.Description, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, p.Cost, p.Price, p.ImageUrl, p.CreatedAt
    FROM dbo.Products p
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    WHERE p.ProductId = @ProductId;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 4. INVENTORY STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllInventories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  i.InventoryId, i.ProductId, p.ProductName, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, i.Quantity, i.UpdatedAt
    FROM dbo.Inventories i
    INNER JOIN dbo.Products   p ON p.ProductId  = i.ProductId
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    ORDER BY p.ProductName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetInventoryById
    @InventoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  i.InventoryId, i.ProductId, p.ProductName, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, i.Quantity, i.UpdatedAt
    FROM dbo.Inventories i
    INNER JOIN dbo.Products   p ON p.ProductId  = i.ProductId
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    WHERE i.InventoryId = @InventoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetInventoryByProductId
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) i.InventoryId, i.ProductId, p.ProductName, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, i.Quantity, i.UpdatedAt
    FROM dbo.Inventories i
    INNER JOIN dbo.Products   p ON p.ProductId  = i.ProductId
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    WHERE i.ProductId = @ProductId
    ORDER BY i.InventoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetLowStockInventories
    @Threshold INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  i.InventoryId, i.ProductId, p.ProductName, p.Barcode, p.CategoryId,
            c.Name AS CategoryName, i.Quantity, i.UpdatedAt
    FROM dbo.Inventories i
    INNER JOIN dbo.Products   p ON p.ProductId  = i.ProductId
    INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    WHERE i.Quantity <= @Threshold
    ORDER BY i.Quantity;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 5. ORDER STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllOrders
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  o.OrderId, o.UserId, o.Phone, o.IsOnlineOrder, o.Status, o.OrderDate,
            o.TotalAmount, o.DiscountPercent, o.DiscountAmount, o.ShippingAmount, o.PayableAmount,
            o.PaymentMethod, o.PaymentStatus, o.PaymentTransactionId,
            da.DeliveryAddressId, da.RecipientName, da.Phone AS DeliveryPhone, da.Address,
            oi.OrderItemId, oi.ProductId, p.ProductName, oi.Quantity, oi.UnitPrice, oi.Discount, oi.Subtotal
    FROM dbo.Orders o
    LEFT JOIN dbo.DeliveryAddresses da ON da.OrderId   = o.OrderId
    LEFT JOIN dbo.OrderItems        oi ON oi.OrderId   = o.OrderId
    LEFT JOIN dbo.Products          p  ON p.ProductId  = oi.ProductId
    ORDER BY o.OrderDate DESC, o.OrderId DESC, oi.OrderItemId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetOrderById
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  o.OrderId, o.UserId, o.Phone, o.IsOnlineOrder, o.Status, o.OrderDate,
            o.TotalAmount, o.DiscountPercent, o.DiscountAmount, o.ShippingAmount, o.PayableAmount,
            o.PaymentMethod, o.PaymentStatus, o.PaymentTransactionId,
            da.DeliveryAddressId, da.RecipientName, da.Phone AS DeliveryPhone, da.Address,
            oi.OrderItemId, oi.ProductId, p.ProductName, oi.Quantity, oi.UnitPrice, oi.Discount, oi.Subtotal
    FROM dbo.Orders o
    LEFT JOIN dbo.DeliveryAddresses da ON da.OrderId   = o.OrderId
    LEFT JOIN dbo.OrderItems        oi ON oi.OrderId   = o.OrderId
    LEFT JOIN dbo.Products          p  ON p.ProductId  = oi.ProductId
    WHERE o.OrderId = @OrderId
    ORDER BY oi.OrderItemId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetOrdersByUserId
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  o.OrderId, o.UserId, o.Phone, o.IsOnlineOrder, o.Status, o.OrderDate,
            o.TotalAmount, o.DiscountPercent, o.DiscountAmount, o.ShippingAmount, o.PayableAmount,
            o.PaymentMethod, o.PaymentStatus, o.PaymentTransactionId,
            da.DeliveryAddressId, da.RecipientName, da.Phone AS DeliveryPhone, da.Address,
            oi.OrderItemId, oi.ProductId, p.ProductName, oi.Quantity, oi.UnitPrice, oi.Discount, oi.Subtotal
    FROM dbo.Orders o
    LEFT JOIN dbo.DeliveryAddresses da ON da.OrderId   = o.OrderId
    LEFT JOIN dbo.OrderItems        oi ON oi.OrderId   = o.OrderId
    LEFT JOIN dbo.Products          p  ON p.ProductId  = oi.ProductId
    WHERE o.UserId = @UserId
    ORDER BY o.OrderDate DESC, o.OrderId DESC, oi.OrderItemId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetOrdersByDateRange
    @StartDate DATETIME2,
    @EndDate   DATETIME2,
    @UserId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  o.OrderId, o.UserId, o.Phone, o.IsOnlineOrder, o.Status, o.OrderDate,
            o.TotalAmount, o.DiscountPercent, o.DiscountAmount, o.ShippingAmount, o.PayableAmount,
            o.PaymentMethod, o.PaymentStatus, o.PaymentTransactionId,
            da.DeliveryAddressId, da.RecipientName, da.Phone AS DeliveryPhone, da.Address,
            oi.OrderItemId, oi.ProductId, p.ProductName, oi.Quantity, oi.UnitPrice, oi.Discount, oi.Subtotal
    FROM dbo.Orders o
    LEFT JOIN dbo.DeliveryAddresses da ON da.OrderId   = o.OrderId
    LEFT JOIN dbo.OrderItems        oi ON oi.OrderId   = o.OrderId
    LEFT JOIN dbo.Products          p  ON p.ProductId  = oi.ProductId
    WHERE o.OrderDate >= @StartDate
      AND o.OrderDate <  @EndDate
      AND (@UserId IS NULL OR o.UserId = @UserId)
    ORDER BY o.OrderDate DESC, o.OrderId DESC, oi.OrderItemId;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 6. RETURN STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllReturns
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  r.ReturnId, r.OrderId, r.ReturnDate,
            ri.ReturnItemId, ri.OrderItemId, oi.ProductId, p.ProductName,
            ri.Quantity, oi.UnitPrice, ri.ReturnReason
    FROM dbo.Returns r
    LEFT JOIN dbo.ReturnItems ri ON ri.ReturnId    = r.ReturnId
    LEFT JOIN dbo.OrderItems  oi ON oi.OrderItemId = ri.OrderItemId
    LEFT JOIN dbo.Products    p  ON p.ProductId    = oi.ProductId
    ORDER BY r.ReturnDate DESC, r.ReturnId DESC, ri.ReturnItemId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetReturnById
    @ReturnId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  r.ReturnId, r.OrderId, r.ReturnDate,
            ri.ReturnItemId, ri.OrderItemId, oi.ProductId, p.ProductName,
            ri.Quantity, oi.UnitPrice, ri.ReturnReason
    FROM dbo.Returns r
    LEFT JOIN dbo.ReturnItems ri ON ri.ReturnId    = r.ReturnId
    LEFT JOIN dbo.OrderItems  oi ON oi.OrderItemId = ri.OrderItemId
    LEFT JOIN dbo.Products    p  ON p.ProductId    = oi.ProductId
    WHERE r.ReturnId = @ReturnId
    ORDER BY ri.ReturnItemId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetReturnsByOrderId
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  r.ReturnId, r.OrderId, r.ReturnDate,
            ri.ReturnItemId, ri.OrderItemId, oi.ProductId, p.ProductName,
            ri.Quantity, oi.UnitPrice, ri.ReturnReason
    FROM dbo.Returns r
    LEFT JOIN dbo.ReturnItems ri ON ri.ReturnId    = r.ReturnId
    LEFT JOIN dbo.OrderItems  oi ON oi.OrderItemId = ri.OrderItemId
    LEFT JOIN dbo.Products    p  ON p.ProductId    = oi.ProductId
    WHERE r.OrderId = @OrderId
    ORDER BY r.ReturnDate DESC, r.ReturnId DESC, ri.ReturnItemId;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 7. ROLES & USERS STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE dbo.sp_GetAllRoles
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, ISNULL(Name, '') AS Name 
    FROM dbo.AspNetRoles 
    ORDER BY Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetUserRolesByPhone
    @PhoneNumber NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  u.Id AS UserId, u.UserName, u.Email, u.PhoneNumber, r.Name AS RoleName
    FROM dbo.AspNetUsers u
    LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
    LEFT JOIN dbo.AspNetRoles     r  ON r.Id      = ur.RoleId
    WHERE u.Id = (SELECT TOP (1) Id FROM dbo.AspNetUsers WHERE PhoneNumber = @PhoneNumber ORDER BY Id);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetAllUsersWithRoles
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  u.Id AS UserId, u.UserName, u.Email, u.PhoneNumber, r.Name AS RoleName
    FROM dbo.AspNetUsers u
    LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
    LEFT JOIN dbo.AspNetRoles     r  ON r.Id      = ur.RoleId
    ORDER BY u.Id, r.Name;
END;
GO

