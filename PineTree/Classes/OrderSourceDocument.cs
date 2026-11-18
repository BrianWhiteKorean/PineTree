using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PineTree.PdfEngine;

namespace PineTree.Classes
{
    public sealed class OrderSourcePage
    {
        public object Details;
        public string FileType, FileInfo, CutlineColorName;
        public double SourceWidthMm, SourceHeightMm;
        public bool HasCutline;
        public double? CutlineWidthMm;
    }

    // A source is analyzed once and can be shared by independent material editors.
    public sealed class OrderSourceDocument : IDisposable
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CancellationTokenSource _operation;
        private CancellationTokenSource _revisionLifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim _pageGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<int, OrderSourcePage> _pages = new Dictionary<int, OrderSourcePage>();
        private readonly Dictionary<int, Image> _images = new Dictionary<int, Image>();
        private readonly Dictionary<int, Task> _loads = new Dictionary<int, Task>();
        private readonly List<int> _lru = new List<int>();
        private bool _disposed, _restorePending, _inspecting, _outputBusy;
        private string _downloadedPath;
        private OrderPreviewSnapshot _savedPreview;
        public bool UseSavedPreviewsOnly { get; set; }
        public event EventHandler Changed;
        public event EventHandler PageChanging;
        public int Revision { get; private set; }
        public int WorkingPage { get; private set; }
        public int PageCount { get; private set; }
        public string FilePath { get; private set; }
        public string ReferencePath { get; private set; }
        public string FileInfo { get; private set; }
        public OrderServerFile ServerFile { get; set; }
        public bool IsRestoring { get; private set; }
        public bool IsInspecting { get { return _inspecting; } }
        public bool IsBusy { get { return _inspecting || (CurrentPage == null && _loads.ContainsKey(WorkingPage)); } }
        public bool IsPreviewBusy { get { return _inspecting || _loads.ContainsKey(WorkingPage); } }
        public bool OutputBusy
        {
            get { return _outputBusy; }
            set { _outputBusy = value; Notify(); }
        }
        public OrderSourcePage CurrentPage { get { OrderSourcePage page; return _pages.TryGetValue(WorkingPage, out page) ? page : null; } }
        public Image CurrentImage { get { Image image; return _images.TryGetValue(WorkingPage, out image) ? image : null; } }

        public static string SupportedFile(string[] files)
        {
            if (files == null || files.Length != 1 || !File.Exists(files[0])) return null;
            switch (Path.GetExtension(files[0]).ToLowerInvariant())
            {
                case ".pdf": case ".eps": case ".ai": case ".png": case ".jpg": case ".jpeg": return files[0];
                default: return null;
            }
        }

