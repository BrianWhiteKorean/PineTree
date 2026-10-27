using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PineTree.PdfEngine
{
    // Direct UI settings. These are independent of the reusable finishing rules.
    public sealed class PdfGenerationSettings
    {
        public double TargetWidthMm { get; set; } = 600;
        public double TargetHeightMm { get; set; } = 1800;
        public string FitMode { get; set; } = "CONTAIN";
        public double EdgeTopMm { get; set; } = 2;
        public double EdgeBottomMm { get; set; } = 2;
        public double EdgeLeftMm { get; set; } = 2;
        public double EdgeRightMm { get; set; } = 2;
        public bool MakeCutline { get; set; }
        public double LineWidthMm { get; set; } = 0.18;
        public string SpotColorName { get; set; } = "thru";
        public bool MakeHole { get; set; }
        public string HoleType { get; set; } = "FOUR_CORNERS";
        public double MarkLeftRightMm { get; set; } = 20;
        public double MarkTopBottomMm { get; set; } = 20;
        public double HoleSpacingMm { get; set; } = 500;

        public bool MakeWood { get; set; }
        public double WoodExtraMm { get; set; }
        public bool MakeWoodDesignLine { get; set; }
        public double WoodDesignOffsetMm { get; set; }

        public int GetMarkCount()
        {
            if (!MakeHole) return 0;
            if (HoleType == "FOUR_CORNERS") return 4;
            double horizontal = Math.Ceiling((TargetWidthMm + (MakeWood ? 2 * WoodExtraMm : 0) - 2 * MarkLeftRightMm) / HoleSpacingMm);
            double vertical = Math.Ceiling((TargetHeightMm - 2 * MarkTopBottomMm) / HoleSpacingMm);
            double count = 2 * (horizontal + vertical);
            if (double.IsNaN(count) || double.IsInfinity(count) || count > 2000)
                throw new ArgumentException("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.");
            return (int)count;
        }

        public void Validate()
        {
            foreach (double value in new[] { TargetWidthMm, TargetHeightMm, EdgeTopMm, EdgeBottomMm,
                EdgeLeftMm, EdgeRightMm, LineWidthMm, MarkLeftRightMm, MarkTopBottomMm, HoleSpacingMm, WoodExtraMm, WoodDesignOffsetMm })
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("유효한 숫자를 입력해주세요.");
            if (TargetWidthMm <= 0 || TargetHeightMm <= 0 || TargetWidthMm + EdgeLeftMm + EdgeRightMm + (MakeWood ? 2 * WoodExtraMm : 0) > 14400
                || TargetHeightMm + EdgeTopMm + EdgeBottomMm > 14400)
                throw new ArgumentException("완성 가로·세로는 양수이며 가장자리를 포함한 출력 크기는 14,400mm 이하여야 합니다.");
            if (EdgeTopMm < 0 || EdgeBottomMm < 0 || EdgeLeftMm < 0 || EdgeRightMm < 0)
                throw new ArgumentException("가장자리 여유는 0 이상이어야 합니다.");
            if (MakeWood && (WoodExtraMm < 0 || WoodDesignOffsetMm < 0 || (MakeWoodDesignLine && WoodDesignOffsetMm > WoodExtraMm)))
                throw new ArgumentException("좌우각목 여백은 0 이상, 디자인선 이격은 추가 여백 이내로 입력해주세요.");
            if (FitMode != "CONTAIN" && FitMode != "COVER") throw new ArgumentException("배치방식을 선택해주세요.");
            if (MakeCutline && (LineWidthMm <= 0 || LineWidthMm > 10 || string.IsNullOrWhiteSpace(SpotColorName)
                || SpotColorName.Length > 100 || SpotColorName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0))
                throw new ArgumentException("재단선 두께는 0 초과 10mm 이하, 별색 이름은 100자 이내로 입력해주세요.");
            if (MakeHole)
            {
                if (HoleType != "FOUR_CORNERS" && HoleType != "CUSTOM_SPACING") throw new ArgumentException("타공 배치를 선택해주세요.");
                if (MarkLeftRightMm < 2.5 || MarkTopBottomMm < 2.5 || 2 * MarkLeftRightMm >= TargetWidthMm + (MakeWood ? 2 * WoodExtraMm : 0)
                    || 2 * MarkTopBottomMm >= TargetHeightMm)
                    throw new ArgumentException("마크 안쪽 거리는 2.5mm 이상이며 완성 가로·세로의 절반보다 작아야 합니다.");
                if (HoleType == "CUSTOM_SPACING" && HoleSpacingMm <= 0) throw new ArgumentException("타공 최대 간격은 양수여야 합니다.");
                GetMarkCount();
            }
        }
    }

    public sealed class OrderDocumentResult
    {
        public bool Success { get; set; }
        public bool IsCanceled { get; set; }
        public string Error { get; set; }
        public string FileType { get; set; }
        public string FileInfo { get; set; }
        public int PageCount { get; set; }
        public double SourceWidthMm { get; set; }
        public double SourceHeightMm { get; set; }
        public string SourceDimensionNote { get; set; }
        public bool HasCutline { get; set; }
        public string CutlineColorName { get; set; }
        public double? CutlineWidthMm { get; set; }
        public string RegionDescription { get; set; }
        public string Recipe { get; set; }
        public string OutputPath { get; set; }
        public double Scale { get; set; }
        public double OutputWidthMm { get; set; }
        public double OutputHeightMm { get; set; }
        public int MarkCount { get; set; }
        [ScriptIgnore]
        public Image PreviewImage { get; set; }
    }

    public static class OrderDocumentService
    {
        public const string InputFilter = "출력 원본 (PDF/AI/EPS/PNG/JPG)|*.pdf;*.ai;*.eps;*.png;*.jpg;*.jpeg|PDF 문서|*.pdf|Illustrator 문서|*.ai|EPS 문서|*.eps|PNG 이미지|*.png|JPEG 이미지|*.jpg;*.jpeg";

        public static Task<OrderDocumentResult> InspectAsync(string source, CancellationToken token)
        {
            return RunAsync("inspect", source, null, 0, 1600, false, token);
        }

        public static Task<OrderDocumentResult> PreviewAsync(string source, int page, int maxPixels, bool fullCanvas, CancellationToken token)
        {
            return RunAsync("preview", source, null, page, maxPixels, fullCanvas, token);
        }

        // Rectangle uses fractions of the displayed page, with its origin at the top left.
        // Normal viewport cancellation returns IsCanceled, without throwing in the debugger.
        public static Task<OrderDocumentResult> PreviewRegionAsync(string source, int page, int maxPixels,
            bool fullCanvas, RectangleF region, CancellationToken token)
        {
            return Task.Run(() => RunWorker("preview", source, null, page, maxPixels, fullCanvas, token,
                viewportRegion: new[] { (double)region.X, region.Y, region.Width, region.Height }));
        }

        public static Task<OrderDocumentResult> GenerateSampleAsync(string source, string output, CancellationToken token)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("원본파일과 출력파일의 저장 경로는 달라야 합니다.");
            if (!string.Equals(Path.GetExtension(output), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력파일은 PDF 형식으로 저장해주세요.");
            return RunAsync("generate", source, output, 0, 1600, false, token);
        }

        public static Task<OrderDocumentResult> GenerateAsync(string source, string output, PdfGenerationSettings settings, CancellationToken token)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            settings.Validate();
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("원본파일과 출력파일의 저장 경로는 달라야 합니다.");
            if (!string.Equals(Path.GetExtension(output), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력파일은 PDF 형식으로 저장해주세요.");
            // Freeze settings before the background operation starts.
            string snapshot = new JavaScriptSerializer().Serialize(settings);
            return Task.Run(() => RunWorker("generate", source, output, 0, 1600, false, token, snapshot), token);
        }

        private static Task<OrderDocumentResult> RunAsync(string operation, string source, string output,
            int page, int maxPixels, bool fullCanvas, CancellationToken token)
        {
            return Task.Run(() => RunWorker(operation, source, output, page, maxPixels, fullCanvas, token), token);
        }

        private static OrderDocumentResult RunWorker(string operation, string source, string output,
            int page, int maxPixels, bool fullCanvas, CancellationToken token, string settingsJson = null, double[] viewportRegion = null)
        {
            bool quietCancellation = viewportRegion != null;
            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException("작업요청파일을 선택해주세요.", source);
            // Shared runtime preparation should finish once even if this particular viewport closes.
            string tools = PdfRuntime.EnsureAvailable(quietCancellation ? CancellationToken.None : token);
            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
            string worker = Path.Combine(tools, "pdf_worker.py");
            string python = ConfigurationManager.AppSettings["PdfPythonPath"];
            if (string.IsNullOrWhiteSpace(python)) python = Path.Combine(tools, "runtime", "python.exe");
            if (!File.Exists(python)) throw new FileNotFoundException("PDF Python 실행 파일이 없습니다.", python);
            string work = Path.Combine(Path.GetTempPath(), "PineTreePdf", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string preview = Path.Combine(work, "preview.png");
            string generated = Path.Combine(work, "generated.pdf");
            string staging = null;
            try
            {
                var request = new Dictionary<string, object>
                {
                    { "Operation", operation }, { "SourcePath", Path.GetFullPath(source) }, { "PageIndex", page },
                    { "WorkDirectory", work },
                    { "MaxPixels", maxPixels }, { "FullCanvas", fullCanvas },
                    { "OutputPath", operation == "generate" ? generated : preview },
                    { "GhostscriptDll", ConfigurationManager.AppSettings["PdfGhostscriptDllPath"] }
                };
                var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                if (viewportRegion != null) request["ViewportRegion"] = viewportRegion;
                if (settingsJson != null) request["Settings"] = json.DeserializeObject(settingsJson);
                string requestFile = Path.Combine(work, "request.json");
                string responseFile = Path.Combine(work, "response.json");
                File.WriteAllText(requestFile, json.Serialize(request), new UTF8Encoding(false));
                var start = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = QuoteArgument(worker) + " " + QuoteArgument(requestFile) + " " + QuoteArgument(responseFile),
                    WorkingDirectory = tools,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8
                };
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                start.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
                start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
                using (var process = Process.Start(start))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        while (!process.WaitForExit(100))
                        {
                            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                            if (watch.Elapsed > TimeSpan.FromMinutes(3))
                                throw new TimeoutException("파일 처리 시간이 3분을 초과했습니다. 파일의 크기와 상태를 확인해주세요.");
                        }
                        if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                        Task.WaitAll(stdout, stderr);
                        if (!File.Exists(responseFile))
                            throw new InvalidOperationException("PDF 처리 엔진이 응답하지 않았습니다. 런타임 설치 상태를 확인해주세요.\r\n" +
                                (stderr.Result.Length > 1200 ? stderr.Result.Substring(0, 1200) : stderr.Result));
                    }
                    finally
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                }
                var result = json.Deserialize<OrderDocumentResult>(File.ReadAllText(responseFile, Encoding.UTF8));
                if (result == null || !result.Success)
                    throw new InvalidOperationException(result == null ? "파일 처리 결과가 없습니다." : result.Error);
                if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                if (operation == "preview")
                {
                    using (var stream = new MemoryStream(File.ReadAllBytes(preview)))
                    using (var image = Image.FromStream(stream))
                        result.PreviewImage = new Bitmap(image);
                }
                if (operation == "generate")
                {
                    string final = Path.GetFullPath(output);
                    string directory = Path.GetDirectoryName(final);
                    Directory.CreateDirectory(directory);
                    staging = Path.Combine(directory, ".pinetree-" + Guid.NewGuid().ToString("N") + ".tmp");
                    using (var input = File.OpenRead(generated))
                    using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[1024 * 1024];
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                            file.Write(buffer, 0, count);
                        }
                        file.Flush(true);
                    }
                    if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                    if (File.Exists(final)) File.Replace(staging, final, null);
                    else File.Move(staging, final);
                    staging = null;
                    result.OutputPath = final;
                }
                return result;
            }
            finally
            {
                if (staging != null && File.Exists(staging)) File.Delete(staging);
                try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static bool ShouldStop(CancellationToken token, bool quietCancellation)
        {
            if (!token.IsCancellationRequested) return false;
            if (quietCancellation) return true;
            token.ThrowIfCancellationRequested();
            return false;
        }

        private static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2).Append('"');
            return result.ToString();
        }
    }
}
