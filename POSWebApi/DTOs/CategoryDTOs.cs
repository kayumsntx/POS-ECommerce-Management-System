namespace POSWebApi.DTOs
{
    // Used for Create / Update requests
    public class CategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // Used as the SP result / response shape
    public class CategoryResponseDto
    {
        public int     CategoryId  { get; set; }
        public string  Name        { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
