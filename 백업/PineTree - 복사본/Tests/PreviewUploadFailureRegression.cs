using PineTree.Classes;
using PineTree.PdfEngine;
using PineTree.UserControlsOrder;
using System;
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
    private sealed class PetEditor : UcOrder페트, IFakeUpload
    {
        public Func<string, Task<OrderServerFile>> Upload { get; set; }
        protected override Task<OrderServerFile> UploadOrderFileAsync(string path) { return Upload(path); }
    }
    private sealed class BannerEditor : UcOrder현수막, IFakeUpload
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
        foreach (var editor in new UserControl[] { new PetEditor(), new BannerEditor() })
        using (editor)
        {
            host.Controls.Add(editor);
            await (Task)Call(editor, "LoadSourceFileAsync", source);
            Check(Field(editor, "_sourceDetails") != null, "reported PDF loads");
            var cut = OrderPostProcessSetting.Create("CUT");
            cut.Parameters.CutInsetTopMm = cut.Parameters.CutInsetBottomMm = cut.Parameters.CutInsetLeftMm = cut.Parameters.CutInsetRightMm = 5;
            Call(editor, "CommitPostProcess", -1, cut);
            Call(editor, "CommitPostProcess", -1, OrderPostProcessSetting.Create("HOLE"));
            var settings = (PdfGenerationSettings)Call(editor, "ReadPdfGenerationSettings");
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
                var result = await (Task<OrderDocumentResult>)Call(editor, "GenerateAndUploadPdfAsync", output, settings);
                Check(File.Exists(result.OutputPath), "upload failure preserves generated PDF");
                Check(Field(editor, "_sourceProgress") == null && !(bool)Field(editor, "_generating"), "progress and busy state cleaned up");
                Check(failAt == 0 ? Field(editor, "_serverUploadWarning") == null
                    : ((string)Field(editor, "_serverUploadWarning")).Contains("HTTPS 인증서"), "warning resets on successful retry");
                Check(failAt == 0 ? Field(editor, "_outputServerFile") != null : Field(editor, "_outputServerFile") == null, "failed upload never records output as uploaded");
                Check(failAt == 1 ? Field(editor, "_sourceServerFile") == null : Field(editor, "_sourceServerFile") != null, "successful source upload retained");
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
