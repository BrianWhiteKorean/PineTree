using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PineTree.Classes
{
    // One generated workbook and PDF are shared by preview, printing and Excel export.
    internal sealed class WorkOrderDocument : IDisposable
    {
        public string DirectoryPath { get; private set; }
        public string ExcelPath { get { return Path.Combine(DirectoryPath, "작업지시서.xlsx"); } }
        public string PdfPath { get { return Path.Combine(DirectoryPath, "작업지시서.pdf"); } }

        public WorkOrderDocument()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "PineTreeWorkOrders", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public void Dispose()
        {
            // Only delete the two files owned by this document; never traverse other temp files.
            foreach (string path in new[] { ExcelPath, PdfPath })
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            try { Directory.Delete(DirectoryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static class WorkOrderDocumentService
    {
        public static Task<WorkOrderDocument> CreateAsync(DataRow order, CancellationToken token)
        {
            return CreateWithProgressAsync(order, token, null);
        }

        public static Task<WorkOrderDocument> CreateWithProgressAsync(DataRow order, CancellationToken token, IProgress<string> progress)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            var completion = new TaskCompletionSource<WorkOrderDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Excel automation needs an STA thread. Do not attach to the user's open Excel instance.
            var thread = new Thread(() =>
            {
                WorkOrderDocument result = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    result = new WorkOrderDocument();
                    CreateWorkbook(order, result, token, progress);
                    token.ThrowIfCancellationRequested();
                    completion.SetResult(result);
                }
                catch (OperationCanceledException)
                {
                    if (result != null) result.Dispose();
                    completion.SetCanceled();
                }
                catch (Exception ex)
                {
                    if (result != null) result.Dispose();
                    completion.SetException(ex);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void CreateWorkbook(DataRow order, WorkOrderDocument document, CancellationToken token, IProgress<string> progress)
        {
            using (var source = typeof(WorkOrderDocumentService).Assembly.GetManifestResourceStream("PineTree.Resources.StandardWorkOrder.xlsx"))
            {
                if (source == null) throw new InvalidOperationException("표준 작업지시서 리소스를 찾을 수 없습니다.");
                using (var output = File.Create(document.ExcelPath)) source.CopyTo(output);
            }

            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null) throw new InvalidOperationException("작업지시서 미리보기를 만들려면 Microsoft Excel을 설치해야 합니다.");
            dynamic excel = null, workbooks = null, workbook = null, sheets = null, sheet = null;
            try
            {
                progress?.Report("Excel을 시작하는 중입니다…");
                excel = Activator.CreateInstance(excelType);
                excel.Visible = false;
                excel.DisplayAlerts = false;
                excel.AskToUpdateLinks = false;
                excel.AutomationSecurity = 3; // Disable macros in templates added in the future.
                token.ThrowIfCancellationRequested();
                progress?.Report("표준 양식에 주문정보를 채우는 중입니다…");
                workbooks = excel.Workbooks;
                workbook = workbooks.Open(document.ExcelPath, UpdateLinks: 0, ReadOnly: false, IgnoreReadOnlyRecommended: true);
                sheets = workbook.Worksheets;
                sheet = sheets.Item[1];
                string customer = Text(order, "CustName");
                string title = Text(order, "OrderTitle");
                if (string.IsNullOrWhiteSpace(title)) title = Text(order, "CustomName");
                if (string.IsNullOrWhiteSpace(title)) title = Text(order, "ItemName");
                foreach (string address in new[] { "B1", "B8" }) SetText((object)sheet, address, customer);
                foreach (string address in new[] { "C2", "C9" }) SetText((object)sheet, address, title);
                DateTime date;
                bool hasDate = DateTime.TryParse(Text(order, "OrderDate"), out date);
                foreach (string address in new[] { "A3", "A10" })
                    SetText((object)sheet, address, hasDate ? date.ToString("yyyy", CultureInfo.InvariantCulture) : "");
                foreach (string address in new[] { "A4", "A11" })
                    SetText((object)sheet, address, hasDate ? date.ToString("M월 d일", CultureInfo.InvariantCulture) : "");
                SetText((object)sheet, "C14", ScaleText(order));
                // These template references otherwise display 0 for fields not yet confirmed.
                foreach (string address in new[] { "C11", "D11", "E11", "F11", "H11", "G12" })
                    SetText((object)sheet, address, "");
                token.ThrowIfCancellationRequested();
                excel.CalculateFull();
                workbook.Save();
                token.ThrowIfCancellationRequested();
                progress?.Report("인쇄용 작업서 PDF를 만드는 중입니다…");
                // Use the original workbook's page setup, breaks, print areas and visible sheets.
                workbook.ExportAsFixedFormat(0, document.PdfPath, Quality: 0, IncludeDocProperties: true,
                    IgnorePrintAreas: false, OpenAfterPublish: false);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Excel에서 작업지시서를 생성하지 못했습니다. Excel 설치 및 기본 프린터 설정을 확인해주세요.\r\n" + ex.Message, ex);
            }
            finally
            {
                try { if ((object)workbook != null) workbook.Close(false); }
                finally
                {
                    Release((object)sheet); Release((object)sheets); Release((object)workbook); Release((object)workbooks);
                    try { if ((object)excel != null) excel.Quit(); }
                    finally { Release((object)excel); }
                }
            }
        }

        internal static void SetText(dynamic sheet, string address, string value)
        {
            dynamic range = null;
            try
            {
                range = sheet.Range[address];
                // Treat customer supplied strings as literal text, including leading '=' or '+'.
                range.NumberFormat = "@";
                range.Value2 = value ?? "";
            }
            finally { Release((object)range); }
        }

        internal static string ScaleText(DataRow order)
        {
            string json = Text(order, "EditorStateJson");
            if (string.IsNullOrWhiteSpace(json)) return "";
            var state = new JavaScriptSerializer().Deserialize<BannerOrderEditorState>(json);
            if (state == null) return "";
            if (state.OutputServerFile != null)
                return (state.OutputServerFile.RipScale ?? 1m).ToString("0.###", CultureInfo.InvariantCulture) + "배";
            decimal rate;
            switch (state.ZoomType)
            {
                case "ORIGINAL": rate = 1m; break;
                case "SCALE": rate = state.ZoomRate; break;
                default:
                    if (state.SourceWidthMm <= 0 || state.SourceHeightMm <= 0 || state.WidthMm <= 0 || state.HeightMm <= 0) return "";
                    rate = state.WidthMm / state.SourceWidthMm;
                    if (Math.Abs(state.HeightMm - state.SourceHeightMm * rate) > 1m) return "";
                    break;
            }
            return rate > 0 ? rate.ToString("0.###", CultureInfo.InvariantCulture) + "배" : "";
        }

        internal static string Text(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToString(row[column]) : "";
        }

        internal static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
    }
}
