using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PineTree.Classes
{
    // Server credentials are read from DB configuration, never from the development memo.
    public static class OrderFileStorageService
    {
        public const long SourceUploadLimitBytes = 100L * 1024 * 1024;
        public const string SourceUploadLimitMessage = "100 Mbyte 미만의 파일만 서버에 업로드가 가능합니다";

        public static bool CanUploadSource(string path)
        {
            return new FileInfo(path).Length < SourceUploadLimitBytes;
        }

        public static string GetTransferFailureMessage(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
            {
                var web = current as WebException;
                if (current is System.Security.Authentication.AuthenticationException
                    || (web != null && (web.Status == WebExceptionStatus.TrustFailure
                        || web.Status == WebExceptionStatus.SecureChannelFailure)))
                    return "파일 서버의 HTTPS 인증서를 확인할 수 없습니다. 서버 인증서의 유효기간, 도메인 및 인증서 체인을 확인해주세요.";
                if (web != null && (web.Status == WebExceptionStatus.ConnectFailure
                    || web.Status == WebExceptionStatus.NameResolutionFailure))
                    return "파일 서버에 연결할 수 없습니다. 네트워크와 파일 서버의 연결 상태를 확인해주세요.";
                if (current is TimeoutException || current is TaskCanceledException
                    || (web != null && web.Status == WebExceptionStatus.Timeout))
                    return "파일 서버의 응답 시간이 초과되었습니다. 잠시 후 다시 시도해주세요.";
            }
            return error.Message;
        }

        private sealed class StorageSettings
        {
            public Uri Endpoint;
            public string Token;
        }

        private static StorageSettings ReadSettings(string storageKey, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var connection = DB.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT EndpointUrl, ApiToken FROM OrderFileStorageSettings WHERE StorageKey=@Key AND IsActive=1";
                command.CommandTimeout = 20;
                command.Parameters.AddWithValue("@Key", storageKey);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("주문파일 서버 연결 설정이 없습니다.");
                    Uri endpoint;
                    string key = reader.GetString(1);
                    if (!Uri.TryCreate(reader.GetString(0), UriKind.Absolute, out endpoint)
                        || endpoint.Scheme != Uri.UriSchemeHttps || endpoint.Query.Length != 0
                        || endpoint.UserInfo.Length != 0 || string.IsNullOrWhiteSpace(key))
                        throw new InvalidOperationException("주문파일 서버 연결 설정을 확인해주세요.");
                    return new StorageSettings { Endpoint = endpoint, Token = key };
                }
            }
        }

        private static HttpClient CreateClient(StorageSettings settings)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
                Timeout = TimeSpan.FromMinutes(15)
            };
            client.DefaultRequestHeaders.Add("X-PineTree-Token", settings.Token);
            return client;
        }

        private static async Task<string> HashFileAsync(string path, CancellationToken token)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            using (var hash = SHA256.Create())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                    hash.TransformBlock(buffer, 0, read, buffer, 0);
                token.ThrowIfCancellationRequested();
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static async Task<OrderServerFile> UploadAsync(string path, CancellationToken token)
        {
            var settings = await Task.Run(() => ReadSettings("BANNER", token), token).ConfigureAwait(false);
            // Keep the source locked against writes from hashing through upload.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                var file = new OrderServerFile {
                    OriginalFileName = Path.GetFileName(path), Length = stream.Length,
                    Sha256 = await HashFileAsync(path, token).ConfigureAwait(false)
                };
                file.FileId = file.Sha256 + Path.GetExtension(path).ToLowerInvariant();
                file.Validate();
                using (var client = CreateClient(settings))
                using (var content = new StreamContent(stream))
                {
                    content.Headers.ContentLength = file.Length;
                    var uri = new Uri(settings.Endpoint + "?id=" + file.FileId);
                    using (var response = await client.PostAsync(uri, content, token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new IOException("서버 파일 업로드에 실패했습니다. (HTTP " + (int)response.StatusCode + ")");
                        var result = new JavaScriptSerializer().Deserialize<OrderServerFile>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        if (result == null || result.FileId != file.FileId || result.Sha256 != file.Sha256 || result.Length != file.Length)
                            throw new IOException("업로드한 파일의 서버 검증 결과가 일치하지 않습니다.");
                    }
                }
                return file;
            }
        }

        public static async Task<string> DownloadAsync(OrderServerFile file, CancellationToken token)
        {
            file.Validate();
            var settings = await Task.Run(() => ReadSettings(file.StorageKey, token), token).ConfigureAwait(false);
            string directory = Path.Combine(Path.GetTempPath(), "PineTreeOrderSources", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, file.OriginalFileName);
            string partial = path + ".partial";
            try
            {
                using (var client = CreateClient(settings))
                using (var response = await client.GetAsync(new Uri(settings.Endpoint + "?id=" + file.FileId), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new IOException("서버 원본파일을 내려받지 못했습니다. (HTTP " + (int)response.StatusCode + ")");
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != file.Length)
                        throw new IOException("서버 원본파일의 크기가 저장된 정보와 다릅니다.");
                    using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    using (token.Register(() => source.Dispose()))
                    {
                        byte[] buffer = new byte[81920];
                        long total = 0;
                        int read;
                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                        {
                            total += read;
                            if (total > file.Length) throw new IOException("서버 원본파일의 크기가 저장된 정보와 다릅니다.");
                            await target.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                        }
                        if (total != file.Length) throw new IOException("서버 원본파일이 완전히 내려받아지지 않았습니다.");
                    }
                }
                if (await HashFileAsync(partial, token).ConfigureAwait(false) != file.Sha256)
                    throw new IOException("내려받은 원본파일의 무결성 검증에 실패했습니다.");
                token.ThrowIfCancellationRequested();
                File.Move(partial, path);
                return path;
            }
            catch
            {
                try { if (File.Exists(partial)) File.Delete(partial); Directory.Delete(directory, false); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        internal static void DeleteFiles(IEnumerable<string> fileIds)
        {
            DeleteFilesAsync(fileIds).GetAwaiter().GetResult();
        }

        private static async Task DeleteFilesAsync(IEnumerable<string> fileIds)
        {
            using (var ids = fileIds.GetEnumerator())
            {
                if (!ids.MoveNext()) return;
                var settings = ReadSettings("BANNER", CancellationToken.None);
                using (var client = CreateClient(settings))
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    do
                    {
                        var uri = new Uri(settings.Endpoint + "?id=" + Uri.EscapeDataString(ids.Current));
                        using (var response = await client.DeleteAsync(uri).ConfigureAwait(false))
                        {
                            if (response.StatusCode != HttpStatusCode.NoContent)
                                throw new IOException("서버 파일 삭제에 실패했습니다. (HTTP " + (int)response.StatusCode + ")");
                        }
                    } while (ids.MoveNext());
                }
            }
        }
    }
}
