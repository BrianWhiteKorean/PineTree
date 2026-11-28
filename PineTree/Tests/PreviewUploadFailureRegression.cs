using PineTree.Classes;
using PineTree.PdfEngine;
using PineTree.UserControlsOrder;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Uses the reported PDF and simulated uploads; never writes to a remote server.
internal static class PreviewUploadFailureRegression
{
    private static int checks;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private interface IFakeUpload { Func<string, Task<OrderServerFile>> Upload { get; set; } }
    private sealed class PetEditor : UcOrder수성_그룹B, IFakeUpload
    {
        public Func<string, Task<OrderServerFile>> Upload { get; set; }
        protected override Task<OrderServerFile> UploadOrderFileAsync(string path) { return Upload(path); }
    }
    private sealed class BannerEditor : UcOrder수성_그룹A, IFakeUpload
    {
        public Func<string, Task<OrderServerFile>> Upload { get; set; }
        protected override Task<OrderServerFile> UploadOrderFileAsync(string path) { return Upload(path); }
    }
    private sealed class LatexBannerEditor : UcOrder라텍스_그룹A, IFakeUpload
    {
        public Func<string, Task<OrderServerFile>> Upload { get; set; }
        protected override Task<OrderServerFile> UploadOrderFileAsync(string path) { return Upload(path); }
    }
    private static object Call(UserControl editor, string name, params object[] args)
    { return editor.GetType().BaseType.GetMethod(name, Flags).Invoke(editor, args); }
    private static object Field(UserControl editor, string name)
    { return editor.GetType().BaseType.GetField(name, Flags).GetValue(editor); }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
    private static OrderServerFile Uploaded(string path)
    {
        string hash = new string('a', 64);
        return new OrderServerFile { FileId = hash + Path.GetExtension(path), Sha256 = hash,
            OriginalFileName = Path.GetFileName(path), Length = new FileInfo(path).Length };
    }
    private static Exception CertificateError()
    { return new HttpRequestException("An error occurred while sending the request.", new WebException("Untrusted certificate", WebExceptionStatus.TrustFailure)); }

