using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Web.Script.Serialization;

namespace PineTree.PdfEngine
{
    internal sealed class PdfTiming
    {
        private const string WorkerPrefix = "PINETREE_PDF_TIMING ";
        private static readonly string LogDirectory = Environment.GetEnvironmentVariable("PINETREE_PDF_TIMING_DIRECTORY");
        private static readonly object LogGate = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string requestId;
        private readonly string operation;
        private readonly Stopwatch total = Stopwatch.StartNew();
        private readonly Dictionary<string, double> elapsed = new Dictionary<string, double>();
        private object workerElapsed;

        private PdfTiming(string requestId, string operation)
        {
            this.requestId = requestId;
            this.operation = operation;
        }

        internal static PdfTiming Start(string requestId, string operation)
        {
            return string.IsNullOrWhiteSpace(LogDirectory) ? null : new PdfTiming(requestId, operation);
        }

        internal IDisposable Measure(string stage) { return new Stage(this, stage); }

        internal void Record(string stage, double milliseconds) { elapsed[stage] = Math.Round(milliseconds, 3); }

        internal void ReadWorker(string stdout)
        {
            int start = stdout.LastIndexOf(WorkerPrefix, StringComparison.Ordinal);
            if (start < 0) return;
            start += WorkerPrefix.Length;
            int end = stdout.IndexOf('\n', start);
            if (end < 0) end = stdout.Length;
            try
            {
                var entry = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(stdout.Substring(start, end - start));
                object id, times;
                if (entry != null && entry.TryGetValue("requestId", out id) && requestId.Equals(id)
                    && entry.TryGetValue("elapsedMs", out times)) workerElapsed = times;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }

        internal void Write(string status)
        {
            Record("total", total.Elapsed.TotalMilliseconds);
            try
            {
                var entry = new { requestId, operation, status, host = elapsed, worker = workerElapsed };
                string line = new JavaScriptSerializer().Serialize(entry) + Environment.NewLine;
                using (var process = Process.GetCurrentProcess())
                {
                    string file = Path.Combine(LogDirectory, "pdf-timing-" + process.Id + ".jsonl");
                    lock (LogGate)
                    {
                        Directory.CreateDirectory(LogDirectory);
                        File.AppendAllText(file, line, Utf8);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (SecurityException) { }
        }

        private sealed class Stage : IDisposable
        {
            private readonly PdfTiming timing;
            private readonly string name;
            private readonly Stopwatch watch = Stopwatch.StartNew();

            internal Stage(PdfTiming timing, string name)
            {
                this.timing = timing;
                this.name = name;
            }

            public void Dispose() { timing.Record(name, watch.Elapsed.TotalMilliseconds); }
        }
    }
}
