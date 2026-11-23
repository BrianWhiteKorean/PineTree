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
    /// <summary>
    /// 현수막에 사용
    /// </summary>
    public partial class UcOrder수성_그룹A : UserControl, IOrderSourceEditor
    {
        private readonly CancellationTokenSource _documentLifetime = new CancellationTokenSource();
        // 편집 화면을 표시할 때는 대용량 PDF 엔진 어셈블리를 로드하지 않는다.
        private object _sourceDetails;
        private string _sourcePath;
        private string _sourceFileReferencePath;
        private OrderServerFile _sourceServerFile;
        private OrderServerFile _outputServerFile;
        private bool _generating;
        private string _serverUploadWarning;
        private bool _documentsDisposed;
        private string _pdfSummary;
        private DialogProgressbar _sourceProgress;
        private bool _updatingInputs;
        private bool _sourceHasCutline;
        private decimal _sourceWidthMm;
        private decimal _sourceHeightMm;
        private string _sourceFileInfoSnapshot;
        private int _workingPage;
        private int _artboardCount;
        private readonly Dictionary<int, BannerOrderEditorState> _artboardStates = new Dictionary<int, BannerOrderEditorState>();


        private OrderSourceDocument _sourceDocument;
        private bool _ownsSourceDocument;
        private bool _restoringState;
        private int _sourceRevision;
        private int _appliedSourcePage = -1;
        private OrderSourcePage _appliedSourceData;
        private bool _inspecting { get { return _sourceDocument != null && _sourceDocument.IsBusy; } }

        public OrderSourceDocument SourceDocument
        {
            get
            {
                if (_sourceDocument == null)
                {
                    AttachSourceDocument(new OrderSourceDocument());
                    _ownsSourceDocument = true;
                }
                return _sourceDocument;
            }
        }

        public void AttachSourceDocument(OrderSourceDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (_sourceDocument == document) return;
            SaveWorkingSettings();
            if (_sourceDocument != null)
            {
                _sourceDocument.Changed -= SourceDocumentChanged;
                _sourceDocument.PageChanging -= SourcePageChanging;
                if (_ownsSourceDocument) _sourceDocument.Dispose();
            }
            _sourceDocument = document;
            _ownsSourceDocument = false;
            _sourceRevision = document.Revision;
            _appliedSourcePage = -1; _appliedSourceData = null;
            document.Changed += SourceDocumentChanged;
            document.PageChanging += SourcePageChanging;
            SourceDocumentChanged(document, EventArgs.Empty);
        }

        private void SourcePageChanging(object sender, EventArgs e) { SaveWorkingSettings(); }

        private void SourceDocumentChanged(object sender, EventArgs e)
        {
            if (_documentsDisposed || _restoringState) return;
            var source = _sourceDocument;
            _sourcePath = source.FilePath;
            _sourceFileReferencePath = source.ReferencePath;
            _sourceServerFile = source.ServerFile;
            _sourceFileInfoSnapshot = source.FileInfo;
            if (_sourceRevision != source.Revision)
            {
                _sourceRevision = source.Revision;
                _artboardStates.Clear(); _outputServerFile = null;
                _sourceDetails = null; _appliedSourceData = null; _appliedSourcePage = -1;
                ResetSourceMeasurements();
            }
            _workingPage = source.WorkingPage; _artboardCount = source.PageCount;
            var metadata = source.CurrentPage;
            if (metadata != null && (_appliedSourcePage != _workingPage || _appliedSourceData != metadata))
            {
                if (_appliedSourcePage == _workingPage) SaveWorkingSettings();
                _appliedSourcePage = _workingPage; _appliedSourceData = metadata;
                decimal previousWidth = WidthMm, previousHeight = HeightMm, previousQuantity = Quantity;
                ApplyWorkingArtboard(metadata);
                if (!source.IsRestoring && (previousWidth != WidthMm || previousHeight != HeightMm || previousQuantity != Quantity))
                    RaiseOrderValuesChanged();
            }
            else if (metadata == null)
            {
                _sourceDetails = null; _appliedSourcePage = -1; _appliedSourceData = null;
            }
            UpdateDocumentState();
            RaiseSourceViewChanged();
        }

        private int _sourceRotationDegreesClockwise;
        private bool _sourceFlipHorizontal;
        public event EventHandler SourceViewChanged;
        public int SourceRotationDegreesClockwise { get { return _sourceRotationDegreesClockwise; } }
        public bool SourceFlipHorizontal { get { return _sourceFlipHorizontal; } }
        private void RaiseSourceViewChanged()
        {
            var handler = SourceViewChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
        public void RotateSource(int clockwiseDegrees)
        {
            if (clockwiseDegrees % 90 != 0) throw new ArgumentException("90도 단위로 회전해주세요.");
            if (IsProcessing || (_sourceDocument != null && _sourceDocument.OutputBusy) || _sourceWidthMm <= 0 || _sourceHeightMm <= 0) return;
            int turn = ((clockwiseDegrees % 360) + 360) % 360;
            if (turn == 0) return;
            // Canonical order: rotation, then screen-horizontal reflection.
            _sourceRotationDegreesClockwise = (_sourceRotationDegreesClockwise + (_sourceFlipHorizontal ? 360-turn : turn)) % 360;
            _updatingInputs = true;
            try
            {
                if (turn % 180 != 0)
                {
                    decimal sourceWidth = _sourceWidthMm;
                    _sourceWidthMm = _sourceHeightMm; _sourceHeightMm = sourceWidth;
                    if (cbZoomType.SelectedIndex < 0)
                    {
                        decimal width = WidthMm;
                        SetRestoredNumber(tbTargetWidth, HeightMm); SetRestoredNumber(tbTargetHeight, width);
                    }
                }
                ApplyZoomDimensions();
            }
            finally { _updatingInputs = false; }
            SourceTransformEdited();
        }
        public void FlipSourceHorizontally()
        {
            if (IsProcessing || (_sourceDocument != null && _sourceDocument.OutputBusy) || _sourceWidthMm <= 0 || _sourceHeightMm <= 0) return;
            _sourceFlipHorizontal = !_sourceFlipHorizontal;
            SourceTransformEdited();
        }
        private void SourceTransformEdited()
        {
            _outputServerFile = null;
            SaveWorkingSettings(); UpdateDocumentState();
            RaiseOrderValuesChanged(); RaiseSourceViewChanged();
        }
        private void SetOrientedSourceDimensions(OrderSourcePage metadata)
        {
            bool swap = _sourceRotationDegreesClockwise % 180 != 0;
            _sourceWidthMm = (decimal)(swap ? metadata.SourceHeightMm : metadata.SourceWidthMm);
            _sourceHeightMm = (decimal)(swap ? metadata.SourceWidthMm : metadata.SourceHeightMm);
        }

        public event EventHandler OrderValuesChanged;
        public bool IsProcessing { get { return _inspecting || _generating; } }
        public decimal WidthMm { get { return (decimal)PdfNumber(tbTargetWidth, 0); } }
        public decimal HeightMm { get { return (decimal)PdfNumber(tbTargetHeight, 0); } }
        private decimal _orderQuantity = 1m;
        public decimal Quantity { get { return _orderQuantity; } }
        public void SetOrderQuantity(decimal quantity)
        {
            quantity = Math.Max(0m, quantity);
            if (_orderQuantity == quantity) return;
            _orderQuantity = quantity;
            RaiseOrderValuesChanged();
        }
        public decimal AreaQuantity { get { return WidthMm * HeightMm * Quantity / 1000000m; } }

        public UcOrder수성_그룹A()
        {
            InitializeComponent();
            _updatingInputs = true;
            InitializePdfInputs();
            InitializePostProcesses();
            _updatingInputs = false;
            btnPreview.Click += PreviewOutput;
            btnPreview.ToolTipText = "현재 선택한 대지의 설정으로 단일 페이지 출력용 PDF를 생성합니다.";
            UpdateDocumentState();
        }

        public BannerOrderEditorState CaptureEditorState()
        {
            SaveWorkingSettings();
            var state = CaptureWorkingSettings();
            state.WorkingArtboardIndex = _workingPage;
            state.Artboards = new List<BannerArtboardEditorState>();
            foreach (var item in _artboardStates)
                state.Artboards.Add(new BannerArtboardEditorState { PageIndex = item.Key, Settings = CopyWorkingSettings(item.Value) });
            return state;
        }

        private static BannerOrderEditorState CopyWorkingSettings(BannerOrderEditorState state)
        {
            var copy = BannerOrderEditorState.FromJson(state.ToJson());
            copy.Artboards = null;
            return copy;
        }

        private void SaveWorkingSettings()
        {
            if (_sourceDetails != null) _artboardStates[_workingPage] = CaptureWorkingSettings();
        }

        private BannerOrderEditorState CaptureWorkingSettings()
        {
            var state = new BannerOrderEditorState {
                WidthMm = WidthMm, HeightMm = HeightMm, Quantity = Quantity,
                ZoomType = CaptureZoomType(), ZoomRate = ReadZoomRate(),
                SourceRotationDegreesClockwise = _sourceRotationDegreesClockwise, SourceFlipHorizontal = _sourceFlipHorizontal,
                FitMode = "CONTAIN",
                EdgeTopMm = (decimal)PdfNumber(tbEdgeTop, 0), EdgeBottomMm = (decimal)PdfNumber(tbEdgeBottom, 0),
                EdgeLeftMm = (decimal)PdfNumber(tbEdgeLeft, 0), EdgeRightMm = (decimal)PdfNumber(tbEdgeRight, 0),
                PostProcesses = OrderPostProcessSetting.CopyList(_postProcesses),
                SourceFilePath = _sourceFileReferencePath ?? _sourcePath,
                SourceServerFile = _sourceDocument != null && _sourceDocument.ServerFile != null ? _sourceDocument.ServerFile.Copy() : (_sourceServerFile == null ? null : _sourceServerFile.Copy()),
                OutputServerFile = _outputServerFile == null ? null : _outputServerFile.Copy(),
                SourceFileInfo = string.IsNullOrEmpty(_sourcePath) ? null : _sourceFileInfoSnapshot,
                SourceWidthMm = _sourceWidthMm, SourceHeightMm = _sourceHeightMm, SourceHasCutline = _sourceHasCutline
            };
            ExportLegacyFinishingSettings(state);
            return state;
        }

        public void RestoreEditorState(BannerOrderEditorState state)
        {
            if (state == null) return;
            _restoringState = _updatingInputs = true;
            try
            {
                _artboardStates.Clear();
                if (state.Artboards != null)
                    foreach (var page in state.Artboards) _artboardStates[page.PageIndex] = CopyWorkingSettings(page.Settings);
                _workingPage = Math.Max(0, state.WorkingArtboardIndex);
                _artboardStates[_workingPage] = CopyWorkingSettings(state);
                ApplyWorkingInputs(state);
                _sourceWidthMm = state.SourceWidthMm; _sourceHeightMm = state.SourceHeightMm;
                _sourceHasCutline = state.SourceHasCutline;
                if (_sourceWidthMm > 0 || _sourceHeightMm > 0) ApplyZoomDimensions();
                _sourcePath = _sourceFileReferencePath = state.SourceFilePath;
                _sourceServerFile = state.SourceServerFile == null ? null : state.SourceServerFile.Copy();
                _sourceFileInfoSnapshot = state.SourceFileInfo;
                SourceDocument.Restore(state);
                _sourceRevision = _sourceDocument.Revision;
                _appliedSourcePage = -1; _appliedSourceData = null; _sourceDetails = null;
            }
            finally { _restoringState = _updatingInputs = false; }
            UpdateDocumentState();
            RaiseSourceViewChanged();
        }

        private static void SetRestoredNumber(InputNumericBox input, decimal value)
        {
            input.Maximum = Math.Max(input.Maximum, value);
            input.Value = value;
        }

        private void InitializePdfInputs()
        {
            cbZoomType.DropDownStyle = InputComboBoxStyle.DropDownList;
            cbZoomType.SelectedOption = inputOption6;
            cbZoomType.SelectedIndexChanged += ZoomInputChanged;

            _pdfSummary = string.Empty;

            var values = new[] { tbTargetWidth, tbTargetHeight, tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight };
            decimal[] defaults = { 0m, 0m, 0m, 0m, 0m, 0m };
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
            tbTargetWidth.ToolTipText = "이미지 확장 전 가로(mm). 고정사이즈에서는 가로를 입력하면 세로가 자동 계산됩니다.";
            tbTargetHeight.ToolTipText = "이미지 확장 전 세로(mm). 원본의 가로·세로 비율로 자동 계산됩니다.";
            foreach (var edge in new[] { tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight })
                edge.ToolTipText = "지정한 만큼 이미지 크기를 늘립니다(mm). 최종 가로 = 가로 + 좌측 + 우측, 최종 세로 = 세로 + 상단 + 하단";
            tbTargetWidth.DecimalPlaces = tbTargetHeight.DecimalPlaces = 0;
            tbTargetWidth.Format = tbTargetHeight.Format = "#,##0";
            inputNumericBox3.ReadOnly = true;
            inputNumericBox3.Maximum = decimal.MaxValue;
            inputNumericBox3.Value = 0;
            inputNumericBox3.ToolTipText = "폭 × 길이로 계산한 1개당 면적 (㎡). 수량은 하단 주문가격에서 적용합니다.";

        }

        private static double PdfNumber(InputNumericBox input, double fallback)
        {
            return input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text) ? fallback : Convert.ToDouble(input.Value);
        }

        private PdfGenerationSettings ReadPdfGenerationSettings()
        {
            if (cbZoomType.SelectedIndex < 0)
                throw new InvalidOperationException("원본 이미지 비율에서 1:1 또는 1/10을 선택해주세요.");
            OrderPostProcessSetting.ValidateList(_postProcesses);
            var settings = new PdfGenerationSettings
            {
                GenerateAllPages = false, SourcePageIndex = _workingPage,
                SourceRotationDegreesClockwise = _sourceRotationDegreesClockwise, SourceFlipHorizontal = _sourceFlipHorizontal,
                RoundSourceDimensionsBeforeScale = true,
                SourceSizingMode = CaptureZoomType(), SourceScale = (double)ReadZoomRate(),
                OutputRipScale = (double)ReadZoomRate(),
                ExpandImage = true,
                TargetWidthMm = PdfNumber(tbTargetWidth, 0), TargetHeightMm = PdfNumber(tbTargetHeight, 0),
                FitMode = "CONTAIN",
                EdgeTopMm = PdfNumber(tbEdgeTop, 0), EdgeBottomMm = PdfNumber(tbEdgeBottom, 0),
                EdgeLeftMm = PdfNumber(tbEdgeLeft, 0), EdgeRightMm = PdfNumber(tbEdgeRight, 0),

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
                }
                finally { _updatingInputs = false; }
            }
            UpdateDocumentState();
            if (sender == tbTargetWidth || sender == tbTargetHeight)
                RaiseOrderValuesChanged();
        }

        private static decimal RoundDimension(decimal value)
        {
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        private decimal ReadZoomRate()
        {
            return cbZoomType.SelectedOption == inputOption7 ? 10m : 1m;
        }

        private void ApplyZoomDimensions()
        {
            if (cbZoomType.SelectedIndex < 0) return;
            decimal rate = ReadZoomRate();
            SetRestoredNumber(tbTargetWidth, RoundDimension(RoundDimension(_sourceWidthMm) * rate));
            SetRestoredNumber(tbTargetHeight, RoundDimension(RoundDimension(_sourceHeightMm) * rate));
        }

        private string CaptureZoomType()
        {
            if (cbZoomType.SelectedIndex < 0) return "FIXED_SIZE";
            decimal rate = ReadZoomRate();
            // Preserve existing/manual target sizes rather than silently resizing them on reload.
            if (WidthMm != RoundDimension(RoundDimension(_sourceWidthMm) * rate) || HeightMm != RoundDimension(RoundDimension(_sourceHeightMm) * rate)) return null;
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
        }

        private void UpdateDocumentState()
        {
            bool busy = _generating || (_sourceDocument != null && _sourceDocument.OutputBusy);
            // 이 경로는 초기 표시와 모든 입력 변경에서 호출된다.
            // PdfGenerationSettings는 실제 PDF 작업을 시작할 때만 생성한다.
            double width = PdfNumber(tbTargetWidth, 0);
            double height = PdfNumber(tbTargetHeight, 0);
            double finishedWidth = (double)FinishedWidthMm();
            double outputWidth = finishedWidth + PdfNumber(tbEdgeLeft, 0) + PdfNumber(tbEdgeRight, 0);
            double finishedHeight = (double)FinishedHeightMm();
            double outputHeight = finishedHeight + PdfNumber(tbEdgeTop, 0) + PdfNumber(tbEdgeBottom, 0);
            btnPreview.Enabled = !busy && !_inspecting && _sourceDetails != null;
            bool pageReady = !_inspecting;
            foreach (var input in new[] { tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight })
                input.Enabled = !busy && pageReady;
            tbTargetWidth.Enabled = tbTargetHeight.Enabled = false;
            cbWork.Enabled = btnAdd.Enabled = FGrid1.Enabled = !busy && pageReady;
            cbZoomType.Enabled = !busy && !_inspecting && _sourceWidthMm > 0 && _sourceHeightMm > 0;
            cbZoomType.ToolTipText = cbZoomType.SelectedIndex < 0
                ? "이 주문에 저장된 기존 치수입니다. PDF를 다시 만들려면 원본 이미지 비율을 선택해주세요."
                : "미리보기에 놓은 원본 파일의 축척을 선택합니다. 1:1은 RIP 1배, 1/10은 RIP 10배로 출력합니다.";
            RefreshPostProcessGrid();
            inputNumericBox3.Value = WidthMm * HeightMm / 1000000m;
            if (_sourceDetails == null && _sourceWidthMm == 0 && _sourceHeightMm == 0)
            {
                _pdfSummary = "출력 0 × 0 mm · 타공 0개";
                btnPreview.ToolTipText = _pdfSummary;
                return;
            }
            try
            {
                if (width <= 0 || height <= 0 || outputWidth > 14400 || outputHeight > 14400)
                    throw new ArgumentException("완성 가로·세로는 양수이며 이미지 확장을 포함한 출력 크기는 14,400mm 이하여야 합니다.");
                var hole = _postProcesses.Find(item => item.ProcessCode == "HOLE");
                int markCount = hole == null ? 0 : hole.GetMarkCountForImage((decimal)finishedWidth, (decimal)finishedHeight, WidthMm, HeightMm);
                _pdfSummary = string.Format("원본 {0:0.###} × {1:0.###} mm · 완성/재단 {2:0.###} × {3:0.###} mm · 최종 출력(이미지 확장 포함) {4:0.###} × {5:0.###} mm · 타공 {6}개 · 선택 대지 기준",
                    _sourceWidthMm, _sourceHeightMm, finishedWidth, finishedHeight, outputWidth, outputHeight, markCount);
                decimal ripScale = ReadZoomRate();
                if (ripScale > 1m)
                    _pdfSummary += $" · 전달 PDF {(decimal)outputWidth / ripScale:0.###} × {(decimal)outputHeight / ripScale:0.###} mm · RIP {ripScale:0.###}배";
            }
            catch (ArgumentException ex) { _pdfSummary = ex.Message; }
            if (_artboardCount > 0) _pdfSummary += " · 대지 " + (_workingPage + 1) + " / " + _artboardCount;
            btnPreview.ToolTipText = _pdfSummary + "\n각목·사방줄미싱·타공·재단을 반영하고 원본과 출력용 PDF를 서버에 업로드합니다. 기타 미싱 후가공의 PDF 규칙은 확인 중입니다.";
        }

        private void ResetSourceMeasurements()
        {
            _updatingInputs = true;
            try
            {
                _sourceRotationDegreesClockwise = 0; _sourceFlipHorizontal = false;
                _sourceWidthMm = _sourceHeightMm = 0;
                tbTargetWidth.Value = tbTargetHeight.Value = 0;
                _sourceHasCutline = false;
                _postProcesses.RemoveAll(item => item.ProcessCode == "CUT" && item.Parameters.FromSource);
                RefreshPostProcessGrid();
            }
            finally { _updatingInputs = false; }
            RaiseOrderValuesChanged();
        }

        private void ApplySourceMeasurements(OrderSourcePage source)
        {
            _updatingInputs = true;
            try
            {
                SetOrientedSourceDimensions(source);
                ApplyZoomDimensions();
                // Quantity is edited in the parent order-price footer.
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

        private void ApplyWorkingInputs(BannerOrderEditorState state)
        {
            _updatingInputs = true;
            try
            {
                _sourceRotationDegreesClockwise = state.SourceRotationDegreesClockwise; _sourceFlipHorizontal = state.SourceFlipHorizontal;
                _sourceWidthMm = state.SourceWidthMm; _sourceHeightMm = state.SourceHeightMm; _sourceHasCutline = state.SourceHasCutline;
                cbZoomType.SelectedIndex = state.GetZoomSelectionIndex();
                SetRestoredNumber(tbTargetWidth, state.WidthMm); SetRestoredNumber(tbTargetHeight, state.HeightMm);
                _orderQuantity = Math.Max(0m, state.Quantity);
                SetRestoredNumber(tbEdgeTop, state.EdgeTopMm); SetRestoredNumber(tbEdgeBottom, state.EdgeBottomMm);
                SetRestoredNumber(tbEdgeLeft, state.EdgeLeftMm); SetRestoredNumber(tbEdgeRight, state.EdgeRightMm);
                _postProcesses.Clear(); _postProcesses.AddRange(state.GetPostProcesses());
                _outputServerFile = state.OutputServerFile == null ? null : state.OutputServerFile.Copy();
                RefreshPostProcessGrid();
            }
            finally { _updatingInputs = false; }
        }

        private void ApplyWorkingArtboard(OrderSourcePage metadata)
        {
            BannerOrderEditorState saved;
            bool exists = _artboardStates.TryGetValue(_workingPage, out saved);
            ApplyWorkingInputs(exists ? saved : new BannerOrderEditorState { Quantity = 1, ZoomType = "ORIGINAL",
                PostProcesses = new List<OrderPostProcessSetting>() });
            _sourceDetails = metadata.Details;
            if (exists)
            {
                SetOrientedSourceDimensions(metadata);
                _sourceHasCutline = metadata.HasCutline;
                _updatingInputs = true;
                try { ApplyZoomDimensions(); }
                finally { _updatingInputs = false; }
            }
            else ApplySourceMeasurements(metadata);
            _sourceFileInfoSnapshot = _sourceDocument == null ? null : _sourceDocument.FileInfo;
            SaveWorkingSettings();
            UpdateDocumentState();
        }

        private async void PreviewOutput(object sender, EventArgs e)
        {
            if (_generating || (_sourceDocument != null && _sourceDocument.OutputBusy) || _inspecting || _sourceDetails == null) return;
            string output = null;
            try
            {
                var settings = ReadPdfGenerationSettings();
                settings.Validate();
                string folder = Path.Combine(Path.GetTempPath(), "PineTreePdfPreview", Guid.NewGuid().ToString("N"));
                output = Path.Combine(folder, Path.GetFileNameWithoutExtension(_sourcePath) + "_대지" + (_workingPage + 1) + "_출력용.pdf");
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
            if (_generating || (_sourceDocument != null && _sourceDocument.OutputBusy) || _inspecting || _sourceDetails == null) throw new InvalidOperationException("원본파일을 먼저 선택해주세요.");
            settings.Validate();
            _serverUploadWarning = null;
            _generating = true;
            if (_sourceDocument != null) _sourceDocument.OutputBusy = true;
            btnPreview.Text = "PDF 생성 중…";
            UpdateDocumentState();
            try
            {
                ShowSourceProgress();
                _sourceProgress.ReportProgress("출력에 필요한 원본파일을 준비하는 중입니다…");
                if (_sourceDocument != null && _sourceDocument.UseSavedPreviewsOnly)
                    _sourcePath = await _sourceDocument.EnsureSourceFileAvailableAsync(_documentLifetime.Token);
                if (uploadToServer && !OrderFileStorageService.CanUploadSource(_sourcePath))
                {
                    MessageBox.Show(this, OrderFileStorageService.SourceUploadLimitMessage,
                        "서버 업로드", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    uploadToServer = false;
                }
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
                        if (_sourceDocument != null) _sourceDocument.ServerFile = sourceFile;
                        _outputServerFile = await UploadOrderFileAsync(result.OutputPath);
                        _outputServerFile.RipScale = (decimal)result.OutputRipScale;
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
                if (_sourceDocument != null) _sourceDocument.OutputBusy = false;
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
            _documentLifetime.Cancel();
            if (_sourceDocument != null)
            {
                _sourceDocument.Changed -= SourceDocumentChanged;
                _sourceDocument.PageChanging -= SourcePageChanging;
                if (_ownsSourceDocument) _sourceDocument.Dispose();
            }
            _documentLifetime.Dispose();
        }


    }
}
