using System.ComponentModel.DataAnnotations;

namespace POSWebApi.DTOs
{
    
    public class InventoryDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
        public int Quantity { get; set; }
    }

   
    public class AdjustQuantityDto
    {
        [Required]
        [Range(int.MinValue, int.MaxValue)]
        public int Adjustment { get; set; }
    }

    // ── SP result / response shape ────────────────────────────────────────────
    public class InventoryResponseDto
    {
        public int      InventoryId  { get; set; }
        public int      ProductId    { get; set; }
        public string   ProductName  { get; set; } = string.Empty;
        public string   Barcode      { get; set; } = string.Empty;
        public int      CategoryId   { get; set; }
        public string   CategoryName { get; set; } = string.Empty;
        public int      Quantity     { get; set; }
        public DateTime UpdatedAt    { get; set; }
    }
}
