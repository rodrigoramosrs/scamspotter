using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ScamSpotter.Utils
{
    internal static class UrlUtils
    {
        internal static string RemoveInvalidUriCharsFromUrl(string url)
        {
            string result = Regex.Replace(url, @"[^a-zA-Z0-9\-\.]", ""); //Sanitize uri
            result = Regex.Replace(result, @"\.{2,}", "."); //Remove multiple dots
            return result;
        }

        internal static bool IsValidUrl(string url)
        {
            Uri uriResult;
            bool result = Uri.TryCreate(url, UriKind.Absolute, out uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            return result;
        }
    }
}
