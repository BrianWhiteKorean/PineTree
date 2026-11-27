using C1.Win.C1InputPanel;
using PineTree.Dialogs;
using PineTree.Classes;
using PineTree.PdfEngine;
using System;
using System.Drawing;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PineTree.UserControlsOrder
{
    public partial class UcOrder현수막 : UserControl, IOrderItemEditor
    {
        private readonly CancellationTokenSource _documentLifetime = new CancellationTokenSource();
        private readonly ToolTip _fileInfoToolTip = new ToolTip();
        private CancellationTokenSource _sourceInspection;
        // 편집 화면을 표시할 때는 대용량 PDF 엔진 어셈블리를 로드하지 않는다.
        private object _sourceDetails;
        private string _sourcePath;
        private string _sourceFileReferencePath;
        private string _downloadedSourcePath;
        private OrderServerFile _sourceServerFile;
        private OrderServerFile _outputServerFile;
        private int _sourceVersion;
        private bool _inspecting;
        private bool _generating;
        private string _serverUploadWarning;
        private bool _documentsDisposed;
        private string _pdfSummary;
        private ShellFileDropWindow _shellDropWindow;
        private DialogProgressbar _sourceProgress;
        private bool _updatingInputs;
        private bool _sourceHasCutline;
        private decimal _sourceWidthMm;
        private decimal _sourceHeightMm;
        private string _restoredPreviewPath;
        private string _sourceFileInfoSnapshot;
        private CancellationTokenSource _sourcePageLifetime;
        private readonly Dictionary<int, Task> _sourcePageLoads = new Dictionary<int, Task>();
        private readonly Dictionary<int, string> _sourcePageInfo = new Dictionary<int, string>();
        private readonly List<int> _sourcePageLru = new List<int>();
        private readonly SemaphoreSlim _sourcePageGate = new SemaphoreSlim(1, 1);
        private bool _buildingSourceTabs;
        private bool _elevatedFileDrop;
        private readonly List<ShellFileDropWindow> _pageDropWindows = new List<ShellFileDropWindow>();

        public event EventHandler OrderValuesChanged;
        public bool IsProcessing { get { return _inspecting || _generating; } }
        public decimal WidthMm { get { return (decimal)PdfNumber(tbTargetWidth, 0); } }
        public decimal HeightMm { get { return (decimal)PdfNumber(tbTargetHeight, 0); } }
        public decimal Quantity { get { return (decimal)PdfNumber(inputNumericBox4, 0); } }
        public decimal AreaQuantity { get { return WidthMm * HeightMm * Quantity / 1000000m; } }

        public UcOrder현수막()
        {
            InitializeComponent();
            tabControl1.SelectedIndexChanged += SourcePageChanged;
            _updatingInputs = true;
            InitializePdfInputs();
            InitializePostProcesses();
            _updatingInputs = false;
            tbFileInfo.ReadOnly = true;
            tbFileInfo.ScrollBars = ScrollBars.Vertical;
            tbFileInfo.Text = "원본 PDF / EPS / AI / PNG / JPG 파일을 아래로 끌어 놓으세요.";
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.BackColor = Color.DimGray;
            pictureBox1.DragEnter += Source_DragEnter;
            pictureBox1.DragOver += Source_DragEnter;
            pictureBox1.DragDrop += Source_DragDrop;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                InitializeFileDrop(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
            }
            btnPreview.Click += PreviewOutput;
            btnPreview.ToolTipText = "입력한 공통 조건으로 출력용 PDF를 생성합니다. AI의 PDF 호환 페이지는 모두 포함합니다.";
            Load += BeginRestoredPreview;
            VisibleChanged += BeginRestoredPreview;
            UpdateDocumentState();
        }

        public BannerOrderEditorState CaptureEditorState()
        {
            var state = new BannerOrderEditorState {
                WidthMm = WidthMm, HeightMm = HeightMm, Quantity = Quantity,
                ZoomType = CaptureZoomType(), ZoomRate = ReadZoomRate(),
                FitMode = cbDeploy.SelectedOption == inputOption5 ? "COVER" : "CONTAIN",
                EdgeTopMm = (decimal)PdfNumber(tbEdgeTop, 0), EdgeBottomMm = (decimal)PdfNumber(tbEdgeBottom, 0),
                EdgeLeftMm = (decimal)PdfNumber(tbEdgeLeft, 0), EdgeRightMm = (decimal)PdfNumber(tbEdgeRight, 0),
                PostProcesses = OrderPostProcessSetting.CopyList(_postProcesses),
                SourceFilePath = _sourceFileReferencePath ?? _sourcePath,
                SourceServerFile = _sourceServerFile == null ? null : _sourceServerFile.Copy(),
                OutputServerFile = _outputServerFile == null ? null : _outputServerFile.Copy(),
                SourceFileInfo = string.IsNullOrEmpty(_sourcePath) ? null : (_sourceFileInfoSnapshot ?? tbFileInfo.Text),
                SourceWidthMm = _sourceWidthMm, SourceHeightMm = _sourceHeightMm, SourceHasCutline = _sourceHasCutline
            };
            ExportLegacyFinishingSettings(state);
            return state;
        }

        public void RestoreEditorState(BannerOrderEditorState state)
        {
            if (state == null) return;
            ++_sourceVersion;
            if (_sourceInspection != null) _sourceInspection.Cancel();
            _restoredPreviewPath = null;
            _sourceServerFile = state.SourceServerFile == null ? null : state.SourceServerFile.Copy();
            _outputServerFile = state.OutputServerFile == null ? null : state.OutputServerFile.Copy();
            if (_sourceServerFile != null) _sourceServerFile.Validate();
            _inspecting = false;
            CloseSourceProgress();
            ReplaceSourceImage(null);
            _updatingInputs = true;
            try
            {
                SetRestoredNumber(tbTargetWidth, RoundDimension(state.WidthMm)); SetRestoredNumber(tbTargetHeight, RoundDimension(state.HeightMm));
                cbZoomType.SelectedOption = state.ZoomType == "FIXED_SIZE" ? inputOption13
                    : state.ZoomType == "SCALE" ? inputOption7 : inputOption6;
                SetRestoredNumber(tbZoomRate, state.ZoomRate >= 1 ? state.ZoomRate : 1);
                SetRestoredNumber(inputNumericBox4, state.Quantity);
                cbDeploy.SelectedOption = state.FitMode == "COVER" ? inputOption5 : inputOption4;
                SetRestoredNumber(tbEdgeTop, state.EdgeTopMm); SetRestoredNumber(tbEdgeBottom, state.EdgeBottomMm);
                SetRestoredNumber(tbEdgeLeft, state.EdgeLeftMm); SetRestoredNumber(tbEdgeRight, state.EdgeRightMm);
                _postProcesses.Clear();
                _postProcesses.AddRange(state.GetPostProcesses());
                OrderPostProcessSetting.ValidateList(_postProcesses);
                RefreshPostProcessGrid();
                _sourceFileInfoSnapshot = state.SourceFileInfo ?? (string.IsNullOrEmpty(state.SourceFilePath) ? null : Path.GetFileName(state.SourceFilePath));
                _sourcePath = _sourceFileReferencePath = state.SourceFilePath; _sourceWidthMm = state.SourceWidthMm; _sourceHeightMm = state.SourceHeightMm;
                _sourceHasCutline = state.SourceHasCutline;
                if ((state.ZoomType == "ORIGINAL" || state.ZoomType == "SCALE") && (_sourceWidthMm > 0 || _sourceHeightMm > 0))
                    ApplyZoomDimensions();
                _sourceDetails = null;
                if (_sourceServerFile != null)
                {
                    tbFileInfo.Text = state.SourceFileInfo ?? _sourceServerFile.OriginalFileName;
                    _restoredPreviewPath = _sourceServerFile.OriginalFileName;
                }
                else if (!string.IsNullOrEmpty(_sourcePath))
                {
                    tbFileInfo.Text = state.SourceFileInfo ?? Path.GetFileName(_sourcePath);
                    if (File.Exists(_sourcePath)) _restoredPreviewPath = _sourcePath;
                    else tbFileInfo.Text += Environment.NewLine + "원본파일을 찾을 수 없습니다. 저장된 편집정보는 유지됩니다.";
                }
            }
            finally { _updatingInputs = false; }
            // Restore only: do not recalculate or overwrite the saved order amounts.
            UpdateDocumentState();
            if (IsHandleCreated && FindForm() != null) BeginRestoredPreview(this, EventArgs.Empty);
        }

        private static void SetRestoredNumber(InputNumericBox input, decimal value)
        {
            input.Maximum = Math.Max(input.Maximum, value);
            input.Value = value;
        }

        private async void BeginRestoredPreview(object sender, EventArgs e)
        {
            if (_documentsDisposed || !Visible || string.IsNullOrEmpty(_restoredPreviewPath)) return;
            string source = _restoredPreviewPath;
            var serverFile = _sourceServerFile == null ? null : _sourceServerFile.Copy();
            bool downloaded = false;
            _restoredPreviewPath = null;
            int version = ++_sourceVersion;
            if (_sourceInspection != null) _sourceInspection.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
            _sourceInspection = cancellation;
            _inspecting = true;
            UpdateDocumentState();
            try
            {
                ShowSourceProgress();
                if (serverFile != null)
                {
                    tbFileInfo.Text = serverFile.OriginalFileName + Environment.NewLine + "서버에서 원본파일을 내려받는 중입니다…";
                    source = await OrderFileStorageService.DownloadAsync(serverFile, cancellation.Token);
                    downloaded = true;
                }
                var result = await Task.Run(() => InspectSourceInBackgroundAsync(source, cancellation.Token), cancellation.Token);
                if (_documentsDisposed || version != _sourceVersion || cancellation.IsCancellationRequested)
                {
                    if (result.PreviewImage != null) result.PreviewImage.Dispose();
                    if (downloaded) DeletePreviewFile(source);
                    return;
                }
                if (downloaded)
                {
                    DeletePreviewFile(_downloadedSourcePath);
                    _downloadedSourcePath = source;
                }
                _sourcePath = source;
                ReplaceSourceImage(result.PreviewImage);
                _sourceDetails = result.Details;
                BuildSourceTabs(result.PageCount);
                WarmSourcePages();
                // Refresh source metadata, keeping saved target sizes, quantity and finishing settings.
                _sourceWidthMm = (decimal)result.SourceWidthMm;
                _sourceHeightMm = (decimal)result.SourceHeightMm;
                _sourceHasCutline = result.HasCutline;
                // Reload the thumbnail without reapplying source defaults (especially quantity=1).
                tbFileInfo.Text = Path.GetFileName(source) + " · " + result.FileType + Environment.NewLine + result.FileInfo;
                _sourceFileInfoSnapshot = tbFileInfo.Text;
            }
            catch (OperationCanceledException) { if (downloaded) DeletePreviewFile(source); }
            catch (Exception ex)
            {
                if (downloaded) DeletePreviewFile(source);
                if (!_documentsDisposed && version == _sourceVersion)
                    tbFileInfo.Text += Environment.NewLine + "미리보기를 복원하지 못했습니다: " + ex.Message;
            }
            finally
            {
                cancellation.Dispose();
                if (_sourceInspection == cancellation) _sourceInspection = null;
                if (version == _sourceVersion) CloseSourceProgress();
                if (!_documentsDisposed && version == _sourceVersion) { _inspecting = false; UpdateDocumentState(); }
            }
        }

        private void InitializePdfInputs()
        {
            cbDeploy.DropDownStyle = InputComboBoxStyle.DropDownList;
            cbDeploy.SelectedOption = inputOption4;
            cbZoomType.SelectedOption = inputOption6;
            //tbZoomRate.Minimum = 1;
            //tbZoomRate.DecimalPlaces = 3;
            //// C1 uses the integer placeholders as an input mask; allow multi-digit rates.
            //tbZoomRate.Format = "#,##0.###";
            //tbZoomRate.Value = 1;
            cbZoomType.SelectedIndexChanged += ZoomInputChanged;
            tbZoomRate.ValueChanged += ZoomInputChanged;

            _pdfSummary = string.Empty;

            var values = new[] { tbTargetWidth, tbTargetHeight, tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight };
            decimal[] defaults = { 0m, 0m, 2m, 2m, 2m, 2m };
            for (int i = 0; i < values.Length; i++)
            {
                values[i].Minimum = 0;
                values[i].Maximum = 100000;
                values[i].DecimalPlaces = 3;
                values[i].Format = "#,##0.###";
                values[i].Value = defaults[i];
                values[i].ToolTipText = "단위 mm / 빈값 기본값: " + defaults[i];
                values[i].ValueChanged += PdfInputChanged;
            }
            tbTargetWidth.ToolTipText = "완성·재단 폭(mm). 고객사 페트 예: 600 → 사방 도련 2mm 포함 출력 폭 604mm";
            tbTargetHeight.ToolTipText = "완성·재단 길이(mm). 고객사 페트 예: 1800 → 사방 도련 2mm 포함 출력 길이 1804mm";
            foreach (var edge in new[] { tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight })
                edge.ToolTipText = "재단 경계 바깥에 추가하는 도련(mm). 고객사 샘플 기본 2mm / 입력값 유지";
            tbTargetWidth.DecimalPlaces = tbTargetHeight.DecimalPlaces = 0;
            tbTargetWidth.Format = tbTargetHeight.Format = "#,##0";
            inputNumericBox3.ReadOnly = true;
            inputNumericBox3.Maximum = decimal.MaxValue;
            inputNumericBox3.Value = 0;
            inputNumericBox3.ToolTipText = "폭 × 길이 × 수량으로 계산한 전체 면적 (㎡)";
            inputNumericBox4.Minimum = 0;
            inputNumericBox4.DecimalPlaces = 0;
            inputNumericBox4.Value = 0;
            inputNumericBox4.ToolTipText = "주문 수량. 출력용 PDF는 원본 첫 페이지를 기준으로 1쪽 생성합니다.";
            inputNumericBox4.ValueChanged += PdfInputChanged;
            cbDeploy.SelectedIndexChanged += PdfInputChanged;
        }

        private static double PdfNumber(InputNumericBox input, double fallback)
        {
            return input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text) ? fallback : Convert.ToDouble(input.Value);
        }

        private PdfGenerationSettings ReadPdfGenerationSettings()
        {
            OrderPostProcessSetting.ValidateList(_postProcesses);
            var settings = new PdfGenerationSettings
            {
                GenerateAllPages = string.Equals(Path.GetExtension(_sourcePath), ".ai", StringComparison.OrdinalIgnoreCase),
                SourceSizingMode = CaptureZoomType(), SourceScale = (double)ReadZoomRate(),
                TargetWidthMm = PdfNumber(tbTargetWidth, 0), TargetHeightMm = PdfNumber(tbTargetHeight, 0),
                FitMode = cbDeploy.SelectedOption == inputOption5 ? "COVER" : "CONTAIN",
                EdgeTopMm = PdfNumber(tbEdgeTop, 2), EdgeBottomMm = PdfNumber(tbEdgeBottom, 2),
                EdgeLeftMm = PdfNumber(tbEdgeLeft, 2), EdgeRightMm = PdfNumber(tbEdgeRight, 2),

            };
            ApplyPostProcessesToPdf(settings);
            settings.RequireSourceCutline = _sourceHasCutline && _postProcesses.Exists(item => item.ProcessCode == "CUT");
            return settings;
        }

        private void PdfInputChanged(object sender, EventArgs e)
        {
            if (_updatingInputs || _pdfSummary == null) return;
            _outputServerFile = null;
            if (sender == tbTargetWidth || sender == tbTargetHeight)
            {
                _updatingInputs = true;
                try
                {
                    SetRestoredNumber(tbTargetWidth, RoundDimension(WidthMm));
                    SetRestoredNumber(tbTargetHeight, RoundDimension(HeightMm));
                    ApplyFixedSizeHeight();
                }
                finally { _updatingInputs = false; }
            }
            UpdateDocumentState();
            if (sender == tbTargetWidth || sender == tbTargetHeight || sender == inputNumericBox4)
                RaiseOrderValuesChanged();
        }

        private static decimal RoundDimension(decimal value)
        {
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        private decimal ReadZoomRate()
        {
            return tbZoomRate.ValueIsNull || string.IsNullOrWhiteSpace(tbZoomRate.Text) ? 1m : Convert.ToDecimal(tbZoomRate.Value);
        }

        private void ApplyZoomDimensions()
        {
            if (cbZoomType.SelectedOption == inputOption13)
            {
                ApplyFixedSizeHeight();
                return;
            }
            decimal rate = cbZoomType.SelectedOption == inputOption7 ? ReadZoomRate() : 1m;
            SetRestoredNumber(tbTargetWidth, RoundDimension(_sourceWidthMm * rate));
            SetRestoredNumber(tbTargetHeight, RoundDimension(_sourceHeightMm * rate));
        }

        private void ApplyFixedSizeHeight()
        {
            if (cbZoomType.SelectedOption != inputOption13 || _sourceWidthMm <= 0 || _sourceHeightMm <= 0) return;
            SetRestoredNumber(tbTargetHeight, RoundDimension(WidthMm * _sourceHeightMm / _sourceWidthMm));
        }

        private string CaptureZoomType()
        {
            if (cbZoomType.SelectedOption == inputOption13) return "FIXED_SIZE";
            decimal rate = cbZoomType.SelectedOption == inputOption7 ? ReadZoomRate() : 1m;
            // Preserve existing/manual target sizes rather than silently resizing them on reload.
            if (WidthMm != RoundDimension(_sourceWidthMm * rate) || HeightMm != RoundDimension(_sourceHeightMm * rate)) return null;
            return cbZoomType.SelectedOption == inputOption7 ? "SCALE" : "ORIGINAL";
        }

        private void ZoomInputChanged(object sender, EventArgs e)
        {
            if (_updatingInputs) return;
            _outputServerFile = null;
            _updatingInputs = true;
            try { ApplyZoomDimensions(); }
            finally { _updatingInputs = false; }
            UpdateDocumentState();
            RaiseOrderValuesChanged();
            if (sender == cbZoomType && cbZoomType.SelectedOption == inputOption13)
                MessageBox.Show(this, "폭을 지정하면 원본 비율에 맞춰 길이가 자동으로 입력됩니다.", "확대비율",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateDocumentState()
        {
            // 이 경로는 초기 표시와 모든 입력 변경에서 호출된다.
            // PdfGenerationSettings는 실제 PDF 작업을 시작할 때만 생성한다.
            double width = PdfNumber(tbTargetWidth, 0);
            double height = PdfNumber(tbTargetHeight, 0);
            double finishedWidth = (double)FinishedWidthMm();
            double outputWidth = finishedWidth + PdfNumber(tbEdgeLeft, 2) + PdfNumber(tbEdgeRight, 2);
            double finishedHeight = (double)FinishedHeightMm();
            double outputHeight = finishedHeight + PdfNumber(tbEdgeTop, 2) + PdfNumber(tbEdgeBottom, 2);
            btnPreview.Enabled = !_generating && !_inspecting && _sourceDetails != null;
            foreach (var input in new[] { tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight, inputNumericBox4 })
                input.Enabled = !_generating;
            tbTargetWidth.Enabled = tbTargetHeight.Enabled = !_generating && cbZoomType.SelectedOption == inputOption13;
            cbDeploy.Enabled = cbWork.Enabled = btnAdd.Enabled = FGrid1.Enabled = !_generating;
            cbZoomType.Enabled = !_generating && !_inspecting && _sourceWidthMm > 0 && _sourceHeightMm > 0;
            tbZoomRate.Enabled = cbZoomType.Enabled && cbZoomType.SelectedOption == inputOption7;
            tbTargetHeight.ReadOnly = cbZoomType.SelectedOption == inputOption13;
            RefreshPostProcessGrid();
            inputNumericBox3.Value = (decimal)(width * height / 1000000 * PdfNumber(inputNumericBox4, 0));
            if (_sourceDetails == null && _sourceWidthMm == 0 && _sourceHeightMm == 0)
            {
                _pdfSummary = "출력 0 × 0 mm · 타공 0개";
                btnPreview.ToolTipText = _pdfSummary;
                return;
            }
            try
            {
                if (width <= 0 || height <= 0 || outputWidth > 14400 || outputHeight > 14400)
                    throw new ArgumentException("완성 가로·세로는 양수이며 가장자리를 포함한 출력 크기는 14,400mm 이하여야 합니다.");
                var hole = _postProcesses.Find(item => item.ProcessCode == "HOLE");
                int markCount = hole == null ? 0 : hole.GetMarkCount((decimal)finishedWidth, (decimal)finishedHeight);
                _pdfSummary = string.Format("원본 {0:0.###} × {1:0.###} mm · 완성/재단 {2:0.###} × {3:0.###} mm · 출력(도련 포함) {4:0.###} × {5:0.###} mm · 타공 {6}개 · 첫 페이지 기준",
                    _sourceWidthMm, _sourceHeightMm, finishedWidth, finishedHeight, outputWidth, outputHeight, markCount);
            }
            catch (ArgumentException ex) { _pdfSummary = ex.Message; }
            if (string.Equals(Path.GetExtension(_sourcePath), ".ai", StringComparison.OrdinalIgnoreCase) && tabControl1.TabPages.Count > 1)
                _pdfSummary += " · AI 전체 " + tabControl1.TabPages.Count + "페이지에 공통 후가공 적용";
            btnPreview.ToolTipText = _pdfSummary + "\n각목·사방줄미싱·타공·재단을 반영하고 원본과 출력용 PDF를 서버에 업로드합니다. 기타 미싱 후가공의 PDF 규칙은 확인 중입니다.";
        }

        private void InitializeFileDrop(bool elevated)
        {
            _elevatedFileDrop = elevated;
            if (_shellDropWindow != null)
            {
                _shellDropWindow.Dispose();
                _shellDropWindow = null;
            }

            // 관리자 권한에서는 일반 탐색기의 OLE 드롭이 UIPI에 의해 차단된다.
            // 이 경우 픽처박스 창에만 Shell 파일 드롭 메시지를 허용한다.
            pictureBox1.AllowDrop = !elevated;
            if (elevated) _shellDropWindow = new ShellFileDropWindow(pictureBox1, Source_FilesDropped);
        }

        private async void Source_FilesDropped(string[] files)
        {
            if (_generating || _documentsDisposed) return;
            string source = GetSupportedFile(files);
            if (source != null) await LoadSourceFileAsync(source);
        }

        private static string GetDroppedFile(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return null;
            return GetSupportedFile(data.GetData(DataFormats.FileDrop) as string[]);
        }

        private static string GetSupportedFile(string[] files)
        {
            if (files == null || files.Length != 1 || !File.Exists(files[0])) return null;
            switch (Path.GetExtension(files[0]).ToLowerInvariant())
            {
                case ".pdf": case ".eps": case ".ai": case ".png": case ".jpg": case ".jpeg": return files[0];
                default: return null;
            }
        }

        private void Source_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = !_generating && GetDroppedFile(e.Data) != null && (e.AllowedEffect & DragDropEffects.Copy) != 0
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void Source_DragDrop(object sender, DragEventArgs e)
        {
            if (_generating) return;
            string source = GetDroppedFile(e.Data);
            if (source != null) await LoadSourceFileAsync(source);
        }

        private async Task LoadSourceFileAsync(string source)
        {
            int version = ++_sourceVersion;
            if (_sourceInspection != null) _sourceInspection.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
            _sourceInspection = cancellation;
            _sourcePath = _sourceFileReferencePath = source;
            _sourceServerFile = _outputServerFile = null;
            DeletePreviewFile(_downloadedSourcePath);
            _downloadedSourcePath = null;
            _sourceFileInfoSnapshot = null;
            _restoredPreviewPath = null;
            _sourceDetails = null;
            ResetSourceMeasurements();
            _inspecting = true;
            ReplaceSourceImage(null);
            tbFileInfo.Text = Path.GetFileName(source) + Environment.NewLine + "파일을 분석하는 중입니다…";
            UpdateDocumentState();
            try
            {
                ShowSourceProgress();
                // 이 메서드와 반환 형식에는 PDF 엔진 형식을 사용하지 않는다.
                // 최초 엔진 로드/JIT도 작업 스레드에서 실행되어 진행창이 멈추지 않는다.
                var result = await Task.Run(() => InspectSourceInBackgroundAsync(source, cancellation.Token), cancellation.Token);
                if (_documentsDisposed || version != _sourceVersion || cancellation.IsCancellationRequested)
                {
                    if (result.PreviewImage != null) result.PreviewImage.Dispose();
                    return;
                }
                tbFileInfo.Text = Path.GetFileName(source) + " · " + result.FileType + Environment.NewLine + result.FileInfo;
                _sourceFileInfoSnapshot = tbFileInfo.Text;
                _fileInfoToolTip.SetToolTip(tbFileInfo, source + Environment.NewLine + result.FileInfo);
                ReplaceSourceImage(result.PreviewImage);
                _sourceDetails = result.Details;
                BuildSourceTabs(result.PageCount);
                WarmSourcePages();
                ApplySourceMeasurements(result);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_documentsDisposed && version == _sourceVersion)
                {
                    tbFileInfo.Text = Path.GetFileName(source) + Environment.NewLine + "원본파일을 읽지 못했습니다: " + ex.Message;
                    _fileInfoToolTip.SetToolTip(tbFileInfo, tbFileInfo.Text);
                }
            }
            finally
            {
                cancellation.Dispose();
                if (_sourceInspection == cancellation) _sourceInspection = null;
                if (version == _sourceVersion) CloseSourceProgress();
                if (!_documentsDisposed && version == _sourceVersion)
                {
                    _inspecting = false;
                    UpdateDocumentState();
                }
            }
        }

        private sealed class SourcePreviewData
        {
            public int PageCount;
            public object Details;
            public string FileType;
            public string FileInfo;
            public Image PreviewImage;
            public double SourceWidthMm;
            public double SourceHeightMm;
            public bool HasCutline;
            public string CutlineColorName;
            public double? CutlineWidthMm;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<SourcePreviewData> InspectSourceInBackgroundAsync(string source, CancellationToken token)
        {
            var preview = await OrderDocumentService.PreviewAsync(source, 0, 1600, false, token).ConfigureAwait(false);
            // Preview includes inspection metadata; avoid opening a large AI twice.
            var details = preview;
            if (token.IsCancellationRequested)
            {
                if (preview.PreviewImage != null) preview.PreviewImage.Dispose();
                token.ThrowIfCancellationRequested();
            }
            return new SourcePreviewData { Details = details, FileType = details.FileType, PageCount = details.PageCount,
                FileInfo = details.FileInfo + (string.IsNullOrEmpty(details.SourceDimensionNote) ? string.Empty : " · " + details.SourceDimensionNote),
                PreviewImage = preview.PreviewImage, SourceWidthMm = details.SourceWidthMm, SourceHeightMm = details.SourceHeightMm,
                HasCutline = details.HasCutline, CutlineColorName = details.CutlineColorName, CutlineWidthMm = details.CutlineWidthMm };
        }

        private void ResetSourceMeasurements()
        {
            _updatingInputs = true;
            try
            {
                _sourceWidthMm = _sourceHeightMm = 0;
                tbTargetWidth.Value = tbTargetHeight.Value = inputNumericBox4.Value = 0;
                _sourceHasCutline = false;
                _postProcesses.RemoveAll(item => item.ProcessCode == "CUT" && item.Parameters.FromSource);
                RefreshPostProcessGrid();
            }
            finally { _updatingInputs = false; }
            RaiseOrderValuesChanged();
        }

        private void ApplySourceMeasurements(SourcePreviewData source)
        {
            _updatingInputs = true;
            try
            {
                _sourceWidthMm = (decimal)source.SourceWidthMm;
                _sourceHeightMm = (decimal)source.SourceHeightMm;
                ApplyZoomDimensions();
                // A newly dropped source starts with its own size in manual-size mode too.
                if (cbZoomType.SelectedOption == inputOption13 && (WidthMm <= 0 || HeightMm <= 0))
                {
                    SetRestoredNumber(tbTargetWidth, RoundDimension(_sourceWidthMm));
                    SetRestoredNumber(tbTargetHeight, RoundDimension(_sourceHeightMm));
                    ApplyFixedSizeHeight();
                }
                inputNumericBox4.Value = 1;
                _sourceHasCutline = source.HasCutline;
                if (source.HasCutline)
                {
                    // A source cutline is already part of the vector artwork. Never generate it twice.
                    _postProcesses.RemoveAll(item => item.ProcessCode == "CUT");
                    var cut = OrderPostProcessSetting.Create("CUT");
                    cut.Parameters.FromSource = true;
                    cut.Parameters.LineWidthMm = source.CutlineWidthMm.HasValue ? (decimal)source.CutlineWidthMm.Value : .18m;
                    cut.Parameters.SpotColorName = source.CutlineColorName;
                    _postProcesses.Add(cut);
                }
                RefreshPostProcessGrid();
            }
            finally { _updatingInputs = false; }
            UpdateDocumentState();
            RaiseOrderValuesChanged();
        }

        private void RaiseOrderValuesChanged()
        {
            var handler = OrderValuesChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void ShowSourceProgress()
        {
            if (_sourceProgress != null && !_sourceProgress.IsDisposed) return;
            _sourceProgress = new DialogProgressbar();
            var owner = FindForm();
            if (owner != null) _sourceProgress.Show(owner);
            else _sourceProgress.Show();
            _sourceProgress.Update();
        }

        private void CloseSourceProgress()
        {
            var progress = _sourceProgress;
            _sourceProgress = null;
            if (progress != null) progress.Dispose();
        }

        private void ReplaceSourceImage(Image image)
        {
            if (image == null) BuildSourceTabs(1);
            var old = pictureBox1.Image;
            pictureBox1.Image = image;
            if (old != null) old.Dispose();
        }

        private void BuildSourceTabs(int count)
        {
            _buildingSourceTabs = true;
            try
            {
                if (_sourcePageLifetime != null) { _sourcePageLifetime.Cancel(); _sourcePageLifetime.Dispose(); }
                _sourcePageLifetime = _documentsDisposed ? null : CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
                _sourcePageLoads.Clear(); _sourcePageInfo.Clear(); _sourcePageLru.Clear();
                if (pictureBox1.Image != null) _sourcePageLru.Add(0);
                foreach (var window in _pageDropWindows) window.Dispose();
                _pageDropWindows.Clear();
                tabControl1.SelectedIndex = 0;
                while (tabControl1.TabPages.Count > 1)
                {
                    var page = tabControl1.TabPages[1];
                    tabControl1.TabPages.Remove(page);
                    foreach (Control control in page.Controls)
                    {
                        var picture = control as PictureBox;
                        if (picture != null && picture.Image != null) picture.Image.Dispose();
                    }
                    page.Dispose();
                }
                tabPage1.Text = "대지 1";
                for (int i = 1; i < count; i++)
                {
                    var page = new TabPage("대지 " + (i + 1));
                    var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
                        BackColor = Color.DimGray, AllowDrop = !_elevatedFileDrop };
                    picture.DragEnter += Source_DragEnter;
                    picture.DragOver += Source_DragEnter;
                    picture.DragDrop += Source_DragDrop;
                    page.Controls.Add(picture);
                    if (_elevatedFileDrop) _pageDropWindows.Add(new ShellFileDropWindow(picture, Source_FilesDropped));
                    tabControl1.TabPages.Add(page);
                }
            }
            finally { _buildingSourceTabs = false; }
        }

        private void SourcePageChanged(object sender, EventArgs e)
        {
            if (_buildingSourceTabs || _documentsDisposed || _sourceDetails == null || tabControl1.SelectedIndex < 0) return;
            int index = tabControl1.SelectedIndex;
            var picture = (PictureBox)tabControl1.SelectedTab.Controls[0];
            if (picture.Image != null)
            {
                TouchSourcePage(index);
                string info;
                tbFileInfo.Text = _sourcePageInfo.TryGetValue(index, out info) ? info : _sourceFileInfoSnapshot;
                return;
            }
            tbFileInfo.Text = "대지 " + (index + 1) + " 미리보기를 만드는 중입니다…";
            QueueSourcePage(index);
        }

        private Task QueueSourcePage(int index)
        {
            Task pending;
            if (_sourcePageLoads.TryGetValue(index, out pending)) return pending;
            if (((PictureBox)tabControl1.TabPages[index].Controls[0]).Image != null) return Task.CompletedTask;
            pending = LoadSourcePageAsync(index);
            _sourcePageLoads[index] = pending;
            return pending;
        }

        private async void WarmSourcePages()
        {
            var lifetime = _sourcePageLifetime;
            // One background worker at a time; never preload all large documents.
            for (int index = 1; index < Math.Min(4, tabControl1.TabPages.Count); index++)
            {
                if (_documentsDisposed || lifetime != _sourcePageLifetime) return;
                await QueueSourcePage(index);
            }
        }

        private void TouchSourcePage(int index)
        {
            _sourcePageLru.Remove(index); _sourcePageLru.Add(index);
            while (_sourcePageLru.Count > 4)
            {
                int oldest = _sourcePageLru.FindIndex(x => x != tabControl1.SelectedIndex && x != index);
                if (oldest < 0) break;
                int key = _sourcePageLru[oldest]; _sourcePageLru.RemoveAt(oldest);
                var picture = (PictureBox)tabControl1.TabPages[key].Controls[0];
                var image = picture.Image; picture.Image = null;
                if (image != null) image.Dispose();
                _sourcePageLoads.Remove(key); _sourcePageInfo.Remove(key);
            }
        }

        private async Task LoadSourcePageAsync(int index)
        {
            int version = _sourceVersion;
            var lifetime = _sourcePageLifetime;
            var token = lifetime.Token;
            var target = (PictureBox)tabControl1.TabPages[index].Controls[0];
            bool entered = false;
            await Task.Yield();
            try
            {
                // Serialize workers even if many uncached tabs are clicked quickly.
                await _sourcePageGate.WaitAsync(); entered = true;
                if (token.IsCancellationRequested) return;
                var result = await OrderDocumentService.PreviewPageAsync(_sourcePath, index, 1600, false, token);
                if (result.IsCanceled) return;
                if (_documentsDisposed || target.IsDisposed || version != _sourceVersion || lifetime != _sourcePageLifetime)
                { if (result.PreviewImage != null) result.PreviewImage.Dispose(); return; }
                target.Image = result.PreviewImage;
                _sourcePageInfo[index] = "대지 " + (index + 1) + " · " + result.FileInfo;
                TouchSourcePage(index);
                if (tabControl1.SelectedIndex == index) tbFileInfo.Text = _sourcePageInfo[index];
            }
            catch (Exception ex)
            {
                if (!_documentsDisposed && version == _sourceVersion && lifetime == _sourcePageLifetime && tabControl1.SelectedIndex == index)
                    tbFileInfo.Text = "대지 " + (index + 1) + " 미리보기 실패: " + ex.Message;
            }
            finally
            {
                if (entered) _sourcePageGate.Release();
                if (lifetime == _sourcePageLifetime) _sourcePageLoads.Remove(index);
            }
        }

        private async void PreviewOutput(object sender, EventArgs e)
        {
            if (_generating || _inspecting || _sourceDetails == null) return;
            string output = null;
            try
            {
                var settings = ReadPdfGenerationSettings();
                settings.Validate();
                string folder = Path.Combine(Path.GetTempPath(), "PineTreePdfPreview", Guid.NewGuid().ToString("N"));
                output = Path.Combine(folder, Path.GetFileNameWithoutExtension(_sourcePath) + "_출력용.pdf");
                var result = await GenerateAndUploadPdfAsync(output, settings);
                if (_documentsDisposed) { DeletePreviewFile(output); return; }
                if (!string.IsNullOrEmpty(_serverUploadWarning))
                    MessageBox.Show(this, "출력 PDF는 생성되었지만 서버 업로드에 실패했습니다.\r\n로컬 미리보기를 표시합니다.\r\n\r\n" + _serverUploadWarning,
                        "서버 업로드", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowOutputPreview(result.OutputPath);
            }
            catch (OperationCanceledException) { DeletePreviewFile(output); }
            catch (Exception ex)
            {
                DeletePreviewFile(output);
                if (!_documentsDisposed) MessageBox.Show(this, "출력 미리보기를 생성하지 못했습니다.\r\n" + ex.Message,
                    "출력 미리보기", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Task<OrderDocumentResult> GeneratePdfToPathAsync(string output, PdfGenerationSettings settings)
        {
            return GeneratePdfCoreAsync(output, settings, false);
        }

        private Task<OrderDocumentResult> GenerateAndUploadPdfAsync(string output, PdfGenerationSettings settings)
        {
            return GeneratePdfCoreAsync(output, settings, true);
        }

        private async Task<OrderDocumentResult> GeneratePdfCoreAsync(string output, PdfGenerationSettings settings, bool uploadToServer)
        {
            if (_generating || _inspecting || _sourceDetails == null) throw new InvalidOperationException("원본파일을 먼저 선택해주세요.");
            settings.Validate();
            _serverUploadWarning = null;
            if (uploadToServer && !OrderFileStorageService.CanUploadSource(_sourcePath))
            {
                MessageBox.Show(this, OrderFileStorageService.SourceUploadLimitMessage,
                    "서버 업로드", MessageBoxButtons.OK, MessageBoxIcon.Information);
                uploadToServer = false;
            }
            _generating = true;
            btnPreview.Text = "PDF 생성 중…";
            UpdateDocumentState();
            try
            {
                ShowSourceProgress();
                _sourceProgress.ReportProgress("PDF 미리보기를 생성하는 중입니다…");
                var result = await OrderDocumentService.GenerateAsync(_sourcePath, output, settings, _documentLifetime.Token);
                if (uploadToServer)
                {
                    btnPreview.Text = "서버 업로드 중…";
                    if (_sourceProgress != null) _sourceProgress.ReportProgress("서버에 파일을 업로드하는 중입니다…");
                    _outputServerFile = null;
                    try
                    {
                        var sourceFile = await UploadOrderFileAsync(_sourcePath);
                        // Preserve a successfully uploaded source even if the output upload fails.
                        _sourceServerFile = sourceFile;
                        _outputServerFile = await UploadOrderFileAsync(result.OutputPath);
                    }
                    catch (Exception ex)
                    {
                        _documentLifetime.Token.ThrowIfCancellationRequested();
                        _serverUploadWarning = OrderFileStorageService.GetTransferFailureMessage(ex);
                    }
                }
                return result;
            }
            finally
            {
                CloseSourceProgress();
                _generating = false;
                if (!_documentsDisposed)
                {
                    btnPreview.Text = "출력 미리보기";
                    UpdateDocumentState();
                }
            }
        }

        protected virtual Task<OrderServerFile> UploadOrderFileAsync(string path)
        {
            return OrderFileStorageService.UploadAsync(path, _documentLifetime.Token);
        }

        private void ShowOutputPreview(string path)
        {
            var preview = new DialogDocumentPreview(path, true);
            preview.FormClosed += delegate { DeletePreviewFile(path); };
            var owner = FindForm();
            if (owner != null) preview.Show(owner);
            else preview.Show();
        }

        private static void DeletePreviewFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
                string directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory)) Directory.Delete(directory, false);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void DisposeDocuments()
        {
            if (_documentsDisposed) return;
            _documentsDisposed = true;
            CloseSourceProgress();
            ++_sourceVersion;
            if (_shellDropWindow != null) _shellDropWindow.Dispose();
            _documentLifetime.Cancel();
            if (_sourceInspection != null) _sourceInspection.Cancel();
            ReplaceSourceImage(null);
            DeletePreviewFile(_downloadedSourcePath);
            _documentLifetime.Dispose();
            _fileInfoToolTip.Dispose();
        }

        private sealed class ShellFileDropWindow : NativeWindow, IDisposable
        {
            private const int WmDropFiles = 0x0233;
            private const int WmCopyGlobalData = 0x0049;
            private const int WmCopyData = 0x004A;
            private readonly Control _target;
            private readonly Action<string[]> _onDrop;

            internal ShellFileDropWindow(Control target, Action<string[]> onDrop)
            {
                _target = target;
                _onDrop = onDrop;
                _target.HandleCreated += Target_HandleCreated;
                _target.HandleDestroyed += Target_HandleDestroyed;
                if (_target.IsHandleCreated) Register();
            }

            private void Target_HandleCreated(object sender, EventArgs e) { Register(); }
            private void Target_HandleDestroyed(object sender, EventArgs e)
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }

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
                            if (DragQueryFile(message.WParam, 0, path, (uint)path.Capacity) == length)
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
                _target.HandleCreated -= Target_HandleCreated;
                _target.HandleDestroyed -= Target_HandleDestroyed;
                if (Handle == IntPtr.Zero) return;
                DragAcceptFiles(Handle, false);
                foreach (uint message in new uint[] { WmDropFiles, WmCopyGlobalData, WmCopyData })
                    ChangeWindowMessageFilterEx(Handle, message, 0, IntPtr.Zero);
                ReleaseHandle();
            }

            [DllImport("shell32.dll")]
            private static extern void DragAcceptFiles(IntPtr window, bool accept);
            [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "DragQueryFileW")]
            private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder fileName, uint size);
            [DllImport("shell32.dll")]
            private static extern void DragFinish(IntPtr drop);
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ChangeWindowMessageFilterEx(IntPtr window, uint message, uint action, IntPtr changeInfo);
        }
    }
}
