using System.ComponentModel.DataAnnotations;

namespace POSWebApi.DTOs
{
    public class ReturnItemRequestDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "OrderItemId must be a positive integer.")]
        public int OrderItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? ReturnReason { get; set; }
    }

    public class CreateReturnDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "OrderId must be a positive integer.")]
        public int OrderId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "A return must contain at least one return item.")]
        public List<ReturnItemRequestDto> Items { get; set; } = new();
    }

    public class UpdateReturnDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "A return must contain at least one return item.")]
        public List<ReturnItemRequestDto> Items { get; set; } = new();
    }

    public class UpdateReturnItemDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? ReturnReason { get; set; }
    }

    public class ReturnItemResponseDto
    {
        public int     ReturnItemId { get; set; }
        public int     OrderItemId  { get; set; }
        public int     ProductId    { get; set; }
        public string  ProductName  { get; set; } = string.Empty;
        public int     Quantity     { get; set; }
        public decimal UnitPrice    { get; set; }
        public string? ReturnReason { get; set; }
    }

    public class ReturnResponseDto
    {
        public int      ReturnId   { get; set; }
        public int      OrderId    { get; set; }
        public DateTime ReturnDate { get; set; }

        public List<ReturnItemResponseDto> Items { get; set; } = new();
    }

    // ── SP flat row shape (one row per return item from stored procedures) ────
    // Used internally by ReturnController to read SP results before mapping to
    // ReturnResponseDto. Not exposed in API responses.
    public class ReturnSpRow
    {
        public int      ReturnId     { get; set; }
        public int      OrderId      { get; set; }
        public DateTime ReturnDate   { get; set; }

        public int?     ReturnItemId { get; set; }
        public int?     OrderItemId  { get; set; }
        public int?     ProductId    { get; set; }
        public string?  ProductName  { get; set; }
        public int?     Quantity     { get; set; }
        public decimal? UnitPrice    { get; set; }
        public string?  ReturnReason { get; set; }
    }
}


