using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static PineTree.Classes.WorkOrderDocumentService;

namespace PineTree.Classes
{
    internal sealed class DeliveryWorkOrder
    {
        public string OrderNo, Delivery, Customer, Equipment, Paper, Size, Quantity, Coating, Packing, Finishing, Materials, Remarks;
        public DateTime? WrittenAt;
        public DateTime DueAt;
        public Image CoverImage;
        public Image[] ExtraImages = new Image[0];

        public string GetFileName(bool asPdf = false)
        {
            var invalid = Path.GetInvalidFileNameChars();
            string name = string.Join("_", new[] { Paper, Size, Finishing, Quantity, Delivery }.Select(part =>
                string.Concat((part ?? "").Trim().Select(c => invalid.Contains(c) ? '-' : c))));
            return name + (asPdf ? ".pdf" : ".xlsx");
        }

        public static string DeliveryKind(string value)
        {
            string text = string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c)));
            if (text.Contains("택배")) return "택배";
            if (text.Contains("창고")) return "창고";
            if (text.Contains("퀵")) return "퀵";
            return "";
        }

        public static string DateText(DateTime date)
        {
            return date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }

    internal static class DeliveryWorkOrderService
    {
        private const int ImagesPerSheet = 9;
        private const string DefaultTemplateBaseUrl = "http://211.45.163.175/pinetreeupdate/workorder/";
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(25), MaxResponseContentBufferSize = 10 * 1024 * 1024 };

        public static async Task<string> GenerateAsync(DeliveryWorkOrder order, string destination, bool asPdf)
        {
            string kind = DeliveryWorkOrder.DeliveryKind(order.Delivery);
            if (kind.Length == 0) throw new ArgumentException("택배, 창고, 퀵 중에서 배송방법을 선택해주세요.");
            bool replaceExisting = destination != null && File.Exists(destination);
            if (destination == null) await Task.Run(CleanOldPreviews).ConfigureAwait(false);
            string directory = Path.Combine(Path.GetTempPath(), "PineTreeDeliveryWorkOrders", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, order.GetFileName(asPdf));
            bool keepPreview = false;
            try
            {
                string cover = Path.Combine(directory, "cover.xlsx");
                string appendix = order.ExtraImages.Length == 0 ? null : Path.Combine(directory, "appendix.xlsx");
                await Task.WhenAll(
                    DownloadAsync(kind, cover),
                    appendix == null ? Task.CompletedTask : DownloadAsync("별지", appendix)).ConfigureAwait(false);

                await RunExcelAsync(() => BuildWorkbook(order, kind, cover, appendix, output, asPdf)).ConfigureAwait(false);
                if (destination != null)
                {
                    SaveLocal(output, destination, replaceExisting);
                    return destination;
                }

                keepPreview = true;
                return output;
            }
            finally
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                    if (!keepPreview || file != output) TryDelete(file);
                if (!keepPreview) TryDeleteEmptyDirectory(directory);
            }
        }

        internal static void DeletePreview(string path)
        {
            TryDelete(path);
            TryDeleteEmptyDirectory(Path.GetDirectoryName(path));
        }

        internal static void CleanOldPreviews()
        {
            string root = Path.Combine(Path.GetTempPath(), "PineTreeDeliveryWorkOrders");
            if (!Directory.Exists(root)) return;
            try
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-1);
                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    Guid id;
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out id) ||
                        (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                        Directory.GetLastWriteTimeUtc(directory) >= cutoff) continue;
                    foreach (string file in Directory.EnumerateFiles(directory)) TryDelete(file);
                    TryDeleteEmptyDirectory(directory);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static async Task DownloadAsync(string template, string path)
        {
            string setting = ConfigurationManager.AppSettings["WorkOrderTemplateBaseUrl"];
            if (string.IsNullOrWhiteSpace(setting)) setting = DefaultTemplateBaseUrl;
            Uri root;
            if (!Uri.TryCreate(setting.Trim().TrimEnd('/') + "/", UriKind.Absolute, out root) ||
                (root.Scheme != Uri.UriSchemeHttps && root.Scheme != Uri.UriSchemeHttp))
                throw new ConfigurationErrorsException("작업지시서 서버 주소(WorkOrderTemplateBaseUrl)를 확인해주세요.");

            var url = new Uri(root, Uri.EscapeDataString("작업지시서_" + template + ".xlsx"));
            byte[] data;
            try { data = await Client.GetByteArrayAsync(url).ConfigureAwait(false); }
            catch (HttpRequestException ex) { throw new IOException(template + " 작업지시서 양식을 서버에서 내려받지 못했습니다.", ex); }
            catch (TaskCanceledException ex) { throw new IOException(template + " 작업지시서 서버가 응답하지 않습니다.", ex); }
            if (data.Length < 4 || data[0] != 0x50 || data[1] != 0x4b || data[2] != 3 || data[3] != 4)
                throw new InvalidDataException(template + " 작업지시서 양식이 Excel 파일이 아닙니다.");
            File.WriteAllBytes(path, data);
        }

        private static Task RunExcelAsync(Action action)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { action(); completion.SetResult(true); }
                catch (Exception ex) { completion.SetException(ex); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void BuildWorkbook(DeliveryWorkOrder order, string kind, string cover, string appendix, string output, bool asPdf)
        {
            string directory = Path.GetDirectoryName(cover);
            string left = Path.Combine(directory, "left.png");
            string right = Path.Combine(directory, "right.png");
            WriteLogo(left, "Logo_Left.png", order.OrderNo, true);
            WriteLogo(right, "Logo_Right.png", order.OrderNo, false);
            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null) throw new InvalidOperationException("작업지시서를 생성하려면 Microsoft Excel을 설치해주세요.");

            dynamic excel = null, books = null, book = null, sheets = null, sheet = null;
            dynamic appendixBook = null, appendixSheets = null, appendixTemplate = null;
            try
            {
                excel = Activator.CreateInstance(excelType);
                excel.DisplayAlerts = false;
                excel.AskToUpdateLinks = false;
                excel.AutomationSecurity = 3;
                books = excel.Workbooks;
                book = books.Open(cover, UpdateLinks: 0, ReadOnly: true, IgnoreReadOnlyRecommended: true);
                sheets = book.Worksheets;
                sheet = sheets.Item[1];
                FillCover(sheet, order, kind == "택배", left, right, directory);

                if (appendix != null)
                {
                    Release((object)sheet);
                    sheet = null;
                    appendixBook = books.Open(appendix, UpdateLinks: 0, ReadOnly: true, IgnoreReadOnlyRecommended: true);
                    appendixSheets = appendixBook.Worksheets;
                    appendixTemplate = appendixSheets.Item[1];
                    int pageCount = (order.ExtraImages.Length + ImagesPerSheet - 1) / ImagesPerSheet;
                    for (int page = 0; page < pageCount; page++)
                    {
                        dynamic previous = null, added = null;
                        try
                        {
                            previous = sheets.Item[sheets.Count];
                            appendixTemplate.Copy(After: previous);
                            added = sheets.Item[sheets.Count];
                            added.Name = pageCount == 1 ? "별지" : "별지 " + (page + 1);
                            FillAppendix(added, order, page, pageCount, left, directory);
                        }
                        finally { Release((object)added); Release((object)previous); }
                    }
                    sheet = sheets.Item[1];
                }

                book.Activate();
                sheet.Activate();
                if (asPdf)
                    book.ExportAsFixedFormat(0, output, Quality: 0, IncludeDocProperties: true,
                        IgnorePrintAreas: false, OpenAfterPublish: false);
                else
                    book.SaveAs(output, FileFormat: 51);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Excel에서 작업지시서를 생성하지 못했습니다.\r\n" + ex.Message, ex);
            }
            finally
            {
                Release((object)appendixTemplate);
                Release((object)appendixSheets);
                Release((object)sheet);
                Release((object)sheets);
                try { CloseBook((object)appendixBook); }
                finally
                {
                    try { CloseBook((object)book); }
                    finally
                    {
                        Release((object)books);
                        try { if ((object)excel != null) excel.Quit(); }
                        finally { Release((object)excel); }
                    }
                }
            }
        }

        private static void CloseBook(dynamic book)
        {
            try { if ((object)book != null) book.Close(false); }
            finally { Release((object)book); }
        }

        private static void FillCover(dynamic sheet, DeliveryWorkOrder order, bool parcel, string left, string right, string directory)
        {
            string date = order.WrittenAt.HasValue ? DeliveryWorkOrder.DateText(order.WrittenAt.Value) : "";
            SetText((object)sheet, parcel ? "C4" : "B3", date);
            SetDueDate((object)sheet, parcel ? "H4" : "G3", order.DueAt);
            SetText((object)sheet, parcel ? "C5" : "B4", order.Customer);
            SetText((object)sheet, parcel ? "G5" : "F4", order.Delivery);
            SetText((object)sheet, parcel ? "C6" : "B5", Path.GetFileNameWithoutExtension(order.GetFileName()));
            SetText((object)sheet, parcel ? "C8" : "B7", order.Equipment);
            SetText((object)sheet, parcel ? "G8" : "F7", order.Quantity);
            SetText((object)sheet, parcel ? "C9" : "B8", order.Paper);
            SetText((object)sheet, parcel ? "G9" : "F8", order.Size);
            SetText((object)sheet, parcel ? "C10" : "B9", order.Coating);
            SetText((object)sheet, parcel ? "G10" : "F9", order.Packing);
            SetText((object)sheet, parcel ? "C11" : "B10", order.Finishing);
            SetText((object)sheet, parcel ? "C12" : "B11", order.Materials);
            SetText((object)sheet, parcel ? "B30" : "A29", order.Remarks);
            ReplaceLogos((object)sheet, left, right);
            if (order.CoverImage != null)
                PlaceImage((object)sheet, parcel ? "B15:H26" : "A14:G25", order.CoverImage, Path.Combine(directory, "design.png"));
        }

        private static void FillAppendix(dynamic sheet, DeliveryWorkOrder order, int page, int pageCount, string left, string directory)
        {
            SetText((object)sheet, "I1", (page + 1) + "/" + pageCount);
            SetText((object)sheet, "B3", order.Customer);
            SetText((object)sheet, "F3", order.Delivery);
            SetDueDate((object)sheet, "I3", order.DueAt);
            ReplaceLogos((object)sheet, left);

            string[] columns = { "A", "D", "G", "J" };
            int offset = page * ImagesPerSheet;
            int count = Math.Min(ImagesPerSheet, order.ExtraImages.Length - offset);
            for (int slot = 0; slot < count; slot++)
            {
                int row = 5 + 10 * (slot / 3);
                int column = slot % 3;
                char endColumn = (char)(columns[column + 1][0] - 1);
                string address = columns[column] + row + ":" + endColumn + (row + 9);
                PlaceImage((object)sheet, address, order.ExtraImages[offset + slot],
                    Path.Combine(directory, "attachment-" + (offset + slot + 1) + ".png"));
            }
        }

        private static void SetDueDate(dynamic sheet, string address, DateTime date)
        {
            SetText((object)sheet, address, DeliveryWorkOrder.DateText(date));
            dynamic range = null;
            try { range = sheet.Range[address]; range.ShrinkToFit = true; }
            finally { Release((object)range); }
        }

        private static void ReplaceLogos(dynamic sheet, params string[] files)
        {
            dynamic shapes = null;
            try
            {
                shapes = sheet.Shapes;
                var positions = new List<RectangleF>();
                for (int index = 1; index <= (int)shapes.Count; index++)
                {
                    dynamic shape = null;
                    try
                    {
                        shape = shapes.Item(index);
                        if ((int)shape.Type == 13)
                            positions.Add(new RectangleF((float)shape.Left, (float)shape.Top, (float)shape.Width, (float)shape.Height));
                    }
                    finally { Release((object)shape); }
                }
                if (positions.Count < files.Length)
                    throw new InvalidDataException("작업지시서 양식에 바코드 그림 위치가 없습니다.");

                for (int index = (int)shapes.Count; index >= 1; index--)
                {
                    dynamic shape = null;
                    try { shape = shapes.Item(index); if ((int)shape.Type == 13) shape.Delete(); }
                    finally { Release((object)shape); }
                }
                var ordered = positions.OrderBy(item => item.Top).Take(files.Length).ToArray();
                for (int index = 0; index < files.Length; index++)
                    AddPicture((object)shapes, files[index], ordered[index]);
            }
            finally { Release((object)shapes); }
        }

        private static void PlaceImage(dynamic sheet, string address, Image image, string path)
        {
            image.Save(path, ImageFormat.Png);
            dynamic range = null, shapes = null;
            try
            {
                range = sheet.Range[address];
                shapes = sheet.Shapes;
                const float margin = 4f;
                float width = (float)range.Width, height = (float)range.Height;
                float scale = Math.Min((width - 2 * margin) / image.Width, (height - 2 * margin) / image.Height);
                float imageWidth = image.Width * scale, imageHeight = image.Height * scale;
                AddPicture((object)shapes, path, new RectangleF(
                    (float)range.Left + (width - imageWidth) / 2, (float)range.Top + (height - imageHeight) / 2, imageWidth, imageHeight));
            }
            finally { Release((object)shapes); Release((object)range); }
        }

        private static void AddPicture(dynamic shapes, string path, RectangleF position)
        {
            dynamic picture = null;
            try
            {
                picture = shapes.AddPicture(path, 0, -1, position.X, position.Y, position.Width, position.Height);
                picture.Placement = 3; // xlFreeFloating
            }
            finally { Release((object)picture); }
        }

        // Code 128 symbol widths, indexed by symbol value. The last symbol is the stop pattern.
        private static readonly string[] Code128Patterns = (
            "212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 " +
            "221312 231212 112232 122132 122231 113222 123122 123221 223211 221132 " +
            "221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 " +
            "212123 212321 232121 111323 131123 131321 112313 132113 132311 211313 " +
            "231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 " +
            "231131 213113 213311 213131 311123 311321 331121 312113 312311 332111 " +
            "314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 " +
            "112412 122114 122411 142112 142211 241211 221114 413111 241112 134111 " +
            "111242 121142 121241 114212 124112 124211 411212 421112 421211 212141 " +
            "214121 412121 111143 111341 131141 114113 114311 411113 411311 113141 " +
            "114131 311141 411131 211412 211214 211232 2331112").Split(' ');

        internal static void WriteLogo(string path, string resource, string orderNo, bool logoOnLeft)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                using (Stream logo = typeof(DeliveryWorkOrderService).Assembly.GetManifestResourceStream("PineTree.Resources." + resource))
                using (Stream output = File.Create(path))
                    logo.CopyTo(output);
                return;
            }

            const int startB = 104, stop = 106;
            var codes = new List<int> { startB };
            int checksum = startB;
            foreach (char character in orderNo)
            {
                if (character < ' ' || character > '~') throw new ArgumentException("바코드로 표시할 수 없는 주문번호입니다.");
                int code = character - ' ';
                checksum = (checksum + codes.Count * code) % 103;
                codes.Add(code);
            }
            codes.Add(checksum % 103);
            codes.Add(stop);

            using (Stream stream = typeof(DeliveryWorkOrderService).Assembly.GetManifestResourceStream("PineTree.Resources." + resource))
            using (Image logo = Image.FromStream(stream))
            using (var image = new Bitmap(logo.Width * 2, logo.Height * 2))
            using (Graphics graphics = Graphics.FromImage(image))
            using (var font = new Font("Segoe UI", 23f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.Clear(Color.White);
                graphics.DrawImage(logo, new Rectangle(0, 0, image.Width, image.Height));
                int left = logoOnLeft ? 100 : 0;
                int available = image.Width - 100;
                int modules = codes.Sum(code => Code128Patterns[code].Sum(width => width - '0'));
                int moduleWidth = Math.Min(5, available / (modules + 20));
                if (moduleWidth < 1) throw new ArgumentException("주문번호가 바코드 영역보다 깁니다.");
                int x = left + (available - modules * moduleWidth) / 2;
                foreach (int code in codes)
                {
                    string pattern = Code128Patterns[code];
                    for (int run = 0; run < pattern.Length; run++)
                    {
                        int width = (pattern[run] - '0') * moduleWidth;
                        if (run % 2 == 0) graphics.FillRectangle(Brushes.Black, x, 7, width, 60);
                        x += width;
                    }
                }
                graphics.DrawString(orderNo, font, Brushes.Black, new RectangleF(left, 67, available, 32), alignment);
                image.Save(path, ImageFormat.Png);
            }
        }

        private static void SaveLocal(string source, string destination, bool replaceExisting)
        {
            string staging = Path.Combine(Path.GetDirectoryName(destination), Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(source, staging);
                if (replaceExisting) File.Replace(staging, destination, null);
                else File.Move(staging, destination);
            }
            finally { TryDelete(staging); }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void TryDeleteEmptyDirectory(string path)
        {
            try { Directory.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
