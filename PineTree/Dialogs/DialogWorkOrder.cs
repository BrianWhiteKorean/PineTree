using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogWorkOrder : Form
    {
        private const string QuantityPattern = @"\A(\d+(?:\.\d+)?)\s*([\p{L}㎡²]*)\z";
        private static readonly string[] DateFormats = { "yyyy-M-d", "yyyy-M-d H:mm" };
        private readonly ComboBox _delivery = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly TextBox _remarks = new TextBox { BorderStyle = BorderStyle.FixedSingle, Width = 482 };
        private readonly PictureBox[] _extraImages;
        private readonly DeliveryWorkOrder _order;
        private readonly Func<IWin32Window, Task<string>> _createOrderNumberAsync;
        private bool _generating;

        public DialogWorkOrder()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            _extraImages = new[] { pictureBox2, pictureBox3, pictureBox4, pictureBox5, pictureBox6, pictureBox7,
                pictureBox8, pictureBox9, pictureBox10, pictureBox11, pictureBox12, pictureBox13 };

            label1.Text = "배송방법";
            _delivery.Location = new Point(label1.Right + 8, 10);
            _delivery.AccessibleName = "배송방법";
            _delivery.Items.AddRange(new object[] { "택배", "창고", "퀵" });
            panel1.Controls.Add(_delivery);
            _remarks.Location = new Point(tbMaterial.Left, 262);
            _remarks.AccessibleName = "특이사항";
            c1DockingTabPage1.Controls.Add(new Label { Text = "특이사항", AutoSize = true, Location = new Point(39, 265) });
            c1DockingTabPage1.Controls.Add(_remarks);

            foreach (var input in c1DockingTabPage1.Controls.OfType<C1.Win.C1Input.C1TextBox>())
                input.TextDetached = true;
            foreach (PictureBox box in new[] { pictureBox1 }.Concat(_extraImages)) ConfigureImageSlot(box);
            Disposed += delegate
            {
                foreach (PictureBox box in new[] { pictureBox1 }.Concat(_extraImages))
                    box.Image?.Dispose();
            };
            tbFileName.ReadOnly = true;
            foreach (Control input in new Control[] { tbPaper, tbSize, tbWork, tbQty, _delivery })
                input.TextChanged += delegate { RefreshFileName(); };
            btnPreview.Click += async delegate { await ExportAsync(true); };
            btnSave.Click += async delegate { await ExportAsync(false); };
            FormClosing += (sender, e) => { if (_generating && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; };
        }

        internal DialogWorkOrder(DeliveryWorkOrder order, Func<IWin32Window, Task<string>> createOrderNumberAsync) : this()
        {
            _order = order;
            _createOrderNumberAsync = createOrderNumberAsync;
            dtmDate1.Text = order.WrittenAt.HasValue ? DeliveryWorkOrder.DateText(order.WrittenAt.Value) : "";
            dtmDate2.Text = order.DueAt == default(DateTime) ? "" : DeliveryWorkOrder.DateText(order.DueAt);
            tbCustName.Text = order.Customer ?? "";
            tbEquipment.Text = order.Equipment ?? "";
            tbPaper.Text = order.Paper ?? "";
            tbSize.Text = order.Size ?? "";
            tbQty.Text = order.Quantity ?? "";
            tbCoating.Text = order.Coating ?? "";
            tbPacking.Text = order.Packing ?? "";
            tbWork.Text = order.Finishing ?? "";
            tbMaterial.Text = order.Materials ?? "";
            _remarks.Text = order.Remarks ?? "";
            _delivery.SelectedIndex = _delivery.Items.IndexOf(DeliveryWorkOrder.DeliveryKind(order.Delivery));
            if (order.CoverImage != null) SetImage(pictureBox1, new Bitmap(order.CoverImage));
            RefreshFileName();
        }

        internal static async Task ShowSavedOrderAsync(Control owner, long orderId)
        {
            var saved = await Task.Run(() =>
            {
                DataRow header = OrderRepository.GetOrderForEdit(orderId);
                if (header == null) throw new InvalidOperationException("주문을 찾을 수 없습니다.");
                DataTable items = OrderRepository.GetOrderItemsForEdit(orderId);
                DataRow firstItem = items.Rows.Count > 0 ? items.Rows[0] : null;
                var design = OrderDesignPadState.FromJson(OrderDesignPadStateRepository.Get(orderId));
                var editor = design?.Editor ?? BannerOrderEditorState.FromJson(Convert.ToString(firstItem?["EditorStateJson"]));
                decimal width = editor?.WidthMm ?? (header.Field<decimal?>("WidthMm") ?? 0m);
                decimal height = editor?.HeightMm ?? (header.Field<decimal?>("HeightMm") ?? 0m);
                decimal quantity = editor?.Quantity ?? (header.Field<decimal?>("Qty") ?? 0m);
                string paper = design?.CategoryName;
                if (string.IsNullOrWhiteSpace(paper) || paper == "[선택]")
                    paper = design?.ProductName ?? Convert.ToString(header["ItemName"]);

                var order = new DeliveryWorkOrder
                {
                    OrderNo = Convert.ToString(header["OrderNo"]),
                    WrittenAt = header.Field<DateTime?>("OrderDate"),
                    DueAt = header.Field<DateTime?>("RequestedDueAt").GetValueOrDefault(),
                    Customer = Convert.ToString(header["CustName"]),
                    Delivery = Convert.ToString(header["DeliveryType"]),
                    Equipment = design?.EquipmentName,
                    Paper = paper,
                    Size = width > 0 && height > 0 ? FormattableString.Invariant($"{width:0.####}x{height:0.####}") : "",
                    Quantity = quantity > 0 ? $"{quantity.ToString("0.####", CultureInfo.InvariantCulture)}장" : "",
                    Finishing = editor == null ? "" : string.Join(", ", editor.GetPostProcesses().Select(process =>
                        process.ProcessCode == "WOOD" && process.Parameters.TypeCode == "TOP_BOTTOM" ? "상하각목" : process.ProcessName)),
                    Materials = string.Join(", ", items.AsEnumerable().Skip(design == null ? 1 : 0).Select(row =>
                        $"{(string.IsNullOrWhiteSpace(Convert.ToString(row["CustomName"])) ? row["ItemName"] : row["CustomName"])} {row["Qty"]:0.####}{row["UnitName"]}".Trim())),
                    Remarks = Convert.ToString(header["OrderDescription"])
                };
                return new { Order = order, CoverPage = editor?.WorkingArtboardIndex ?? 0 };
            });

            Bitmap cover = null;
            try
            {
                var snapshot = await Task.Run(() => OrderPreviewSnapshotRepository.Get(orderId));
                var page = snapshot?.Pages.ElementAtOrDefault(saved.CoverPage) ?? snapshot?.Pages.FirstOrDefault();
                if (page != null)
                {
                    string path = await OrderFileStorageService.DownloadAsync(page.ImageFile, CancellationToken.None);
                    try
                    {
                        using (Image image = Image.FromFile(path))
                            cover = new Bitmap(image);
                    }
                    finally
                    {
                        File.Delete(path);
                        Directory.Delete(Path.GetDirectoryName(path));
                    }
                }
            }
            catch (Exception ex)
            {
                if (!owner.IsDisposed)
                    MessageBox.Show(owner, "작업지시서 이미지를 불러오지 못했습니다.\r\n"
                        + OrderFileStorageService.GetTransferFailureMessage(ex), "작업지시서",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            using (cover)
            {
                if (owner.IsDisposed) return;
                saved.Order.CoverImage = cover;
                using (var dialog = new DialogWorkOrder(saved.Order, null))
                    dialog.ShowDialog(owner);
            }
        }

        private void ConfigureImageSlot(PictureBox box)
        {
            box.AllowDrop = true;
            box.SizeMode = PictureBoxSizeMode.Zoom;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Cursor = Cursors.Hand;
            var delete = new PictureBox
            {
                Image = Properties.Resources.Delete_16,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Size = new Size(20, 20),
                Location = new Point(box.ClientSize.Width - 22, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                AccessibleName = "이미지 삭제",
                Cursor = Cursors.Hand,
                Visible = false
            };
            delete.Click += delegate { SetImage(box, null); };
            box.Controls.Add(delete);
            box.Tag = delete;
            box.DoubleClick += delegate
            {
                using (var picker = new OpenFileDialog
                {
                    Title = "작업지시서 이미지 선택",
                    Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"
                })
                    if (picker.ShowDialog(this) == DialogResult.OK) LoadImage(box, picker.FileName);
            };
            box.DragEnter += (sender, e) =>
            {
                e.Effect = DropFile(e.Data) == null ? DragDropEffects.None : DragDropEffects.Copy;
            };
            box.DragDrop += (sender, e) =>
            {
                string path = DropFile(e.Data);
                if (path != null) LoadImage(box, path);
            };
        }

        private static string DropFile(IDataObject data)
        {
            var paths = data.GetData(DataFormats.FileDrop) as string[];
            return paths != null && paths.Length == 1 && File.Exists(paths[0]) ? paths[0] : null;
        }

        private void LoadImage(PictureBox box, string path)
        {
            try
            {
                using (Image source = Image.FromFile(path))
                    SetImage(box, new Bitmap(source));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                ex is ArgumentException || ex is OutOfMemoryException || ex is System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show(this, "이미지를 읽을 수 없습니다.\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void SetImage(PictureBox box, Image image)
        {
            Image previous = box.Image;
            box.Image = image;
            ((PictureBox)box.Tag).Visible = image != null;
            previous?.Dispose();
        }

        private void RefreshFileName()
        {
            tbFileName.Text = Path.GetFileNameWithoutExtension(ReadFields().GetFileName());
        }

        private DeliveryWorkOrder ReadFields()
        {
            string size = Regex.Replace(tbSize.Text.Trim(), @"\s*[xX×*]\s*", "x");
            string quantity = tbQty.Text.Trim();
            Match count = Regex.Match(quantity, QuantityPattern);
            if (count.Success) quantity = count.Groups[1].Value + (count.Groups[2].Length > 0 ? count.Groups[2].Value : "장");
            return new DeliveryWorkOrder
            {
                OrderNo = _order?.OrderNo,
                Delivery = _delivery.Text,
                Customer = tbCustName.Text.Trim(),
                Equipment = tbEquipment.Text.Trim(),
                Paper = tbPaper.Text.Trim(),
                Size = size,
                Quantity = quantity,
                Coating = tbCoating.Text.Trim(),
                Packing = tbPacking.Text.Trim(),
                Finishing = tbWork.Text.Trim(),
                Materials = tbMaterial.Text.Trim(),
                Remarks = _remarks.Text.Trim()
            };
        }

        private bool ValidateFields(DeliveryWorkOrder order)
        {
            if (order.Delivery.Length == 0)
                return InvalidField("배송방법을 선택해주세요.", _delivery);
            string[] dimensions = order.Size.Split('x');
            if (dimensions.Length != 2 || !dimensions.All(IsPositiveNumber))
                return InvalidField("가로와 세로 사이즈를 입력해주세요. 예) 5000x900", tbSize);
            Match quantity = Regex.Match(order.Quantity, QuantityPattern);
            if (!quantity.Success || !IsPositiveNumber(quantity.Groups[1].Value))
                return InvalidField("0보다 큰 수량을 입력해주세요. 예) 3장", tbQty);
            DateTime date;
            if (!DateTime.TryParseExact(dtmDate2.Text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
                return InvalidField("납기일을 입력해주세요. 예) 2026-11-19", dtmDate2);
            order.DueAt = date;
            if (!string.IsNullOrWhiteSpace(dtmDate1.Text))
            {
                if (!DateTime.TryParseExact(dtmDate1.Text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
                    return InvalidField("작성일을 날짜 형식으로 입력해주세요.", dtmDate1);
                order.WrittenAt = date;
            }
            return true;
        }

        private static bool IsPositiveNumber(string text)
        {
            decimal number;
            return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number) && number > 0;
        }

        private bool InvalidField(string message, Control control)
        {
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (control != null)
            {
                c1DockingTab1.SelectedIndex = 0;
                control.Focus();
            }
            return false;
        }

        private async Task ResolveOrderNumberAsync(DeliveryWorkOrder order, bool preview)
        {
            if (!string.IsNullOrWhiteSpace(order.OrderNo) || _createOrderNumberAsync == null) return;

            string declineText = preview ? "아니요를 선택하면 주문번호 없이 미리봅니다." : "아니요를 선택하면 파일 저장을 취소합니다.";
            if (MessageBox.Show(this, "주문번호를 생성할까요?\r\n\r\n예를 선택하면 현재 주문을 저장합니다.\r\n" + declineText,
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            order.OrderNo = await _createOrderNumberAsync(this);
            if (!string.IsNullOrWhiteSpace(order.OrderNo)) _order.OrderNo = order.OrderNo;
        }

        private async Task ExportAsync(bool preview)
        {
            if (_generating) return;
            DeliveryWorkOrder order = ReadFields();
            if (!ValidateFields(order)) return;

            bool asPdf = chkPDF.Checked;
            string destination = null;
            if (!preview)
            {
                string fileName = order.GetFileName(asPdf);
                string extension = Path.GetExtension(fileName);
                using (var picker = new SaveFileDialog
                {
                    Title = "작업지시서 저장",
                    Filter = asPdf ? "PDF 문서 (*.pdf)|*.pdf" : "Excel 통합 문서 (*.xlsx)|*.xlsx",
                    DefaultExt = extension.TrimStart('.'),
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = fileName
                })
                {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    destination = picker.FileName;
                    if (!string.Equals(Path.GetExtension(destination), extension, StringComparison.OrdinalIgnoreCase))
                    {
                        InvalidField("파일 확장자는 " + extension + "로 지정해주세요.", null);
                        return;
                    }
                }
            }

            order.CoverImage = pictureBox1.Image;
            order.ExtraImages = _extraImages.Select(box => box.Image).Where(image => image != null).ToArray();
            _generating = true;
            panel1.Enabled = c1DockingTab1.Enabled = false;
            UseWaitCursor = true;
            try
            {
                await ResolveOrderNumberAsync(order, preview);
                if (!preview && string.IsNullOrWhiteSpace(order.OrderNo)) return;

                string path = await DeliveryWorkOrderService.GenerateAsync(order, destination, asPdf);
                if (preview)
                {
                    try
                    {
                        using (Process process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })) { }
                    }
                    catch
                    {
                        DeliveryWorkOrderService.DeletePreview(path);
                        throw;
                    }
                }
                else MessageBox.Show(this, "작업지시서를 저장했습니다.\r\n" + path, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "작업지시서를 생성하지 못했습니다.\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _generating = false;
                panel1.Enabled = c1DockingTab1.Enabled = true;
                UseWaitCursor = false;
            }
        }
    }
}
