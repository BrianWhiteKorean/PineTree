using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PineTree.PdfEngine
{
    // Resources are read from this assembly, including when Costura loads it from memory.
    internal static class PdfRuntime
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static string readyDirectory;
        private const string ResourcePrefix = "PineTree.PdfEngine.";

        internal static string EnsureAvailable(CancellationToken token)
        {
            if (!Environment.Is64BitOperatingSystem)
                throw new PlatformNotSupportedException("PDF 처리 엔진은 Windows 64비트 환경이 필요합니다.");
            Gate.Wait(token);
            try
            {
                if (readyDirectory != null && File.Exists(Path.Combine(readyDirectory, "runtime", "python.exe"))
                    && File.Exists(Path.Combine(readyDirectory, "pdf_worker.py"))) return readyDirectory;
                string manifest;
                using (var stream = Resource("PdfWorker.manifest"))
                using (var reader = new StreamReader(stream, Encoding.UTF8)) manifest = reader.ReadToEnd();
                string identity;
                using (var sha = SHA256.Create()) identity = Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest)));
                string root = ConfigurationManager.AppSettings["PdfEngineCacheDirectory"];
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PineTree", "PdfEngine");
                root = Path.GetFullPath(root);
                Directory.CreateDirectory(root);
                string destination = Path.Combine(root, identity);
                using (AcquireLock(Path.Combine(root, identity + ".lock"), token))
                {
                    if (!Verify(destination, manifest, token))
                    {
                        // Keep temporary names short: bundled license paths are deeply nested.
                        string staging = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(staging);
                        try
                        {
                            using (var stream = Resource("PdfWorker.zip"))
                            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                            {
                                foreach (var entry in zip.Entries)
                                {
                                    token.ThrowIfCancellationRequested();
                                    string path = ChildPath(staging, entry.FullName);
                                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                                    using (var input = entry.Open())
                                    using (var output = File.Create(path)) Copy(input, output, token);
                                }
                            }
                            if (!Verify(staging, manifest, token)) throw new InvalidDataException("PDF 런타임의 무결성 검증에 실패했습니다.");
                            token.ThrowIfCancellationRequested();
                            if (Directory.Exists(destination))
                            {
                                // Do not modify files which another running worker may have open.
                                string damaged = Path.Combine(root, "damaged-" + Guid.NewGuid().ToString("N"));
                                Directory.Move(destination, damaged);
                                TryDelete(damaged);
                            }
                            Directory.Move(staging, destination);
                        }
                        finally { TryDelete(staging); }
                    }
                }
                readyDirectory = destination;
                return destination;
            }
            finally { Gate.Release(); }
        }

        private static Stream Resource(string name)
        {
            var stream = typeof(PdfRuntime).Assembly.GetManifestResourceStream(ResourcePrefix + name);
            if (stream == null) throw new InvalidOperationException("PDF 런타임 리소스가 없습니다. PdfEngine 프로젝트를 다시 빌드해주세요.");
            return stream;
        }

        private static FileStream AcquireLock(string path, CancellationToken token)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException)
                {
                    if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("PDF 런타임 준비 대기 시간이 초과되었습니다.");
                    if (token.WaitHandle.WaitOne(100)) token.ThrowIfCancellationRequested();
                }
            }
        }

        private static bool Verify(string directory, string manifest, CancellationToken token)
        {
            if (!Directory.Exists(directory)) return false;
            try
            {
                foreach (string line in manifest.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    token.ThrowIfCancellationRequested();
                    string[] parts = line.Split('\t');
                    if (parts.Length != 2) throw new InvalidDataException("Invalid PDF runtime manifest.");
                    string path = ChildPath(directory, parts[1]);
                    if (!File.Exists(path)) return false;
                    using (var sha = SHA256.Create())
                    using (var file = File.OpenRead(path))
                        if (!string.Equals(parts[0], Hash(file, sha, token), StringComparison.Ordinal)) return false;
                }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static string Hash(Stream stream, HashAlgorithm sha, CancellationToken token)
        {
            var buffer = new byte[65536];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                sha.TransformBlock(buffer, 0, count, buffer, 0);
            }
            sha.TransformFinalBlock(new byte[0], 0, 0);
            return Hex(sha.Hash);
        }

        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }

        private static string ChildPath(string root, string relative)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(prefix, relative));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid PDF runtime path.");
            return path;
        }

        private static void Copy(Stream input, Stream output, CancellationToken token)
        {
            var buffer = new byte[65536];
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                output.Write(buffer, 0, count);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