        public void Restore(BannerOrderEditorState state)
        {
            Reset();
            IsRestoring = true;
            ReferencePath = FilePath = state.SourceFilePath;
            ServerFile = state.SourceServerFile == null ? null : state.SourceServerFile.Copy();
            if (ServerFile != null) ServerFile.Validate();
            FileInfo = state.SourceFileInfo;
            WorkingPage = Math.Max(0, state.WorkingArtboardIndex);
            _restorePending = !UseSavedPreviewsOnly && (ServerFile != null || (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath)));
            Notify();
        }

        public Task EnsureLoadedAsync()
        {
            if (!_restorePending || _disposed) return Task.CompletedTask;
            _restorePending = false;
            return UseSavedPreviewsOnly ? QueuePageAsync(WorkingPage) : LoadCoreAsync(true);
        }

        public Task LoadAsync(string path)
        {
            if (_disposed || OutputBusy) return Task.CompletedTask;
            if (SupportedFile(new[] { path }) == null) throw new ArgumentException("지원하는 원본파일을 선택해주세요.");
            Reset();
            UseSavedPreviewsOnly = false;
            ReferencePath = FilePath = path;
            FileInfo = Path.GetFileName(path) + Environment.NewLine + "파일을 분석하는 중입니다…";
            return LoadCoreAsync(false);
        }

        // A modal order uses only DB metadata and server PNGs. Missing caches never fall back to analysis.
        public void SetSavedPreview(OrderPreviewSnapshot snapshot)
        {
            _savedPreview = snapshot;
            if (snapshot == null || snapshot.Pages.Count == 0)
            {
                _restorePending = false;
                IsRestoring = false;
                FileInfo = (FileInfo ?? string.Empty) + "\r\n저장된 미리보기가 없습니다. 원본을 다시 불러온 뒤 저장해주세요.";
                Notify();
                return;
            }
            PageCount = snapshot.Pages.Count;
            WorkingPage = Math.Min(WorkingPage, PageCount - 1);
            _pages.Clear();
            foreach (var page in snapshot.Pages) _pages[page.PageIndex] = ToPage(page.Analysis);
            FileInfo = snapshot.SourceFileInfo;
            IsRestoring = true;
            _restorePending = true;
            Notify();
        }

        public async Task<OrderPreviewSnapshot> CapturePreviewSnapshotAsync(CancellationToken token)
        {
            if (UseSavedPreviewsOnly) return _savedPreview;
            if (string.IsNullOrEmpty(FilePath)) return new OrderPreviewSnapshot();
            if (_inspecting || PageCount == 0) throw new InvalidOperationException("원본파일 분석이 완료된 후 저장해주세요.");
            int revision = Revision;
            var snapshot = new OrderPreviewSnapshot { SourceFileInfo = FileInfo };
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, _revisionLifetime.Token))
            {
                for (int page = 0; page < PageCount; ++page)
                {
                    lifetime.Token.ThrowIfCancellationRequested();
                    await QueuePageAsync(page);
                    lifetime.Token.ThrowIfCancellationRequested();
                    if (_disposed || revision != Revision) throw new OperationCanceledException();
                    Image image; OrderSourcePage metadata;
                    if (!_images.TryGetValue(page, out image) || !_pages.TryGetValue(page, out metadata))
                        throw new InvalidOperationException("대지 " + (page + 1) + "의 미리보기를 저장하지 못했습니다.");
                    string path = Path.Combine(Path.GetTempPath(), "PineTreeOrderPreview", Guid.NewGuid().ToString("N"), "page-" + (page + 1) + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    try
                    {
                        // Store the complete native bitmap, never a viewport screenshot or resized image.
                        using (var bitmap = new Bitmap(image)) bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        var file = await OrderFileStorageService.UploadAsync(path, lifetime.Token);
                        snapshot.Pages.Add(new OrderPreviewPageSnapshot {
                            PageIndex = page, PixelWidth = image.Width, PixelHeight = image.Height,
                            ImageFile = file, Analysis = (OrderDocumentResult)metadata.Details
                        });
                    }
                    finally { DeleteDownloaded(path); TryDeleteDirectory(Path.GetDirectoryName(path)); }
                }
            }
            return snapshot;
        }

        // Original bytes are needed only for an explicit output-generation request, never when opening a modal.
        public async Task<string> EnsureSourceFileAvailableAsync(CancellationToken token)
        {
            if (!UseSavedPreviewsOnly && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath)) return FilePath;
            if (!string.IsNullOrEmpty(_downloadedPath) && File.Exists(_downloadedPath)) return _downloadedPath;
            if (ServerFile != null)
            {
                using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, _revisionLifetime.Token))
                {
                    string path = await OrderFileStorageService.DownloadAsync(ServerFile, lifetime.Token);
                    if (_disposed || lifetime.IsCancellationRequested) { DeleteDownloaded(path); throw new OperationCanceledException(); }
                    _downloadedPath = FilePath = path;
                    Notify();
                    return path;
                }
            }
            if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath)) return FilePath;
            throw new FileNotFoundException("출력에 필요한 원본파일이 없습니다. 원본파일을 다시 선택해주세요.");
        }

        private static void TryDeleteDirectory(string directory)
        {
            try { Directory.Delete(directory, false); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private async Task LoadCoreAsync(bool restoring)
        {
            IsRestoring = restoring;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, _revisionLifetime.Token);
            _operation = cancellation;
            int version = Revision;
            string downloaded = null;
            _inspecting = true;
            Notify();
            try
            {
                if (restoring && ServerFile != null)
                {
                    FileInfo = ServerFile.OriginalFileName + " · 서버에서 원본파일을 내려받는 중입니다…";
                    Notify();
                    downloaded = await OrderFileStorageService.DownloadAsync(ServerFile, cancellation.Token);
                }
                string source = downloaded ?? FilePath;
                var result = await Task.Run(() => InspectAsync(source, cancellation.Token), cancellation.Token);
                if (_disposed || version != Revision || cancellation.IsCancellationRequested) return;
                FilePath = source;
                _downloadedPath = downloaded;
                downloaded = null;
                PageCount = result.Item2;
                _pages[0] = result.Item1;
                WorkingPage = Math.Min(WorkingPage, Math.Max(0, PageCount - 1));
                UpdateInfo();
                _inspecting = false;
                Notify();
                await QueuePageAsync(WorkingPage);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_disposed && version == Revision) { FileInfo = "원본파일을 읽지 못했습니다: " + ex.Message; Notify(); }
            }
            finally
            {
                DeleteDownloaded(downloaded);
                if (_operation == cancellation) _operation = null;
                cancellation.Dispose();
                if (!_disposed && version == Revision) { _inspecting = false; Notify(); IsRestoring = false; }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<Tuple<OrderSourcePage, int>> InspectAsync(string source, CancellationToken token)
        {
            var result = await OrderDocumentService.InspectAsync(source, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return Tuple.Create(ToPage(result), result.PageCount);
        }

        private static OrderSourcePage ToPage(OrderDocumentResult result)
        {
            return new OrderSourcePage { Details = result, FileType = result.FileType,
                FileInfo = result.FileInfo + (string.IsNullOrEmpty(result.SourceDimensionNote) ? "" : " · " + result.SourceDimensionNote),
                SourceWidthMm = result.SourceWidthMm, SourceHeightMm = result.SourceHeightMm,
                HasCutline = result.HasCutline, CutlineColorName = result.CutlineColorName, CutlineWidthMm = result.CutlineWidthMm };
        }

        public async Task SelectPageAsync(int page)
        {
            if (_disposed || _inspecting || OutputBusy || page < 0 || page >= PageCount || page == WorkingPage) return;
            var handler = PageChanging;
            if (handler != null) handler(this, EventArgs.Empty);
            WorkingPage = page;
            UpdateInfo();
            Notify();
            await QueuePageAsync(page);
        }

        private Task QueuePageAsync(int page)
        {
            if (_images.ContainsKey(page)) { Touch(page); return Task.CompletedTask; }
            Task pending;
            if (_loads.TryGetValue(page, out pending)) return pending;
            pending = LoadPageAsync(page, Revision);
            _loads[page] = pending;
            Notify();
            return pending;
        }

        private async Task LoadPageAsync(int page, int version)
        {
            bool entered = false;
            var token = _revisionLifetime.Token;
            await Task.Yield();
            try
            {
                await _pageGate.WaitAsync(token); entered = true;
                if (_disposed || version != Revision) return;
                if (UseSavedPreviewsOnly)
                {
                    if (_savedPreview == null || page >= _savedPreview.Pages.Count) return;
                    var saved = _savedPreview.Pages[page];
                    string downloaded = null;
                    Image image = null;
                    try
                    {
                        downloaded = await OrderFileStorageService.DownloadAsync(saved.ImageFile, token);
                        using (var source = Image.FromFile(downloaded)) image = new Bitmap(source);
                        if (image.Width != saved.PixelWidth || image.Height != saved.PixelHeight)
                            throw new InvalidDataException("저장된 미리보기 이미지의 크기가 올바르지 않습니다.");
                        if (_disposed || version != Revision || token.IsCancellationRequested) return;
                        _images[page] = image;
                        image = null;
                    }
                    finally
                    {
                        if (image != null) image.Dispose();
                        DeleteDownloaded(downloaded);
                        if (downloaded != null) TryDeleteDirectory(Path.GetDirectoryName(downloaded));
                    }
                }
                else
                {
                    var result = await OrderDocumentService.PreviewPageAsync(FilePath, page, 1600, false, token);
                    if (_disposed || version != Revision || result.IsCanceled)
                    { if (result.PreviewImage != null) result.PreviewImage.Dispose(); return; }
                    _pages[page] = ToPage(result);
                    if (result.PreviewImage != null) _images[page] = result.PreviewImage;
                }
                Touch(page);
                if (page == WorkingPage) { UpdateInfo(); Notify(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_disposed && version == Revision && page == WorkingPage)
                    FileInfo = (UseSavedPreviewsOnly ? (_savedPreview == null ? FileInfo : _savedPreview.SourceFileInfo) + "\r\n" : string.Empty)
                        + "대지 미리보기 실패: " + ex.Message;
            }
            finally
            {
                if (entered) _pageGate.Release();
                if (!_disposed && version == Revision) { _loads.Remove(page); if (UseSavedPreviewsOnly) IsRestoring = false; Notify(); }
            }
        }

        private void Touch(int page)
        {
            if (!_images.ContainsKey(page)) return;
            _lru.Remove(page); _lru.Add(page);
            while (_lru.Count > 4)
            {
                int oldest = _lru.FindIndex(candidate => candidate != WorkingPage && candidate != page);
                if (oldest < 0) break;
                int key = _lru[oldest]; _lru.RemoveAt(oldest);
                _images[key].Dispose(); _images.Remove(key);
            }
        }

        private void UpdateInfo()
        {
            var page = CurrentPage;
            FileInfo = page == null ? "대지 " + (WorkingPage + 1) + " 미리보기를 만드는 중입니다…"
                : Path.GetFileName(ReferencePath ?? FilePath) + " · " + page.FileType + Environment.NewLine
                    + "대지 " + (WorkingPage + 1) + " / " + PageCount + " · " + page.FileInfo;
        }

        public void Reset()
        {
            ++Revision;
            if (_operation != null) _operation.Cancel();
            _operation = null;
            _revisionLifetime.Cancel(); _revisionLifetime.Dispose();
            _revisionLifetime = new CancellationTokenSource();
            _inspecting = _restorePending = IsRestoring = false;
            FilePath = ReferencePath = FileInfo = null; ServerFile = null;
            _savedPreview = null;
            WorkingPage = PageCount = 0;
            _pages.Clear(); _loads.Clear();
            var images = new List<Image>(_images.Values);
            _images.Clear(); _lru.Clear();
            // Notify views before releasing images that may still be displayed.
            Notify();
            foreach (var image in images) image.Dispose();
            DeleteDownloaded(_downloadedPath); _downloadedPath = null;
            Notify();
        }

        private static void DeleteDownloaded(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private void Notify() { if (!_disposed) { var handler = Changed; if (handler != null) handler(this, EventArgs.Empty); } }
        public void Dispose()
        {
            if (_disposed) return;
            Reset(); _disposed = true;
            _revisionLifetime.Cancel(); _revisionLifetime.Dispose();
            _lifetime.Cancel(); _lifetime.Dispose();
        }
    }
}
