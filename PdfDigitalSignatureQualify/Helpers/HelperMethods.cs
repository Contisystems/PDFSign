using System;
using System.Configuration;
using System.Net;
using System.Net.Http;

namespace PdfDigitalSignatureQualify.Helpers
{
    internal class HelperMethods
    {
        internal static HttpClient CreateHttpClient()
        {
            var _isToUseProxy = bool.Parse(ConfigurationManager.AppSettings["IsToUseProxy"]);
            var _proxyIP = ConfigurationManager.AppSettings["ProxyIP"];
            var _proxyPort = int.Parse(ConfigurationManager.AppSettings["ProxyPort"]);
            var _digitalSignBaseAddress = ConfigurationManager.AppSettings["DigitalSignBaseAddress"];

            if (_isToUseProxy)
            {
                var proxy = new WebProxy(_proxyIP, _proxyPort)
                {
                    BypassProxyOnLocal = false,
                    UseDefaultCredentials = false
                };

                // Create a client handler that uses the proxy
                var httpClientHandler = new HttpClientHandler
                {
                    Proxy = proxy,
                };

                // Disable SSL verification
                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                // Finally, create the HTTP client object
                var client = new HttpClient(handler: httpClientHandler, disposeHandler: true);
                client.BaseAddress = new Uri(_digitalSignBaseAddress);
                return client;
            }
            else
            {
                return new HttpClient
                {
                    BaseAddress = new Uri(_digitalSignBaseAddress)
                };
            }
        }
    }
}
