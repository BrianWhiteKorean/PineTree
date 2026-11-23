using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    internal sealed class AddressSearchService
    {
        internal const int PageSize = 20;
        private const string DefaultEndpoint = "https://business.juso.go.kr/addrlink/addrLinkApi.do";
        private const string ApprovalKeyConfigName = "주소검색 API 승인키";
        private static readonly HttpClient SharedClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private readonly HttpClient _client;
        private readonly string _endpoint;
        private readonly string _approvalKey;

        internal AddressSearchService(HttpClient client, string endpoint, string approvalKey)
        {
            _client = client;
            _endpoint = endpoint;
            _approvalKey = approvalKey;
        }

        internal static async Task<AddressSearchService> FromSettingsAsync(CancellationToken token)
        {
            string endpoint = ConfigurationManager.AppSettings["AddressSearchEndpoint"];
            string key;
            try
            {
                key = await Task.Run(ReadApprovalKey, token).ConfigureAwait(false);
            }
            catch (MySqlException)
            {
                token.ThrowIfCancellationRequested();
                throw new AddressSearchException("DB에서 도로명주소 검색 API 승인키를 읽을 수 없습니다. DB 연결과 설정 테이블 조회 권한을 확인해 주세요.");
            }

            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(key))
                throw new AddressSearchException("DB의 '주소검색 API 승인키' 설정을 한 건 등록하고 승인키를 입력해 주세요.");

            return new AddressSearchService(SharedClient,
                string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim(), key);
        }

        private static string ReadApprovalKey()
        {
            const string query = @"
SELECT MAX(ConfigValue1)
FROM PineTree.Config
WHERE ConfigName1 = @ConfigName
HAVING COUNT(*) = 1";
            return Convert.ToString(DB.Scalar(query, new[] { new ParamValuePair("@ConfigName", ApprovalKeyConfigName) }),
                CultureInfo.InvariantCulture);
        }

        internal async Task<AddressSearchPage> SearchAsync(string keyword, int page, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(_approvalKey))
                throw new AddressSearchException("도로명주소 검색 API 승인키를 설정해 주세요.");

            Uri endpoint;
            if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
                throw new AddressSearchException("도로명주소 검색 API의 HTTPS 주소 설정을 확인해 주세요.");

            var request = new UriBuilder(endpoint);
            string query = string.Format(CultureInfo.InvariantCulture,
                "confmKey={0}&currentPage={1}&countPerPage={2}&keyword={3}&resultType=json",
                Uri.EscapeDataString(_approvalKey.Trim()), page, PageSize, Uri.EscapeDataString(keyword));
            string existingQuery = request.Query.TrimStart('?');
            request.Query = string.IsNullOrEmpty(existingQuery) ? query : existingQuery + "&" + query;

            try
            {
                using (var response = await _client.GetAsync(request.Uri, token).ConfigureAwait(false))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        throw new AddressSearchException("도로명주소 검색 API 승인키와 사용 권한을 확인해 주세요.");
                    if (!response.IsSuccessStatusCode)
                        throw new AddressSearchException("주소 검색 서버에서 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.");

                    return ParseResponse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new AddressSearchException("주소 검색 응답 시간이 초과되었습니다. 다시 시도해 주세요.");
            }
            catch (HttpRequestException)
            {
                throw new AddressSearchException("주소 검색 서버에 연결할 수 없습니다. 네트워크 연결을 확인해 주세요.");
            }
        }

        private static AddressSearchPage ParseResponse(string json)
        {
            const string invalidResponse = "주소 검색 서버 응답을 읽을 수 없습니다. 잠시 후 다시 시도해 주세요.";
            SearchResponse response;
            try
            {
                response = new JavaScriptSerializer().Deserialize<SearchResponse>(json);
            }
            catch (ArgumentException)
            {
                throw new AddressSearchException(invalidResponse);
            }
            catch (InvalidOperationException)
            {
                throw new AddressSearchException(invalidResponse);
            }

            var results = response?.results;
            var common = results?.common;
            if (string.IsNullOrEmpty(common?.errorCode)) throw new AddressSearchException(invalidResponse);

            if (common.errorCode != "0")
            {
                if (common.errorCode == "E0001" || (common.errorMessage ?? string.Empty).Contains("승인키"))
                    throw new AddressSearchException("도로명주소 검색 API 승인키와 사용 권한을 확인해 주세요.");

                throw new AddressSearchException(string.IsNullOrWhiteSpace(common.errorMessage)
                    ? "도로명주소 검색 API에서 오류가 발생했습니다."
                    : common.errorMessage);
            }

            int totalCount;
            if (!int.TryParse(common.totalCount, NumberStyles.None, CultureInfo.InvariantCulture, out totalCount))
                throw new AddressSearchException(invalidResponse);

            var addresses = new List<AddressResult>();
            var singleAddress = results.juso as Dictionary<string, object>;
            var rows = results.juso as IList;
            if (singleAddress != null)
                addresses.Add(ReadAddress(singleAddress));
            else if (rows != null)
            {
                foreach (object item in rows)
                {
                    var row = item as Dictionary<string, object>;
                    if (row == null) throw new AddressSearchException(invalidResponse);
                    addresses.Add(ReadAddress(row));
                }
            }
            else if (results.juso != null || totalCount > 0)
                throw new AddressSearchException(invalidResponse);

            return new AddressSearchPage(totalCount, addresses);
        }

        private static AddressResult ReadAddress(Dictionary<string, object> row)
        {
            return new AddressResult
            {
                ZipCode = ReadText(row, "zipNo"),
                RoadAddress = ReadText(row, "roadAddr"),
                JibunAddress = ReadText(row, "jibunAddr"),
                BuildingName = ReadText(row, "bdNm"),
                BuildingManagementNumber = ReadText(row, "bdMgtSn")
            };
        }

        private static string ReadText(Dictionary<string, object> row, string name)
        {
            object value;
            return row.TryGetValue(name, out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private sealed class SearchResponse
        {
            public SearchResults results { get; set; }
        }

        private sealed class SearchResults
        {
            public SearchCommon common { get; set; }
            public object juso { get; set; }
        }

        private sealed class SearchCommon
        {
            public string errorCode { get; set; }
            public string errorMessage { get; set; }
            public string totalCount { get; set; }
        }
    }

    internal sealed class AddressSearchPage
    {
        internal AddressSearchPage(int totalCount, List<AddressResult> addresses)
        {
            TotalCount = totalCount;
            Addresses = addresses;
        }

        internal int TotalCount { get; }
        internal List<AddressResult> Addresses { get; }
    }

    internal sealed class AddressSearchException : Exception
    {
        internal AddressSearchException(string message) : base(message) { }
    }
}
