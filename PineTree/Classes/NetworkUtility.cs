using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PineTree.Classes
{
    public class NetworkUtility
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
            MaxResponseContentBufferSize = 1024
        };

        public async Task<string> GetPublicIPAddressAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            string[] endpoints = { "https://api.ipify.org", "https://checkip.amazonaws.com" };
            foreach (string endpoint in endpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (HttpResponseMessage response = await Client.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        string value = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                        IPAddress address;
                        if (IPAddress.TryParse(value, out address)
                            && (address.AddressFamily == AddressFamily.InterNetwork
                                || address.AddressFamily == AddressFamily.InterNetworkV6)
                            && !IPAddress.IsLoopback(address)
                            && !address.Equals(IPAddress.Any)
                            && !address.Equals(IPAddress.IPv6Any))
                        {
                            return address.ToString();
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("공인 IP 조회 시간 초과");
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"공인 IP 조회 실패: {ex.Message}");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return "0.0.0.0";
        }
    }
}
