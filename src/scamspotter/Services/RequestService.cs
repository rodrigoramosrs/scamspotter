using Microsoft.Extensions.Logging;
using System.Net.Security;

namespace ScamSpotter.Services
{
    public class RequestService
    {
        private static readonly HttpClient _internalClient;
        private readonly Random _rand = new Random();
        private readonly List<string> _userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537",
            "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:54.0) Gecko/20100101 Firefox/54.0",
            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3",
            // Add more user agents as needed
        };
        private readonly ILogger<RequestService> _logger;
        private const int MaxAttempts = 10;
        private const int DelayBetweenAttempts = 10000; // in milliseconds

        public Version HttpVersion { get; private set; }

        private string GetRandomUseragent()
        {
            return _userAgents[_rand.Next(_userAgents.Count)];
        }
        static RequestService()
        {
            var socketsHttpHandler = new SocketsHttpHandler()
            {
                //PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
                //MaxConnectionsPerServer = 100,
                //KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                //EnableMultipleHttp2Connections = true,
                SslOptions = new SslClientAuthenticationOptions
                {
                    // Leave certs unvalidated for debugging
                    RemoteCertificateValidationCallback = delegate { return true; }
                },
            };

            _internalClient = new HttpClient(socketsHttpHandler);
        }

        public RequestService(ILogger<RequestService> logger)
        {
            _logger = logger; ;
        }

        internal async Task<HttpResponseMessage> SendRequest(Uri uri, HttpMethod method)
        {
            CancellationTokenSource cts = new CancellationTokenSource(Global.GlobalSettings.RequestTimeoutSeconds * 1000);

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    using (var request = new HttpRequestMessage(method, uri))
                    {
                        request.Headers.Add("User-Agent", GetRandomUseragent());
                        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                        request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
                        return await _internalClient.SendAsync(request).WaitAsync(cts.Token);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError("Exception on RequestService.SendRequest.TaskCanceled", ex);
                    break;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxAttempts - 1)
                    {
                        break;
                    }

                    await Task.Delay(DelayBetweenAttempts);
                    _logger.LogError("Exception on RequestService.SendRequest", ex);
                }
            }

            var exception = new InvalidOperationException("Failed to get search results after maximum attempts.");

            _logger.LogError("Exception on RequestService.SendRequest.MaximumAttempts", exception);

            throw exception;

        }
    }
}