    private static async Task Run(Form host, string source, string folder)
    {
        Directory.CreateDirectory(folder);
        Check(OrderFileStorageService.GetTransferFailureMessage(CertificateError()).Contains("HTTPS 인증서"), "TLS chain has a useful message");
        Check(OrderFileStorageService.GetTransferFailureMessage(new TaskCanceledException()).Contains("응답 시간"), "timeout is distinguished from UI cancellation");
        Check(OrderFileStorageService.GetTransferFailureMessage(new WebException("dns", WebExceptionStatus.NameResolutionFailure)).Contains("연결할 수 없습니다"), "network connection message");
        foreach (var editor in new UserControl[] { new PetEditor(), new BannerEditor(), new LatexBannerEditor() })
        using (editor)
        {
            host.Controls.Add(editor);
            await ((IOrderSourceEditor)editor).SourceDocument.LoadAsync(source);
            Check(Field(editor, "_sourceDetails") != null, "reported PDF loads");
            var cut = OrderPostProcessSetting.Create("CUT");
            cut.Parameters.CutInsetTopMm = cut.Parameters.CutInsetBottomMm = cut.Parameters.CutInsetLeftMm = cut.Parameters.CutInsetRightMm = 5;
            Call(editor, "CommitPostProcess", -1, cut);
            Call(editor, "CommitPostProcess", -1, OrderPostProcessSetting.Create("HOLE"));
            var settings = (PdfGenerationSettings)Call(editor, "ReadPdfGenerationSettings");
            settings.OutputRipScale = 10;
            var uploadToggle = Field(editor, "chkUpload");
            var checkedProperty = uploadToggle.GetType().GetProperty("Checked");
            Check(!(bool)checkedProperty.GetValue(uploadToggle, null), "preview upload is unchecked by default");
            int localUploadAttempts = 0;
            ((IFakeUpload)editor).Upload = path =>
            {
                localUploadAttempts++;
                return Task.FromResult(Uploaded(path));
            };
            string localOutput = Path.Combine(folder, editor.GetType().Name + "-local.pdf");
            var local = await (Task<OrderDocumentResult>)Call(editor, "GeneratePreviewPdfAsync", localOutput, settings);
            Check(File.Exists(local.OutputPath) && localUploadAttempts == 0, "unchecked preview generates a PDF without uploading");
            Check(Field(editor, "_sourceServerFile") == null && Field(editor, "_outputServerFile") == null, "local preview records no server files");

            checkedProperty.SetValue(uploadToggle, true, null);
            foreach (int failAt in new[] { 1, 2, 0 })
            {
                int uploads = 0;
                ((IFakeUpload)editor).Upload = delegate(string path)
                {
                    uploads++;
                    if (uploads == failAt) throw CertificateError();
                    return Task.FromResult(Uploaded(path));
                };
                string output = Path.Combine(folder, editor.GetType().Name + "-" + failAt + ".pdf");
                var result = await (Task<OrderDocumentResult>)Call(editor, "GeneratePreviewPdfAsync", output, settings);
                Check(uploads == (failAt == 1 ? 1 : 2), "checked preview attempts to upload source and output");
                Check(File.Exists(result.OutputPath), "upload failure preserves generated PDF");
                Check(result.OutputRipScale == 10 && Math.Abs(result.OutputWidthMm * 10 -
                    (settings.FinishedWidthMm() + settings.EdgeLeftMm + settings.EdgeRightMm)) < .00001,
                    "generated PDF uses the RIP delivery scale");
                Check(Field(editor, "_sourceProgress") == null && !(bool)Field(editor, "_generating"), "progress and busy state cleaned up");
                Check(failAt == 0 ? Field(editor, "_serverUploadWarning") == null
                    : ((string)Field(editor, "_serverUploadWarning")).Contains("HTTPS 인증서"), "warning resets on successful retry");
                Check(failAt == 0 ? Field(editor, "_outputServerFile") != null : Field(editor, "_outputServerFile") == null, "failed upload never records output as uploaded");
                Check(failAt == 1 ? Field(editor, "_sourceServerFile") == null : Field(editor, "_sourceServerFile") != null, "successful source upload retained");
                if (failAt == 0)
                {
                    var state = ((IOrderItemEditor)editor).CaptureEditorState();
                    var restored = BannerOrderEditorState.FromJson(state.ToJson());
                    Check(restored.OutputServerFile.RipScale == 10m, "uploaded PDF's scale survives saving and reopening");
                    var table = new DataTable();
                    table.Columns.Add("EditorStateJson");
                    var row = table.Rows.Add(state.ToJson());
                    var service = typeof(BannerOrderEditorState).Assembly.GetType("PineTree.Classes.WorkOrderDocumentService", true);
                    var scale = service.GetMethod("ScaleText", BindingFlags.Static | BindingFlags.NonPublic);
                    Check((string)scale.Invoke(null, new object[] { row }) == "10배", "work order displays the uploaded PDF's RIP scale");
                }
                if (failAt == 1)
                {
                    var preview = await OrderDocumentService.PreviewAsync(result.OutputPath, 0, 800, false, CancellationToken.None);
                    Check(preview.PreviewImage != null && preview.MarkCount == 0, "generated PDF remains renderable locally");
                    preview.PreviewImage.Dispose();
                }
            }
            host.Controls.Remove(editor);
        }
        Console.WriteLine("PASS: upload failure and local preview checks: " + checks);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        int exit = 0;
        string appPath = Path.GetFullPath(args[0]), enginePath = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            return name == "PineTree" ? Assembly.LoadFrom(appPath)
                : name == "PineTree.PdfEngine" ? Assembly.LoadFrom(enginePath) : null;
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using (var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -4000, Top = -4000 })
        {
            host.Shown += async delegate
            {
                try { await Run(host, Path.GetFullPath(args[2]), Path.GetFullPath(args[3])); }
                catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
                finally { host.Close(); }
            };
            Application.Run(host);
        }
        return exit;
    }
}
