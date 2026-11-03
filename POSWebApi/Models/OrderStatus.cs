namespace POSWebApi.Models
{
    public enum OrderStatus
    {
        Pending   = 1,  // Online order awaiting staff approval
        Confirmed = 2,  // Payment confirmed (legacy / future use)
        Approved=3,
        Cancelled = 4,   // Order cancelled
        Delivered = 5
    }
}
