using PineTree.Classes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogPrint : Form
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly List<Image> _thumbnailImages = new List<Image>();
        private readonly List<Button> _thumbnailButtons = new List<Button>();
        private readonly Label _status = new Label();
        private FlowLayoutPanel _thumbnailList;
        private CancellationTokenSource _pageRender;
        private WorkOrderDocument _document;
        private int _pageCount;
        private int _selectedPage;
        private int _renderVersion;
        private int _activePageRenders;
        private bool _busy;
        private bool _closeRequested;
        private DialogProgressbar _progress;

        public DialogPrint()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 900);
            MinimumSize = new Size(650, 500);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.BackColor = Color.DimGray;
            panel3.Padding = new Padding(12);
            panelThumbnails.Width = 175;
            _thumbnailList = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(6)
            };
            panelThumbnails.Controls.Add(_thumbnailList);
            _status.Dock = DockStyle.Bottom;
            _status.Height = 30;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(10, 0, 0, 0);
            _status.AutoEllipsis = true;
            Controls.Add(_status);
            _status.SendToBack();
            btnPrint.Enabled = btnExcel.Enabled = false;
            btnPrint.Click += Print_Click;
            btnExcel.Click += Excel_Click;
            Shown += DialogPrint_Shown;
            FormClosing += DialogPrint_FormClosing;
            Disposed += DialogPrint_Disposed;
        }

        public DialogPrint(DataRow orderInfo) : this()
        {
            if (orderInfo == null) throw new ArgumentNullException(nameof(orderInfo));
            DataTable snapshot = orderInfo.Table.Clone();
            snapshot.ImportRow(orderInfo);
            OrderInfo = snapshot.Rows[0];
        }

        public DataRow OrderInfo { get; private set; }

        private void DialogPrint_Shown(object sender, EventArgs e)
        {
            if (OrderInfo == null)
            {
                _status.Text = "미리보기할 주문정보가 없습니다.";
                return;
            }
            SetBusy(true, "작업지시서를 준비하는 중입니다…");
            _progress = new DialogProgressbar();
            _progress.ReportProgress(_status.Text);
            _progress.Show(this);
            // Paint both windows before starting any document/engine preparation.
            Update();
            _progress.Update();
            BeginInvoke(new Action(() => PreparePreview(this, EventArgs.Empty)));
        }

        private void ReportPreparation(string message, int? percent = null)
        {
            if (IsDisposed || !_busy || _closeRequested) return;
            _status.Text = message;
            if (_progress != null && !_progress.IsDisposed) _progress.ReportProgress(message, percent);
        }

        private async void PreparePreview(object sender, EventArgs e)
        {
            if (OrderInfo == null)
            {
                _status.Text = "미리보기할 주문정보가 없습니다.";
                return;
            }
            SetBusy(true, "작업지시서를 만드는 중입니다…");
            Task<WorkOrderDocument> creation = null;
            try
            {
                var progress = new Progress<string>(message => ReportPreparation(message));
                var warmup = WorkOrderPreviewService.WarmUpAsync(_lifetime.Token);
                creation = WorkOrderDocumentService.CreateWithProgressAsync(OrderInfo, _lifetime.Token, progress);
                // Engine extraction/verification and Excel work are independent.
                await Task.WhenAny(warmup, creation);
                if (creation.Status == TaskStatus.RanToCompletion && !warmup.IsCompleted)
                    ReportPreparation("미리보기 엔진을 준비하는 중입니다…");
                await Task.WhenAll(warmup, creation);
                var document = await creation;
                if (IsDisposed) { document.Dispose(); return; }
                _document = document;
                ReportPreparation("첫 페이지 미리보기를 만드는 중입니다…");
                var first = await WorkOrderPreviewService.RenderAsync(_document.PdfPath, 0, 1800, _lifetime.Token);
                if (_lifetime.IsCancellationRequested) { first.Image.Dispose(); return; }
                _pageCount = first.PageCount;
                ReplacePreview(first.Image);
                // The displayed page already contains everything needed for its thumbnail.
                AddThumbnail(0, new Bitmap(first.Image));
                ReportPreparation(string.Format("페이지 미리보기 준비 {0} / {1}", 1, _pageCount), 100 / _pageCount);
                for (int page = 1; page < _pageCount; page++)
                {
                    ReportPreparation(string.Format("페이지 썸네일을 만드는 중입니다… {0} / {1}", page + 1, _pageCount), page * 100 / _pageCount);
                    var thumbnail = await WorkOrderPreviewService.RenderAsync(_document.PdfPath, page, 230, _lifetime.Token);
                    if (_lifetime.IsCancellationRequested) { thumbnail.Image.Dispose(); return; }
                    AddThumbnail(page, thumbnail.Image);
                    ReportPreparation(string.Format("페이지 미리보기 준비 {0} / {1}", page + 1, _pageCount), (page + 1) * 100 / _pageCount);
                }
                SelectThumbnail(0);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_closeRequested) _status.Text = "작업지시서 미리보기 실패: " + ex.Message;
            }
            finally
            {
                if (creation != null && creation.Status == TaskStatus.RanToCompletion && _document != creation.Result)
                    creation.Result.Dispose();
                CloseProgress();
                if (!IsDisposed) SetBusy(false);
                CloseIfRequested();
            }
        }

        private void AddThumbnail(int page, Image image)
        {
            _thumbnailImages.Add(image);
            var button = new Button {
                Width = 138, Height = 195, Margin = new Padding(0, 0, 0, 8),
                Image = image, Text = (page + 1) + " / " + _pageCount,
                TextImageRelation = TextImageRelation.ImageAboveText, ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.BottomCenter, BackColor = Color.White,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                AccessibleName = (page + 1) + "페이지 미리보기"
            };
            // Keep only a thumbnail-sized bitmap per page, even for large documents.
            double scale = Math.Min(112.0 / image.Width, 158.0 / image.Height);
            var scaled = new Bitmap(image, new Size(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale))));
            _thumbnailImages[_thumbnailImages.Count - 1] = scaled;
            button.Image = scaled;
            image.Dispose();
            button.Click += async delegate { await ShowPageAsync(page); };
            _thumbnailButtons.Add(button);
            _thumbnailList.Controls.Add(button);
        }

        private async Task ShowPageAsync(int page)
        {
            if (_busy || _document == null || page < 0 || page >= _pageCount) return;
            if (page == _selectedPage && pictureBox1.Image != null && _pageRender == null) return;
            _activePageRenders++;
            int version = ++_renderVersion;
            if (_pageRender != null) _pageRender.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _pageRender = cancellation;
            SelectThumbnail(page);
            ReplacePreview(null);
            _status.Text = string.Format("{0} / {1} 페이지를 불러오는 중입니다…", page + 1, _pageCount);
            try
            {
                var result = await WorkOrderPreviewService.RenderAsync(_document.PdfPath, page, 1800, cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested || version != _renderVersion)
                {
                    result.Image.Dispose();
                    return;
                }
                ReplacePreview(result.Image);
                _status.Text = string.Format("{0} / {1} 페이지", page + 1, _pageCount);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed && version == _renderVersion) _status.Text = "페이지 미리보기 실패: " + ex.Message;
            }
            finally
            {
                if (_pageRender == cancellation) _pageRender = null;
                cancellation.Dispose();
                _activePageRenders--;
                CloseIfRequested();
            }
        }

        private void SelectThumbnail(int page)
        {
            _selectedPage = page;
            for (int i = 0; i < _thumbnailButtons.Count; i++)
            {
                _thumbnailButtons[i].FlatAppearance.BorderSize = i == page ? 3 : 1;
                _thumbnailButtons[i].FlatAppearance.BorderColor = i == page ? Color.DodgerBlue : Color.Gray;
            }
            _status.Text = string.Format("{0} / {1} 페이지", page + 1, _pageCount);
        }

        private void ReplacePreview(Image image)
        {
            var previous = pictureBox1.Image;
            pictureBox1.Image = image;
            if (previous != null) previous.Dispose();
        }

        private void Excel_Click(object sender, EventArgs e)
        {
            if (_busy || _document == null) return;
            using (var dialog = new SaveFileDialog {
                Title = "작업지시서 엑셀 저장", Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx", AddExtension = true, FileName = DefaultFileName()
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.Copy(_document.ExcelPath, dialog.FileName, true);
                    _status.Text = "엑셀 저장 완료: " + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show(this, "엑셀파일을 저장하지 못했습니다.\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private string DefaultFileName()
        {
            string name = "작업지시서_" + WorkOrderDocumentService.Text(OrderInfo, "OrderNo");
            string item = WorkOrderDocumentService.Text(OrderInfo, "OrderItemId");
            if (!string.IsNullOrEmpty(item)) name += "_" + item;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name + ".xlsx";
        }

        private async void Print_Click(object sender, EventArgs e)
        {
            if (_busy || _document == null || _pageCount < 1) return;
            using (var document = CreatePrintDocument())
            using (var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = true })
            {
                document.PrinterSettings.MinimumPage = 1;
                document.PrinterSettings.MaximumPage = _pageCount;
                document.PrinterSettings.FromPage = 1;
                document.PrinterSettings.ToPage = _pageCount;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SetBusy(true, "작업지시서를 인쇄하는 중입니다…");
                try
                {
                    await Task.Run(() => document.Print());
                    _status.Text = "인쇄 요청을 완료했습니다.";
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (!_closeRequested) MessageBox.Show(this, "인쇄하지 못했습니다.\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally { SetBusy(false); CloseIfRequested(); }
            }
        }

        private PrintDocument CreatePrintDocument()
        {
            var document = new PrintDocument { DocumentName = Path.GetFileNameWithoutExtension(DefaultFileName()), PrintController = new StandardPrintController() };
            document.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            int page = 0, last = _pageCount - 1;
            string pdf = _document.PdfPath;
            document.BeginPrint += delegate {
                bool range = document.PrinterSettings.PrintRange == PrintRange.SomePages;
                page = range ? Math.Max(0, document.PrinterSettings.FromPage - 1) : 0;
                last = range ? Math.Min(_pageCount - 1, document.PrinterSettings.ToPage - 1) : _pageCount - 1;
            };
            document.PrintPage += delegate(object sender, PrintPageEventArgs e) {
                _lifetime.Token.ThrowIfCancellationRequested();
                var result = WorkOrderPreviewService.RenderAsync(pdf, page, 3508, _lifetime.Token).GetAwaiter().GetResult();
                using (var image = result.Image)
                {
                    // Fit the exact PDF page inside the selected printer's printable area.
                    RectangleF area = e.PageSettings.PrintableArea;
                    area.Offset(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
                    float scale = Math.Min(area.Width / image.Width, area.Height / image.Height);
                    float width = image.Width * scale, height = image.Height * scale;
                    e.Graphics.DrawImage(image, area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
                }
                e.HasMorePages = ++page <= last;
            };
            return document;
        }

        private void SetBusy(bool busy, string message = null)
        {
            _busy = busy;
            UseWaitCursor = busy;
            btnExcel.Enabled = !busy && _document != null;
            btnPrint.Enabled = !busy && _document != null && _pageCount > 0;
            if (message != null) _status.Text = message;
        }

        private void DialogPrint_FormClosing(object sender, FormClosingEventArgs e)
        {
            _lifetime.Cancel();
            if (_busy || _activePageRenders > 0)
            {
                _closeRequested = true;
                e.Cancel = true;
                _status.Text = "작업을 정리하는 중입니다…";
                if (_progress != null && !_progress.IsDisposed) _progress.ReportProgress(_status.Text);
            }
        }

        private void CloseIfRequested() { if (_closeRequested && !_busy && _activePageRenders == 0 && !IsDisposed) Close(); }

        private void DialogPrint_Disposed(object sender, EventArgs e)
        {
            _lifetime.Cancel();
            CloseProgress();
            if (_pageRender != null) _pageRender.Cancel();
            ReplacePreview(null);
            foreach (var image in _thumbnailImages) image.Dispose();
            if (_document != null) _document.Dispose();
            _lifetime.Dispose();
        }

        private void CloseProgress()
        {
            var progress = _progress;
            _progress = null;
            if (progress != null) progress.Dispose();
        }
    }
}
