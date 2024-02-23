using Microsoft.Extensions.Logging;
using Whois.NET;

namespace ScamSpotter.Services.OSINT
{
    public class WhoIsService
    {
        private ILogger<WhoIsService> _logger;

        public WhoIsService(ILogger<WhoIsService> logger)
        {
            _logger = logger;
        }
        public async Task<WhoisResponse> QueryByIPAddress(string IpAddress)
        {
            var result = await WhoisClient.QueryAsync(IpAddress);

            //Console.WriteLine("{0} - {1}", result.AddressRange.Begin, result.AddressRange.End); // "8.8.8.0 - 8.8.8.255"
            //Console.WriteLine("{0}", result.OrganizationName); // "Google Inc. LVLT-GOGL-8-8-8 (NET-8-8-8-0-1)"
            //Console.WriteLine(string.Join(" > ", result.RespondedServers)); // "whois.iana.org > whois.arin.net" 
            return result;
        }

        public async Task<WhoisResponse> QueryByDomain(string domain)
        {
            var result = await WhoisClient.QueryAsync(domain);

            //Console.WriteLine("{0}", result.OrganizationName); // "Google Inc."
            //Console.WriteLine(string.Join(" > ", result.RespondedServers)); // "whois.iana.org > whois.verisign-grs.com > whois.markmonitor.com" 
            return result;
        }
    }
}
