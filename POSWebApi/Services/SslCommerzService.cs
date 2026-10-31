using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POSWebApi.Services
{
    // ── Configuration (bound from the "SslCommerz" section of appsettings.json) ──
    public class SslCommerzSettings
    {
        public const string SectionName = "SslCommerz";

        public string StoreId       { get; set; } = string.Empty;
        public string StorePassword { get; set; } = string.Empty;
        public bool   IsSandbox     { get; set; } = true;

        public string SessionUrl    { get; set; } = "https://sandbox.sslcommerz.com/gwprocess/v4/api.php";
        public string ValidationUrl { get; set; } = "https://sandbox.sslcommerz.com/validator/api/validationserverAPI.php";

        public string SuccessUrl    { get; set; } = string.Empty;
        public string FailUrl       { get; set; } = string.Empty;
        public string CancelUrl     { get; set; } = string.Empty;
        public string IpnUrl        { get; set; } = string.Empty;

        /// <summary>Optional front-end page the browser is redirected to after Success/Fail/Cancel.</summary>
        public string? PaymentResultUrl { get; set; }
    }

    // ── Request / result models ──────────────────────────────────────────────
    public class SslCommerzSessionRequest
    {
        public string  TransactionId    { get; set; } = string.Empty;
        public decimal TotalAmount      { get; set; }
        public string  Currency         { get; set; } = "BDT";
        public string  CustomerName     { get; set; } = string.Empty;
        public string  CustomerEmail    { get; set; } = string.Empty;
        public string  CustomerPhone    { get; set; } = string.Empty;
        public string  CustomerAddress  { get; set; } = string.Empty;
        public string  ProductName      { get; set; } = "POS Order";
        public bool    RequiresShipping { get; set; }

        /// <summary>Optional base URL (e.g. "http://localhost:5109") detected from the current HTTP request.</summary>
        public string? CallbackBaseUrl  { get; set; }
    }

    public class SslCommerzSessionResult
    {
        public bool    IsSuccess      { get; set; }
        public string? GatewayPageUrl { get; set; }
        public string? Error          { get; set; }
    }

    public class SslCommerzValidationResult
    {
        public string  Status            { get; set; } = string.Empty;
        public string  TransactionId     { get; set; } = string.Empty;
        public decimal Amount            { get; set; }
        public string  Currency          { get; set; } = string.Empty;
        public string? BankTransactionId { get; set; }

        /// <summary>SSLCommerz reports VALID (first validation) or VALIDATED (already validated).</summary>
        public bool IsValid =>
            Status.Equals("VALID", StringComparison.OrdinalIgnoreCase) ||
            Status.Equals("VALIDATED", StringComparison.OrdinalIgnoreCase);
    }

    // ── Service contract ─────────────────────────────────────────────────────
    public interface ISslCommerzService
    {
        Task<SslCommerzSessionResult> CreateSessionAsync(SslCommerzSessionRequest request, CancellationToken ct = default);

        /// <summary>Calls the validation API. Returns null if the gateway could not be reached or replied with garbage.</summary>
        Task<SslCommerzValidationResult?> ValidateAsync(string valId, CancellationToken ct = default);
    }

    // ── Implementation ───────────────────────────────────────────────────────
    public class SslCommerzService : ISslCommerzService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private readonly HttpClient _http;
        private readonly SslCommerzSettings _s;
        private readonly ILogger<SslCommerzService> _logger;

        public SslCommerzService(HttpClient http, IOptions<SslCommerzSettings> options, ILogger<SslCommerzService> logger)
        {
            _http   = http;
            _s      = options.Value;
            _logger = logger;
        }

        public async Task<SslCommerzSessionResult> CreateSessionAsync(SslCommerzSessionRequest r, CancellationToken ct = default)
        {
            string successUrl = AppendTranId(ResolveCallbackUrl(_s.SuccessUrl, r.CallbackBaseUrl, "/api/payment/success"), r.TransactionId);
            string failUrl    = AppendTranId(ResolveCallbackUrl(_s.FailUrl,    r.CallbackBaseUrl, "/api/payment/fail"),    r.TransactionId);
            string cancelUrl  = AppendTranId(ResolveCallbackUrl(_s.CancelUrl,  r.CallbackBaseUrl, "/api/payment/cancel"),  r.TransactionId);
            string ipnUrl     = ResolveCallbackUrl(_s.IpnUrl, r.CallbackBaseUrl, "/api/payment/ipn");

            var form = new Dictionary<string, string>
            {
                ["store_id"]         = _s.StoreId,
                ["store_passwd"]     = _s.StorePassword,
                ["total_amount"]     = r.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["currency"]         = r.Currency,
                ["tran_id"]          = r.TransactionId,
                ["success_url"]      = successUrl,
                ["fail_url"]         = failUrl,
                ["cancel_url"]       = cancelUrl,
                ["ipn_url"]          = ipnUrl,
                ["cus_name"]         = r.CustomerName,
                ["cus_email"]        = r.CustomerEmail,
                ["cus_add1"]         = r.CustomerAddress,
                ["cus_city"]         = "Dhaka",
                ["cus_postcode"]     = "1000",
                ["cus_country"]      = "Bangladesh",
                ["cus_phone"]        = r.CustomerPhone,
                ["shipping_method"]  = r.RequiresShipping ? "Courier" : "NO",
                ["product_name"]     = r.ProductName,
                ["product_category"] = "General",
                ["product_profile"]  = r.RequiresShipping ? "physical-goods" : "general",
            };

            if (r.RequiresShipping)
            {
                form["ship_name"]     = r.CustomerName;
                form["ship_add1"]     = r.CustomerAddress;
                form["ship_city"]     = "Dhaka";
                form["ship_postcode"] = "1000";
                form["ship_country"]  = "Bangladesh";
            }

            try
            {
                using var response = await _http.PostAsync(_s.SessionUrl, new FormUrlEncodedContent(form), ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SSLCommerz session HTTP {Status}: {Body}", (int)response.StatusCode, body);
                    return new SslCommerzSessionResult { Error = $"Payment gateway returned HTTP {(int)response.StatusCode}." };
                }

                var dto = JsonSerializer.Deserialize<SessionResponse>(body, JsonOptions);
                if (dto == null ||
                    !string.Equals(dto.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(dto.GatewayPageUrl))
                {
                    _logger.LogWarning("SSLCommerz session rejected: {Reason}", dto?.FailedReason);
                    return new SslCommerzSessionResult { Error = dto?.FailedReason ?? "Payment gateway rejected the session request." };
                }

                return new SslCommerzSessionResult { IsSuccess = true, GatewayPageUrl = dto.GatewayPageUrl };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogError(ex, "SSLCommerz session request failed for {TranId}", r.TransactionId);
                return new SslCommerzSessionResult { Error = "Could not reach the payment gateway." };
            }
        }

        public async Task<SslCommerzValidationResult?> ValidateAsync(string valId, CancellationToken ct = default)
        {
            var url = $"{_s.ValidationUrl}?val_id={Uri.EscapeDataString(valId)}" +
                      $"&store_id={Uri.EscapeDataString(_s.StoreId)}" +
                      $"&store_passwd={Uri.EscapeDataString(_s.StorePassword)}" +
                      "&format=json";
            try
            {
                using var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SSLCommerz validation HTTP {Status} for val_id {ValId}", (int)response.StatusCode, valId);
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                var dto  = JsonSerializer.Deserialize<ValidationResponse>(body, JsonOptions);
                if (dto == null) return null;

                var amountStr = !string.IsNullOrWhiteSpace(dto.CurrencyAmount) ? dto.CurrencyAmount : dto.Amount;
                decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);

                var currency = !string.IsNullOrWhiteSpace(dto.CurrencyType) ? dto.CurrencyType : dto.Currency;

                return new SslCommerzValidationResult
                {
                    Status            = dto.Status ?? string.Empty,
                    TransactionId     = dto.TranId ?? string.Empty,
                    Amount            = amount,
                    Currency          = currency ?? "BDT",
                    BankTransactionId = dto.BankTranId
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogError(ex, "SSLCommerz validation failed for val_id {ValId}", valId);
                return null;
            }
        }

        private static string ResolveCallbackUrl(string configuredUrl, string? requestBaseUrl, string path)
        {
            // If running on localhost and we know the exact scheme/port from the active request, use it
            // so port or http/https mismatches never break the browser redirect.
            if (!string.IsNullOrWhiteSpace(requestBaseUrl) &&
                (string.IsNullOrWhiteSpace(configuredUrl) || configuredUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                return $"{requestBaseUrl.TrimEnd('/')}{path}";
            }

            return configuredUrl;
        }

        private static string AppendTranId(string url, string tranId)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(tranId))
                return url;
            var sep = url.Contains('?') ? "&" : "?";
            return $"{url}{sep}tran_id={Uri.EscapeDataString(tranId)}";
        }

        // ── Raw gateway JSON shapes ──────────────────────────────────────────
        private class SessionResponse
        {
            [JsonPropertyName("status")]         public string? Status         { get; set; }
            [JsonPropertyName("failedreason")]   public string? FailedReason   { get; set; }
            [JsonPropertyName("GatewayPageURL")] public string? GatewayPageUrl { get; set; }
        }

        private class ValidationResponse
        {
            [JsonPropertyName("status")]          public string? Status         { get; set; }
            [JsonPropertyName("tran_id")]         public string? TranId         { get; set; }
            [JsonPropertyName("amount")]          public string? Amount         { get; set; }
            [JsonPropertyName("currency_amount")] public string? CurrencyAmount { get; set; }
            [JsonPropertyName("currency")]        public string? Currency       { get; set; }
            [JsonPropertyName("currency_type")]   public string? CurrencyType   { get; set; }
            [JsonPropertyName("bank_tran_id")]    public string? BankTranId     { get; set; }
        }
    }
}
