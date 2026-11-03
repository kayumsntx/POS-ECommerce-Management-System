using POSWebApi.DTOs;

namespace POSWebApi.Services
{
    public class SteadfastService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public SteadfastService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            var baseUrl = _configuration["SteadfastSettings:BaseUrl"];
            var apiKey = _configuration["SteadfastSettings:ApiKey"];
            var secretKey = _configuration["SteadfastSettings:SecretKey"];

            _httpClient.BaseAddress = new Uri(baseUrl!);
            _httpClient.DefaultRequestHeaders.Add("Api-Key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("Secret-Key", secretKey);
        }


        public async Task<string?> CreateOrderAsync(SteadfastOrderRequest orderRequest)
        {
            var response = await _httpClient.PostAsJsonAsync("create_order", orderRequest);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Steadfast API Error: {response.StatusCode} - {errorContent}");
        }


        public async Task<string?> GetTrackingStatusAsync(string consignmentId)
        {
            var response = await _httpClient.GetAsync($"status_by_cid/{consignmentId}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SteadfastStatusResponse>();
                return result?.delivery_status?.ToUpper();
            }

            return null;
        }
    }
}
