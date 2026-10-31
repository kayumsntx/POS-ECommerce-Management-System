using System.ComponentModel.DataAnnotations;

namespace POSWebApi.DTOs
{
    
    public class OrderItemRequestDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "ProductId must be a positive integer.")]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }
    }

    
    public class CreateOrderDto
    {
      
        public string? Phone { get; set; }

        
        [Range(0, 100, ErrorMessage = "DiscountPercent must be between 0 and 100.")]
        public decimal DiscountPercent { get; set; }


        [Required]
        public POSWebApi.Models.PaymentMethodType PaymentMethod { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
        public List<OrderItemRequestDto> Items { get; set; } = new();

     
        public DeliveryAddressDto? DeliveryAddress { get; set; }
    }

 
    public class DeliveryAddressDto
    {
        [Required]
        [MaxLength(150)]
        public string RecipientName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;
    }

    
    public class OrderItemResponseDto
    {
        public int    OrderItemId  { get; set; }
        public int    ProductId    { get; set; }
        public string ProductName  { get; set; } = string.Empty;
        public int    Quantity     { get; set; }
        public decimal UnitPrice   { get; set; }
        public decimal Discount    { get; set; }
        public decimal Subtotal    { get; set; }
    }

    public class DeliveryAddressResponseDto
    {
        public int    DeliveryAddressId { get; set; }
        public string RecipientName     { get; set; } = string.Empty;
        public string Phone             { get; set; } = string.Empty;
        public string Address           { get; set; } = string.Empty;
    }

  
    public class OrderResponseDto
    {
        public int       OrderId         { get; set; }
        public int?      UserId          { get; set; }
        public string    OrderType       { get; set; } = string.Empty;
        public string    Status          { get; set; } = string.Empty;
        public string?   Phone           { get; set; }
        public DateTime  OrderDate       { get; set; }
        public decimal   TotalAmount     { get; set; }
        public decimal   DiscountPercent { get; set; }
        public decimal   DiscountAmount  { get; set; }
        public decimal   ShippingAmount  { get; set; }
        public decimal   PayableAmount   { get; set; }
        public string    PaymentMethod   { get; set; } = string.Empty;
        public string    PaymentStatus   { get; set; } = string.Empty;
        public string?   PaymentTransactionId { get; set; }

       
        public string?   GatewayPageURL  { get; set; }

        public DeliveryAddressResponseDto?    DeliveryAddress { get; set; }
        public List<OrderItemResponseDto>     Items           { get; set; } = new();
    }

  
    public class OrderSummaryDto
    {
        public int      OrderId              { get; set; }
        public int?     UserId               { get; set; }
        public string   OrderType            { get; set; } = string.Empty;
        public string   Status               { get; set; } = string.Empty;
        public DateTime OrderDate            { get; set; }
        public decimal  TotalAmount          { get; set; }
        public decimal  DiscountPercent      { get; set; }
        public decimal  DiscountAmount       { get; set; }
        public decimal  ShippingAmount       { get; set; }
        public decimal  PayableAmount        { get; set; }
        public string   PaymentMethod        { get; set; } = string.Empty;
        public string   PaymentStatus        { get; set; } = string.Empty;
        public string?  PaymentTransactionId { get; set; }
        public int      ItemCount            { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new();
    }

    // ── SP flat row shape (one row per order item from stored procedures) ─────
    // Used internally by OrderController to read SP results before mapping to
    // OrderSummaryDto / OrderResponseDto. Not exposed in API responses.
    public class OrderSpRow
    {
        public int      OrderId              { get; set; }
        public int?     UserId               { get; set; }
        public string?  Phone                { get; set; }
        public bool     IsOnlineOrder        { get; set; }
        public int      Status               { get; set; }
        public DateTime OrderDate            { get; set; }
        public decimal  TotalAmount          { get; set; }
        public decimal  DiscountPercent      { get; set; }
        public decimal  DiscountAmount       { get; set; }
        public decimal  ShippingAmount       { get; set; }
        public decimal  PayableAmount        { get; set; }
        public int      PaymentMethod        { get; set; }
        public int      PaymentStatus        { get; set; }
        public string?  PaymentTransactionId { get; set; }

        public int?     DeliveryAddressId    { get; set; }
        public string?  RecipientName        { get; set; }
        public string?  DeliveryPhone        { get; set; }
        public string?  Address              { get; set; }

        public int?     OrderItemId          { get; set; }
        public int?     ProductId            { get; set; }
        public string?  ProductName          { get; set; }
        public int?     Quantity             { get; set; }
        public decimal? UnitPrice            { get; set; }
        public decimal? Discount             { get; set; }
        public decimal? Subtotal             { get; set; }
    }
}
