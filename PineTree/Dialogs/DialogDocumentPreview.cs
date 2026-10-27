using PineTree.PdfEngine;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public sealed class DialogDocumentPreview : Form
    {
        private readonly string _source;
        private readonly bool _outputPreview;
        private readonly PictureBox _picture = new PictureBox { BackColor = Color.DimGray, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
        private readonly PreviewViewport _viewport = new PreviewViewport { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DimGray };
        private readonly ToolStripLabel _pageLabel = new ToolStripLabel();
        private readonly ToolStripButton _previous = new ToolStripButton("이전");
        private readonly ToolStripButton _next = new ToolStripButton("다음");
        private readonly ToolStripButton _full = new ToolStripButton("전체 캔버스") { CheckOnClick = true };
        private readonly ToolStripButton _save = new ToolStripButton("파일 저장") { Name = "btnSavePdf", ToolTipText = "현재 출력용 PDF를 파일로 저장합니다." };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8), AutoEllipsis = true };
        private readonly CancellationTokenSource _saveLifetime = new CancellationTokenSource();
        private CancellationTokenSource _render;
        private readonly System.Windows.Forms.Timer _detailTimer = new System.Windows.Forms.Timer { Interval = 250 };
        private CancellationTokenSource _detailRender;
        private int _detailVersion;
        private bool _detailPending;
        private Image _detailImage;
        private Rectangle _detailBounds;
        private int _page;
        private int _pageCount = 1;
        private int _version;
        private double _zoom = 1;
        private bool _fitToWindow = true;
        private bool _saving;
        private bool _closeAfterSaving;
        private bool _resourcesDisposed;
        private string _lastSavedPath;

        public DialogDocumentPreview(string source) : this(source, false) { }

        public DialogDocumentPreview(string source, bool outputPreview)
        {
            _source = source;
            _outputPreview = outputPreview;
            Text = outputPreview ? "출력 미리보기" : "파일 미리보기 · " + Path.GetFileName(source);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 850);
            MinimumSize = new Size(500, 450);
            var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            var fit = new ToolStripButton("전체 맞춤");
            var enlarge = new ToolStripButton("확대 +");
            var reduce = new ToolStripButton("축소 −");
            enlarge.ToolTipText = "확대 (Ctrl + 마우스 휠 위)";
            reduce.ToolTipText = "축소 (Ctrl + 마우스 휠 아래)";
            toolbar.Items.AddRange(new ToolStripItem[] { _previous, _pageLabel, _next, new ToolStripSeparator(), fit, enlarge, reduce, new ToolStripSeparator(), _full });
            if (outputPreview) toolbar.Items.AddRange(new ToolStripItem[] { new ToolStripSeparator(), _save });
            _save.Enabled = outputPreview && File.Exists(source);
            _viewport.Controls.Add(_picture);
            Controls.Add(_viewport);
            Controls.Add(_status);
            Controls.Add(toolbar);
            _previous.Click += delegate { if (_page > 0) { --_page; Render(); } };
            _next.Click += delegate { if (_page + 1 < _pageCount) { ++_page; Render(); } };
            _full.CheckedChanged += delegate { Render(); };
            fit.Click += delegate { Fit(); };
            enlarge.Click += delegate { SetZoom(_zoom * 1.25); };
            reduce.Click += delegate { SetZoom(_zoom / 1.25); };
            _viewport.ZoomRequested += ZoomWithMouseWheel;
            _picture.MouseWheel += ZoomWithMouseWheel;
            _picture.MouseEnter += delegate { _viewport.Focus(); };
            _viewport.MouseEnter += delegate { _viewport.Focus(); };
            _viewport.SizeChanged += delegate { if (_fitToWindow) _zoom = FitZoom(); };
            _detailTimer.Tick += delegate { _detailTimer.Stop(); RenderDetail(); };
            _picture.Paint += delegate(object sender, PaintEventArgs e) {
                if (_detailImage != null) e.Graphics.DrawImage(_detailImage, _detailBounds);
            };
            _viewport.Scroll += delegate { ScheduleDetail(); };
            _viewport.SizeChanged += delegate { ScheduleDetail(); };
            _save.Click += SaveOutput;
            Shown += delegate { Render(); };
        }

        private async void Render()
        {
            ClearDetail();
            int version = ++_version;
            if (_render != null) _render.Cancel();
            var cancellation = new CancellationTokenSource();
            _render = cancellation;
            var previousImage = _picture.Image;
            _picture.Image = null;
            if (previousImage != null) previousImage.Dispose();
            _status.Text = "미리보기 이미지를 만드는 중입니다…";
            UpdatePages();
            try
            {
                var result = await OrderDocumentService.PreviewAsync(_source, _page, 2000, _full.Checked, cancellation.Token);
                if (IsDisposed || version != _version)
                {
                    result.PreviewImage.Dispose();
                    return;
                }
                var old = _picture.Image;
                _picture.Image = result.PreviewImage;
                if (old != null) old.Dispose();
                _pageCount = result.PageCount;
                _status.Text = result.FileInfo + Environment.NewLine + (_full.Checked ? "전체 캔버스" : result.RegionDescription);
                Fit();
                UpdatePages();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed && version == _version) _status.Text = "미리보기 실패: " + ex.Message;
            }
            finally
            {
                cancellation.Dispose();
                if (_render == cancellation) _render = null;
            }
        }

        private void UpdatePages()
        {
            _pageLabel.Text = (_page + 1) + " / " + _pageCount;
            _previous.Enabled = _page > 0;
            _next.Enabled = _page + 1 < _pageCount;
        }

        private double FitZoom()
        {
            if (_picture.Image == null) return 1;
            return Math.Min((double)Math.Max(1, _viewport.ClientSize.Width) / _picture.Image.Width,
                (double)Math.Max(1, _viewport.ClientSize.Height) / _picture.Image.Height);
        }

        private void Fit()
        {
            ClearDetail();
            _fitToWindow = true;
            _viewport.AutoScrollPosition = Point.Empty;
            _viewport.AutoScrollMinSize = Size.Empty;
            _picture.Dock = DockStyle.Fill;
            _picture.SizeMode = PictureBoxSizeMode.Zoom;
            _viewport.PerformLayout();
            _zoom = FitZoom();
            ScheduleDetail();
        }

        private void ZoomWithMouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == 0) return;
            var handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
            if (_picture.Image == null || e.Delta == 0) return;
            var control = sender as Control;
            Point anchor = control == null ? new Point(_viewport.ClientSize.Width / 2, _viewport.ClientSize.Height / 2)
                : _viewport.PointToClient(control.PointToScreen(e.Location));
            double current = _fitToWindow ? FitZoom() : _zoom;
            // One wheel notch changes zoom by only 8%; partial trackpad deltas scale proportionally.
            ZoomAt(current * Math.Pow(1.08, e.Delta / 120.0), anchor);
        }

        private void SetZoom(double value)
        {
            ZoomAt(value, new Point(_viewport.ClientSize.Width / 2, _viewport.ClientSize.Height / 2));
        }

        private void ZoomAt(double value, Point anchor)
        {
            if (_picture.Image == null) return;
            RectangleF previous;
            if (_fitToWindow)
            {
                double fit = FitZoom();
                float width = (float)(_picture.Image.Width * fit), height = (float)(_picture.Image.Height * fit);
                previous = new RectangleF((_viewport.ClientSize.Width - width) / 2, (_viewport.ClientSize.Height - height) / 2, width, height);
            }
            else previous = _picture.Bounds;
            double x = Math.Max(0, Math.Min(1, (anchor.X - previous.Left) / previous.Width));
            double y = Math.Max(0, Math.Min(1, (anchor.Y - previous.Top) / previous.Height));
            _zoom = Math.Max(.05, Math.Min(5, value));
            _fitToWindow = false;
            _viewport.AutoScrollPosition = Point.Empty;
            _picture.Dock = DockStyle.None;
            _picture.SizeMode = PictureBoxSizeMode.StretchImage;
            _picture.Size = new Size(Math.Max(1, (int)Math.Round(_picture.Image.Width * _zoom)), Math.Max(1, (int)Math.Round(_picture.Image.Height * _zoom)));
            _viewport.AutoScrollMinSize = _picture.Size;
            _viewport.PerformLayout();
            int left = Math.Max(0, (_viewport.ClientSize.Width - _picture.Width) / 2);
            int top = Math.Max(0, (_viewport.ClientSize.Height - _picture.Height) / 2);
            _picture.Location = new Point(left, top);
            _viewport.AutoScrollPosition = new Point(Math.Max(0, (int)Math.Round(left + x * _picture.Width - anchor.X)),
                Math.Max(0, (int)Math.Round(top + y * _picture.Height - anchor.Y)));
            ScheduleDetail();
        }

        private void ClearDetail()
        {
            _detailTimer.Stop();
            ++_detailVersion;
            // Movement invalidates the result, but lets the current worker finish normally.
            // Cancelling on every wheel/scroll event caused debugger cancellation breaks.
            _detailPending = false;
            var old = _detailImage; _detailImage = null;
            if (old != null) old.Dispose();
            _picture.Invalidate();
        }

        private void ScheduleDetail()
        {
            if (_resourcesDisposed) return;
            ClearDetail();
            if (_picture.Image != null && !_fitToWindow && _zoom > 1)
            {
                _detailPending = true;
                _detailTimer.Start();
            }
        }

        private async void RenderDetail()
        {
            if (_picture.Image == null || _fitToWindow || _resourcesDisposed || _detailRender != null) return;
            _detailPending = false;
            Rectangle visible = Rectangle.Intersect(_viewport.ClientRectangle, _picture.Bounds);
            if (visible.Width <= 0 || visible.Height <= 0) return;
            var bounds = new Rectangle(visible.X - _picture.Left, visible.Y - _picture.Top, visible.Width, visible.Height);
            var region = new RectangleF((float)bounds.X / _picture.Width, (float)bounds.Y / _picture.Height,
                (float)bounds.Width / _picture.Width, (float)bounds.Height / _picture.Height);
            int version = _detailVersion;
            var cancellation = new CancellationTokenSource();
            _detailRender = cancellation;
            _status.Text = "확대 영역을 선명하게 그리는 중입니다…";
            try
            {
                // Render only the visible area at 1.5 screen pixels to retain small text and vector edges.
                int pixels = Math.Min(5000, (int)Math.Ceiling(Math.Max(bounds.Width, bounds.Height) * 1.5));
                var result = await OrderDocumentService.PreviewRegionAsync(_source, _page, pixels, _full.Checked, region, cancellation.Token);
                if (result.IsCanceled) return;
                if (_resourcesDisposed || version != _detailVersion) { result.PreviewImage.Dispose(); return; }
                var old = _detailImage; _detailImage = result.PreviewImage; _detailBounds = bounds;
                if (old != null) old.Dispose();
                _picture.Invalidate(bounds);
                _status.Text = result.FileInfo + Environment.NewLine + "확대 영역 고해상도 미리보기";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_resourcesDisposed && version == _detailVersion) _status.Text = "확대 영역 미리보기 실패: " + ex.Message;
            }
            finally
            {
                if (_detailRender == cancellation) _detailRender = null;
                cancellation.Dispose();
                if (!_resourcesDisposed && _detailPending && !_detailTimer.Enabled) _detailTimer.Start();
            }
        }

        private async void SaveOutput(object sender, EventArgs e)
        {
            if (!_outputPreview || _saving) return;
            using (var dialog = new SaveFileDialog {
                Title = "출력용 PDF 저장", Filter = "PDF 문서 (*.pdf)|*.pdf", DefaultExt = "pdf", AddExtension = true,
                OverwritePrompt = true, FileName = Path.GetFileName(_source),
                InitialDirectory = string.IsNullOrEmpty(_lastSavedPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.GetDirectoryName(_lastSavedPath)
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { await SavePdfToPathAsync(dialog.FileName); }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (!IsDisposed) MessageBox.Show(this, "PDF 파일을 저장하지 못했습니다.\r\n" + ex.Message, "파일 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async Task SavePdfToPathAsync(string destination)
        {
            if (!_outputPreview) throw new InvalidOperationException("출력용 PDF 미리보기에서 저장해주세요.");
            if (_saving) throw new InvalidOperationException("PDF를 저장하는 중입니다.");
            _saving = true;
            bool previousCursor = UseWaitCursor;
            _save.Enabled = false; _save.Text = "저장 중…"; UseWaitCursor = true;
            try
            {
                var token = _saveLifetime.Token;
                await Task.Run(() => CopyPdfFile(_source, destination, token), token);
                _lastSavedPath = Path.GetFullPath(destination);
                if (!IsDisposed) _status.Text = "파일을 저장했습니다." + Environment.NewLine + _lastSavedPath;
            }
            finally
            {
                _saving = false;
                if (!IsDisposed)
                {
                    _save.Enabled = true; _save.Text = "파일 저장"; UseWaitCursor = previousCursor;
                    if (_closeAfterSaving) Close();
                }
            }
        }

        private static void CopyPdfFile(string source, string destination, CancellationToken token)
        {
            string target = Path.GetFullPath(destination);
            if (!string.Equals(Path.GetExtension(target), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("PDF 형식으로 저장해주세요.");
            if (string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("미리보기 원본과 다른 경로로 저장해주세요.");
            token.ThrowIfCancellationRequested();
            string directory = Path.GetDirectoryName(target);
            Directory.CreateDirectory(directory);
            string staging = Path.Combine(directory, ".pinetree-save-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
                    output.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                if (File.Exists(target)) File.Replace(staging, target, null);
                else File.Move(staging, target);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_saving) { _closeAfterSaving = true; _saveLifetime.Cancel(); e.Cancel = true; }
            else if (_render != null) _render.Cancel();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_resourcesDisposed)
            {
                _resourcesDisposed = true;
                if (_detailRender != null) _detailRender.Cancel();
                ClearDetail();
                _detailTimer.Dispose();
                ++_version;
                if (_render != null) _render.Cancel();
                _saveLifetime.Cancel(); _saveLifetime.Dispose();
                var image = _picture.Image; _picture.Image = null;
                if (image != null) image.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class PreviewViewport : Panel
        {
            public event MouseEventHandler ZoomRequested;
            public PreviewViewport() { SetStyle(ControlStyles.Selectable, true); TabStop = true; }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                if ((ModifierKeys & Keys.Control) == 0) { base.OnMouseWheel(e); return; }
                var handled = e as HandledMouseEventArgs;
                if (handled != null) handled.Handled = true;
                var handler = ZoomRequested;
                if (handler != null) handler(this, e);
            }
        }
    }
}
