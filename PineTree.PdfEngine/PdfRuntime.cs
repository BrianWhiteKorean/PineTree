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
    internal sealed class PdfRuntime
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static PdfRuntime ready;
        private const string ResourcePrefix = "PineTree.PdfEngine.";
        private const string WorkerName = "pdf_worker.py";

        internal readonly string DirectoryPath;
        internal readonly string PythonPath;
        internal readonly string GhostscriptPath;
        internal readonly string WorkerPath;

        private PdfRuntime(string runtime, string worker)
        {
            DirectoryPath = runtime;
            PythonPath = Path.Combine(runtime, "python.exe");
            GhostscriptPath = Path.Combine(runtime, "ghostscript", "gsdll64.dll");
            WorkerPath = Path.Combine(worker, WorkerName);
        }

        internal static PdfRuntime EnsureAvailable(CancellationToken token, PdfTiming timing)
        {
            if (!Environment.Is64BitOperatingSystem)
                throw new PlatformNotSupportedException("PDF 처리 엔진은 Windows 64비트 환경이 필요합니다.");
            using (timing?.Measure("prepare_gate_wait")) Gate.Wait(token);
            try
            {
                if (ready != null && File.Exists(ready.PythonPath) && File.Exists(ready.GhostscriptPath)
                    && File.Exists(ready.WorkerPath)) return ready;

                string root = ConfigurationManager.AppSettings["PdfEngineCacheDirectory"];
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PineTree", "PdfEngine");
                root = Path.GetFullPath(root);

                string manifest;
                using (var stream = Resource("PdfRuntime.manifest"))
                using (var reader = new StreamReader(stream, Encoding.UTF8)) manifest = reader.ReadToEnd();
                string runtime = EnsurePart(Path.Combine(root, "runtimes"), Digest(Encoding.UTF8.GetBytes(manifest)),
                    manifest, directory => ExtractRuntime(directory, token), token, timing, "runtime");

                byte[] workerBytes;
                using (var stream = Resource(WorkerName))
                using (var buffer = new MemoryStream())
                {
                    Copy(stream, buffer, token);
                    workerBytes = buffer.ToArray();
                }
                string workerHash = Digest(workerBytes);
                string worker = EnsurePart(Path.Combine(root, "workers"), workerHash, workerHash + "\t" + WorkerName + "\n",
                    directory => File.WriteAllBytes(Path.Combine(directory, WorkerName), workerBytes), token, timing, "worker");
                ready = new PdfRuntime(runtime, worker);
                return ready;
            }
            finally { Gate.Release(); }
        }

        private static string EnsurePart(string parent, string identity, string manifest, Action<string> install,
            CancellationToken token, PdfTiming timing, string part)
        {
            Directory.CreateDirectory(parent);
            string destination = Path.Combine(parent, identity);
            FileStream packageLock;
            using (timing?.Measure(part + "_lock_wait"))
                packageLock = AcquireLock(Path.Combine(parent, identity + ".lock"), token);
            using (packageLock)
            {
                using (timing?.Measure(part + "_verify"))
                    if (Verify(destination, manifest, token)) return destination;

                // Keep temporary names short: bundled license paths are deeply nested.
                string staging = Path.Combine(parent, "staging-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    using (timing?.Measure(part + "_install")) install(staging);
                    using (timing?.Measure(part + "_verify_install"))
                        if (!Verify(staging, manifest, token)) throw new InvalidDataException("PDF 런타임의 무결성 검증에 실패했습니다.");
                    token.ThrowIfCancellationRequested();
                    Publish(staging, destination);
                }
                finally { TryDelete(staging); }
            }
            return destination;
        }

        private static void Publish(string staging, string destination)
        {
            string damaged = null;
            if (Directory.Exists(destination))
            {
                // Replace the directory without modifying files that another process may have open.
                damaged = Path.Combine(Path.GetDirectoryName(destination), "damaged-" + Guid.NewGuid().ToString("N"));
                Directory.Move(destination, damaged);
            }
            try { Directory.Move(staging, destination); }
            catch
            {
                if (damaged != null) Directory.Move(damaged, destination);
                throw;
            }
            if (damaged != null) TryDelete(damaged);
        }

        private static void ExtractRuntime(string directory, CancellationToken token)
        {
            using (var stream = Resource("PdfRuntime.zip"))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string path = ChildPath(directory, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open())
                    using (var output = File.Create(path)) Copy(input, output, token);
                }
            }
        }

        private static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
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
