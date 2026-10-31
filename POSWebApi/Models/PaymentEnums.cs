namespace POSWebApi.Models
{
    /// <summary>
    /// Fixed set of payment methods. Values mirror the PaymentMethods lookup rows
    /// (1 = Cash, 2 = Online) so they can be written straight into Payment.PaymentMethodId.
    /// </summary>
    public enum PaymentMethodType
    {
        Cash   = 1,
        Online = 2
    }

    public enum PaymentStatus
    {
        Unpaid    = 1,  // online payment initiated, awaiting gateway callback
        Paid      = 2,
        Failed    = 3,
        Cancelled = 4
    }
}

