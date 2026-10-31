namespace POSWebApi.DTOs
{
    public class CreatePaymentMethodDto
    {
        public string PayMethodType { get; set; } = null!;
    }

    // SP result / response shape
    public class PaymentMethodResponseDto
    {
        public int    PaymentMethodId { get; set; }
        public string PayMethodType   { get; set; } = string.Empty;
    }
}
