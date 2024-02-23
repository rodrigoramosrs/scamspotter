using Microsoft.Extensions.Logging;
using ScamSpotter.Models.OSINT;
using System.Text.Json;

namespace ScamSpotter.Services.OSINT
{
    public class CrtShService
    {
        private ILogger<CrtShService> _logger;
        private RequestService _requestService;
        private const int MaxAttempts = 10;
        private const int DelayBetweenAttempts = 20000; // in milliseconds

        public CrtShService(ILogger<CrtShService> logger, RequestService requestService)
        {
            _logger = logger;
            _requestService = requestService;
        }
        internal async Task<IEnumerable<CrtShSearchResultModel>> DoSearch(string term)
        {
            var url = $"https://crt.sh/?q=*{term}%25&output=json";

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    var response = await _requestService.SendRequest(new Uri(url), HttpMethod.Get);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<IEnumerable<CrtShSearchResultModel>>(responseBody) ?? new List<CrtShSearchResultModel>();
                }
                catch (Exception)
                {
                    if (attempt == MaxAttempts - 1)
                    {
                        throw;
                    }

                    await Task.Delay(DelayBetweenAttempts);
                }
            }

            throw new InvalidOperationException("Failed to get search results after maximum attempts.");
        }
    }
}

