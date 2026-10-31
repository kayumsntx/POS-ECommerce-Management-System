using System.ComponentModel.DataAnnotations;

namespace POSWebApi.DTOs
{
    // ── Create / Update (shared) ──────────────────────────────────────────────

   
    public class ProductDto
    {
        [Required]
        public string ProductName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than zero.")]
        public decimal Cost { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }
    }

    // ── SP result / response shape ────────────────────────────────────────────
    public class ProductResponseDto
    {
        public int      ProductId    { get; set; }
        public string   ProductName  { get; set; } = string.Empty;
        public string?  Description  { get; set; }
        public string   Barcode      { get; set; } = string.Empty;
        public int      CategoryId   { get; set; }
        public string   CategoryName { get; set; } = string.Empty;
        public decimal  Cost         { get; set; }
        public decimal  Price        { get; set; }
        public string?  ImageUrl     { get; set; }
        public DateTime CreatedAt    { get; set; }
    }

    public class UploadResponse
    {
        public string FileName { get; set; } = string.Empty;
    }
}
