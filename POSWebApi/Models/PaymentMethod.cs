namespace POSWebApi.Models
{
    public class PaymentMethod
    {
        public int PaymentMethodId { get; set; }
        public string PayMethodType { get; set; } = null!;
        public ICollection<Payment> Payments { get; set; }
           = new List<Payment>();
    }
}
