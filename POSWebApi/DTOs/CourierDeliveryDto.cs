namespace POSWebApi.DTOs
{


    public record CourierDeliveryDto(
    string invoice,
    string recipient_name,
    string recipient_phone,
    string recipient_address,
    decimal amount_to_collect,
    string? note
);


    public record SteadfastOrderRequest(
        string invoice,
        string recipient_name,
        string recipient_phone,
        string recipient_address,
        decimal amount_to_collect,
        string? note
    );


    public record SteadfastStatusResponse(
        int status,
        string delivery_status,
        string message
    );

}
