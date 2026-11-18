using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1Input;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControlsOrder
{
    // Binds either DesignPad's existing controls or a modal host's common preview.
    public sealed class OrderSourcePreview : IDisposable
    {
        private readonly Control _host;
        private readonly PictureBox _picture;
        private readonly Panel _canvas;
        private readonly C1TextBox _info;
        private readonly ToolTip _toolTip = new ToolTip();
        private ShellFileDropWindow _shellDrop;
        private DialogProgressbar _progress;
        private OrderSourceDocument _document;
        private bool _disposed;
        private int _buttonCount = -1;
        private int _rotation, _displayRotation;
        private bool _flip, _displayFlip;
        private Image _originalImage, _transformedImage;
        public void SetTransform(int clockwiseDegrees, bool flipHorizontal)
        {
            if (_rotation == clockwiseDegrees && _flip == flipHorizontal) return;
            _rotation = clockwiseDegrees; _flip = flipHorizontal;
            Render();
        }
        private void UpdatePicture()
        {
            Image original = _document == null ? null : _document.CurrentImage;
            if (ReferenceEquals(original, _originalImage) && _rotation == _displayRotation && _flip == _displayFlip) return;
            Image previous = _transformedImage;
            _transformedImage = null;
            if (original != null && (_rotation != 0 || _flip))
            {
                _transformedImage = (Image)original.Clone();
                _transformedImage.RotateFlip((RotateFlipType)(_rotation / 90));
                if (_flip) _transformedImage.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            _picture.Image = _transformedImage ?? original;
            _originalImage = original; _displayRotation = _rotation; _displayFlip = _flip;
            if (previous != null) previous.Dispose();
        }

        public OrderSourcePreview(Control host, PictureBox picture, Panel canvas, C1TextBox info)
        {
            _host = host; _picture = picture; _canvas = canvas; _info = info;
            _picture.SizeMode = PictureBoxSizeMode.Zoom;
            _canvas.AutoScroll = true;
            _info.ReadOnly = true; _info.ScrollBars = ScrollBars.Vertical;
            _picture.DragEnter += DragEnter;
            _picture.DragOver += DragEnter;
            _picture.DragDrop += DragDrop;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                InitializeFileDrop(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
            }
            _host.VisibleChanged += HostVisibleChanged;
            _host.Disposed += HostDisposed;
            Render();
        }

        private void InitializeFileDrop(bool elevated)
        {
            if (_shellDrop != null) { _shellDrop.Dispose(); _shellDrop = null; }
            _picture.AllowDrop = !elevated;
            if (elevated) _shellDrop = new ShellFileDropWindow(_picture, FilesDropped);
        }

        public void Bind(OrderSourceDocument document)
        {
            if (_document == document) return;
            if (_document != null) _document.Changed -= DocumentChanged;
            _document = document;
            if (_document != null) _document.Changed += DocumentChanged;
            _buttonCount = -1;
            Render();
            EnsureLoaded();
        }
        private void HostVisibleChanged(object sender, EventArgs e) { Render(); EnsureLoaded(); }
        private void HostDisposed(object sender, EventArgs e) { Dispose(); }
        private async void EnsureLoaded()
        {
            if (!_disposed && _host.Visible && _host.FindForm() != null && _document != null)
                await _document.EnsureLoadedAsync();
        }
        private void DocumentChanged(object sender, EventArgs e) { Render(); }
        private void DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = _document != null && !_document.OutputBusy && Supported(e.Data) != null
                && (e.AllowedEffect & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;
        }
        private static string Supported(IDataObject data)
        {
            return data != null && data.GetDataPresent(DataFormats.FileDrop)
                ? OrderSourceDocument.SupportedFile(data.GetData(DataFormats.FileDrop) as string[]) : null;
        }
        private async void DragDrop(object sender, DragEventArgs e)
        {
            string path = Supported(e.Data);
            if (path != null && _document != null && !_document.OutputBusy) await _document.LoadAsync(path);
        }
        private async void FilesDropped(string[] files)
        {
            string path = OrderSourceDocument.SupportedFile(files);
            if (path != null && _document != null && !_document.OutputBusy) await _document.LoadAsync(path);
        }

        private void Render()
        {
            if (_disposed || _host.IsDisposed) return;
            UpdatePicture();
            _info.Text = _document == null || string.IsNullOrEmpty(_document.FileInfo)
                ? "원본 PDF / EPS / AI / PNG / JPG 파일을 아래로 끌어 놓으세요." : _document.FileInfo;
            if (_picture.Image != null && (_rotation != 0 || _flip))
                _info.Text += string.Format(" · 표시: 시계방향 {0}도{1}", _rotation, _flip ? " / 좌우대칭" : "");
            _toolTip.SetToolTip(_info, _document == null ? _info.Text : _document.ReferencePath + Environment.NewLine + _info.Text);
            int count = _document == null ? 0 : _document.PageCount;
            if (_buttonCount != count)
            {
                while (_canvas.Controls.Count > 0) _canvas.Controls[0].Dispose();
                for (int i = 0; i < count; ++i)
                {
                    int page = i;
                    var button = new Button { Name = "btnArtboard" + (i + 1), Text = "대지 " + (i + 1), Tag = i,
                        Size = new Size(84, 28), Location = new Point(6 + i * 90, 3), FlatStyle = FlatStyle.Flat };
                    button.Click += async delegate { if (_document != null) await _document.SelectPageAsync(page); };
                    _canvas.Controls.Add(button);
                }
                _buttonCount = count;
            }
            foreach (Button button in _canvas.Controls)
            {
                bool selected = _document != null && (int)button.Tag == _document.WorkingPage;
                button.BackColor = selected ? Color.SteelBlue : SystemColors.Control;
                button.ForeColor = selected ? Color.White : SystemColors.ControlText;
                button.FlatAppearance.BorderSize = selected ? 3 : 1;
                button.Enabled = _document != null && !_document.IsInspecting && !_document.OutputBusy;
            }
            bool busy = _document != null && _document.IsPreviewBusy && _host.Visible;
            if (busy && _host.FindForm() != null)
            {
                if (_progress == null || _progress.IsDisposed)
                {
                    _progress = new DialogProgressbar();
                    _progress.Show(_host.FindForm());
                }
                _progress.ReportProgress(_info.Text);
            }
            else CloseProgress();
        }
        private void CloseProgress() { if (_progress != null) { _progress.Dispose(); _progress = null; } }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_document != null) _document.Changed -= DocumentChanged;
            _picture.Image = null;
            if (_transformedImage != null) _transformedImage.Dispose();
            _transformedImage = _originalImage = null;
            _picture.DragEnter -= DragEnter; _picture.DragOver -= DragEnter; _picture.DragDrop -= DragDrop;
            _host.VisibleChanged -= HostVisibleChanged; _host.Disposed -= HostDisposed;
            if (_shellDrop != null) _shellDrop.Dispose();
            CloseProgress(); _toolTip.Dispose();
        }

        // Native Shell drop support is shared here, including elevated processes.
        private sealed class ShellFileDropWindow : NativeWindow, IDisposable
        {
            private const int WmDropFiles = 0x0233, WmCopyGlobalData = 0x0049, WmCopyData = 0x004A;
            private readonly Control _target;
            private readonly Action<string[]> _onDrop;
            internal ShellFileDropWindow(Control target, Action<string[]> onDrop)
            {
                _target = target; _onDrop = onDrop;
                _target.HandleCreated += Created; _target.HandleDestroyed += Destroyed;
                if (_target.IsHandleCreated) Register();
            }
            private void Created(object sender, EventArgs e) { Register(); }
            private void Destroyed(object sender, EventArgs e) { if (Handle != IntPtr.Zero) ReleaseHandle(); }
            private void Register()
            {
                AssignHandle(_target.Handle);
                foreach (uint message in new uint[] { WmDropFiles, WmCopyGlobalData, WmCopyData })
                    if (!ChangeWindowMessageFilterEx(Handle, message, 1, IntPtr.Zero))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "파일 드롭 메시지를 등록하지 못했습니다.");
                DragAcceptFiles(Handle, true);
            }
            protected override void WndProc(ref Message message)
            {
                if (message.Msg != WmDropFiles) { base.WndProc(ref message); return; }
                string[] files = null;
                try
                {
                    if (DragQueryFile(message.WParam, uint.MaxValue, null, 0) == 1)
                    {
                        uint length = DragQueryFile(message.WParam, 0, null, 0);
                        if (length > 0 && length <= 32767)
                        {
                            var path = new StringBuilder((int)length + 1);
                            DragQueryFile(message.WParam, 0, path, (uint)path.Capacity);
                            files = new[] { path.ToString() };
                        }
                    }
                }
                finally { DragFinish(message.WParam); }
                message.Result = IntPtr.Zero;
                if (files != null) _onDrop(files);
            }
            public void Dispose()
            {
                _target.HandleCreated -= Created; _target.HandleDestroyed -= Destroyed;
                if (Handle == IntPtr.Zero) return;
                DragAcceptFiles(Handle, false);
                foreach (uint message in new uint[] { WmDropFiles, WmCopyGlobalData, WmCopyData })
                    ChangeWindowMessageFilterEx(Handle, message, 0, IntPtr.Zero);
                ReleaseHandle();
            }
            [DllImport("shell32.dll")] private static extern void DragAcceptFiles(IntPtr window, bool accept);
            [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "DragQueryFileW")]
            private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder fileName, uint size);
            [DllImport("shell32.dll")] private static extern void DragFinish(IntPtr drop);
            [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ChangeWindowMessageFilterEx(IntPtr window, uint message, uint action, IntPtr changeInfo);
        }
    }
}
