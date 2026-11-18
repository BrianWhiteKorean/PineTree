using PineTree.PdfEngine;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1InputPanel;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogOrderItem : Form
    {
        private readonly string _custCode = string.Empty;
        private readonly string _custName = string.Empty;

        public long SelectedPriceItemSeqNo { get; private set; }

        public long? SelectedPriceItemPriceId { get; private set; }

        public string SelectedItemCode { get; private set; }

        public string SelectedPriceItemName { get; private set; }

        public string SelectedSpecText { get; private set; }

        public string SelectedBillingBasis { get; private set; }

        public string SelectedUnitName { get; private set; }

        public string SelectedPriceName { get; private set; }

        public decimal SelectedUnitPrice { get; private set; }

        public DialogPriceItemOrderItem SelectedOrderItem { get; private set; }
        private OrderProcessRuleEditor _processRuleEditor;
        private readonly CancellationTokenSource _documentLifetime = new CancellationTokenSource();
        private CancellationTokenSource _sourceInspection;
        private OrderDocumentResult _sourceDetails;
        private int _sourceVersion;
        private bool _generating;
        private InputNumericBox tbHoleSpacing;
        private InputLabel _holeSpacingLabel;
        private InputLabel _pdfSummary;

        private readonly string _initialItemName;
        private string _selectedCustCode;
        private string _selectedPriceCategory;
        private string _selectedCategoryName;
        private string _selectedApplyFrom;

        public DialogOrderItem() : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public DialogOrderItem(string priceItemName, string custCode, string custName)
        {
            InitializeComponent();
            _custCode = string.IsNullOrWhiteSpace(custCode) ? string.Empty : custCode.Trim();
            _custName = string.IsNullOrWhiteSpace(custName) ? string.Empty : custName.Trim();
            _initialItemName = priceItemName ?? string.Empty;
            Text = "주문 품목 입력";
            c1DockingTab1.VisualStyle = C1.Win.C1Command.VisualStyle.Office2010Black;
            btnSelectProduct.Click += btnSelectProduct_Click;
            btnSelectProductExclusive.Click += btnSelectProductExclusive_Click;
            btnSelectProductExclusive.Enabled = !string.IsNullOrWhiteSpace(_custCode);
            btnOK.Click += delegate { ConfirmSelectedOrderItem(); };
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            _processRuleEditor = new OrderProcessRuleEditor(this, c1InputPanel1,
                new[] { tbRule1, tbRule2, tbRule3 }, new[] { btnAdd1, btnAdd2, btnAdd3 }, new[] { btnRemove1, btnRemove2, btnRemove3 });
            InitializeDocuments();
            tbWidth.ValueChanged += OrderInput_ValueChanged;
            tbHeight.ValueChanged += OrderInput_ValueChanged;
            tbQty.ValueChanged += OrderInput_ValueChanged;
            tbPrice.ValueChanged += OrderPrice_ValueChanged;
            chkCustItem.Enabled = !string.IsNullOrWhiteSpace(_custCode);
            tbCustomName.Text = _initialItemName;
            tbCustName.Text = _custName;
        }

        private void InitializeDocuments()
        {
            tbSourceFile.ReadOnly = true;
            inputTextBox2.ReadOnly = true;
            tbFileType.ReadOnly = true;
            tbFileInfo.ReadOnly = true;
            InitializePdfInputs();
            btnGenerate.ToolTipText = "주문파일 탭에 입력한 조건으로 첫 페이지의 출력용 PDF를 생성합니다.";
            btnSelectFile.Click += SelectSourceFile;
            tbSourceFile.TextChanged += delegate { InspectSourceFile(); };
            btnPreview1.Click += delegate { ShowDocumentPreview(tbSourceFile.Text); };
            btnPreview2.Click += delegate { ShowDocumentPreview(inputTextBox2.Text, true); };
            btnGenerate.Click += GeneratePdf;
            FormClosing += delegate
            {
                ++_sourceVersion;
                _documentLifetime.Cancel();
                if (_sourceInspection != null) _sourceInspection.Cancel();
            };
            UpdateDocumentButtons();
        }

        private void InitializePdfInputs()
        {
            cbDeploy.DropDownStyle = cbSpot.DropDownStyle = cbHoleType.DropDownStyle = InputComboBoxStyle.DropDownList;
            cbDeploy.SelectedOption = inputOption1;
            cbSpot.SelectedOption = inputOption3;
            cbHoleType.SelectedOption = inputOption4;
            tbHoleCount.ReadOnly = true;
            tbHoleCount.Maximum = 2000;
            tbHoleCount.DecimalPlaces = 0;
            tbHoleCount.ToolTipText = "사방타공은 4개, 간격별 타공은 최대 간격 이내로 균등 배치한 개수를 자동 계산합니다.";
            tbHoleSpacing = new InputNumericBox { Name = "tbHoleSpacing", Width = 80 };
            _holeSpacingLabel = new InputLabel { Name = "lblHoleSpacing", Text = "최대 간격 (mm)",
                Padding = new Padding(80, 3, 0, 0) };
            int index = c1InputPanel1.Items.IndexOf(inputSeparator4);
            c1InputPanel1.Items.Insert(index++, _holeSpacingLabel);
            c1InputPanel1.Items.Insert(index, tbHoleSpacing);
            _pdfSummary = new InputLabel { Name = "lblPdfGenerationSummary", Width = 690,
                Break = BreakType.Row, Padding = new Padding(30, 5, 0, 5) };
            c1InputPanel1.Items.Insert(c1InputPanel1.Items.IndexOf(inputSeparator1), _pdfSummary);
            var values = new[] { tbTargetWidth, tbTargetHeight, tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight,
                tbLineWidth, tbMarkLeftRight, tbMarkTopBottom, tbHoleSpacing };
            decimal[] defaults = { 600m, 1800m, 2m, 2m, 2m, 2m, 0.18m, 20m, 20m, 500m };
            for (int i = 0; i < values.Length; i++)
            {
                values[i].Minimum = 0;
                values[i].Maximum = 100000;
                //values[i].DecimalPlaces = 3;
                //values[i].Format = "0.###";
                values[i].Value = defaults[i];
                values[i].ToolTipText = "단위: mm / 빈값 기본값: " + defaults[i];
                values[i].ValueChanged += PdfInputChanged;
            }
            tbColorName.Text = "thru";
            tbColorName.ToolTipText = "별색 채널 이름. 기본 thru / 대체색 C100 M0 Y0 K0";
            tbMarkLeftRight.ToolTipText = "완성 영역의 좌우 경계에서 마크 중심까지 안쪽 거리 (mm), 기본 20";
            tbMarkTopBottom.ToolTipText = "완성 영역의 상하 경계에서 마크 중심까지 안쪽 거리 (mm), 기본 20";
            tbTargetWidth.ToolTipText = "가장자리 여유를 제외한 완성 가로 (mm), 기본 600";
            tbTargetHeight.ToolTipText = "가장자리 여유를 제외한 완성 세로 (mm), 기본 1800";
            foreach (var input in new[] { tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight })
                input.ToolTipText += " / 완성 영역 바깥으로 배경 가장자리를 연장합니다.";
            chkMakeCutline.CheckedChanged += PdfInputChanged;
            chkMakeHole.CheckedChanged += PdfInputChanged;
            cbDeploy.SelectedIndexChanged += PdfInputChanged;
            cbHoleType.SelectedIndexChanged += PdfInputChanged;
            tbColorName.TextChanged += PdfInputChanged;
            UpdatePdfInputState();
        }

        private static double PdfNumber(InputNumericBox input, double fallback)
        {
            return input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text) ? fallback : Convert.ToDouble(input.Value);
        }

        private PdfGenerationSettings ReadPdfGenerationSettings()
        {
            return new PdfGenerationSettings
            {
                TargetWidthMm = PdfNumber(tbTargetWidth, 600), TargetHeightMm = PdfNumber(tbTargetHeight, 1800),
                FitMode = cbDeploy.SelectedOption == inputOption2 ? "COVER" : "CONTAIN",
                EdgeTopMm = PdfNumber(tbEdgeTop, 2), EdgeBottomMm = PdfNumber(tbEdgeBottom, 2),
                EdgeLeftMm = PdfNumber(tbEdgeLeft, 2), EdgeRightMm = PdfNumber(tbEdgeRight, 2),
                MakeCutline = chkMakeCutline.Checked, LineWidthMm = PdfNumber(tbLineWidth, 0.18),
                SpotColorName = string.IsNullOrWhiteSpace(tbColorName.Text) ? "thru" : tbColorName.Text.Trim(),
                MakeHole = chkMakeHole.Checked,
                HoleType = cbHoleType.SelectedOption == inputOption5 ? "CUSTOM_SPACING" : "FOUR_CORNERS",
                MarkLeftRightMm = PdfNumber(tbMarkLeftRight, 20), MarkTopBottomMm = PdfNumber(tbMarkTopBottom, 20),
                HoleSpacingMm = PdfNumber(tbHoleSpacing, 500)
            };
        }

        private void PdfInputChanged(object sender, EventArgs e)
        {
            // A generated file remains on disk; its displayed path must match the current settings.
            inputTextBox2.Text = string.Empty;
            UpdatePdfInputState();
            UpdateDocumentButtons();
        }

        private void UpdatePdfInputState()
        {
            var settings = ReadPdfGenerationSettings();
            bool cut = !_generating && settings.MakeCutline;
            tbLineWidth.Enabled = cbSpot.Enabled = tbColorName.Enabled = cut;
            bool marks = !_generating && settings.MakeHole;
            cbHoleType.Enabled = tbMarkLeftRight.Enabled = tbMarkTopBottom.Enabled = marks;
            bool spacing = settings.HoleType == "CUSTOM_SPACING";
            _holeSpacingLabel.Visibility = tbHoleSpacing.Visibility = spacing ? Visibility.Visible : Visibility.Collapsed;
            tbHoleSpacing.Enabled = marks && spacing;
            try
            {
                settings.Validate();
                tbHoleCount.Value = settings.GetMarkCount();
                _pdfSummary.Text = string.Format("단위 mm · 출력 {0:0.###} × {1:0.###} · 타공마크 {2}개 · 원본 첫 페이지",
                    settings.TargetWidthMm + settings.EdgeLeftMm + settings.EdgeRightMm,
                    settings.TargetHeightMm + settings.EdgeTopMm + settings.EdgeBottomMm, settings.GetMarkCount());
            }
            catch (ArgumentException ex) { tbHoleCount.Value = 0; _pdfSummary.Text = ex.Message; }
        }

        private void SelectSourceFile(object sender, EventArgs e)
        {
            if (_generating) return;
            using (var dialog = new OpenFileDialog
            {
                Title = "작업요청파일 선택", Filter = OrderDocumentService.InputFilter,
                CheckFileExists = true, Multiselect = false,
                FileName = File.Exists(tbSourceFile.Text) ? tbSourceFile.Text : string.Empty
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!string.Equals(tbSourceFile.Text, dialog.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    inputTextBox2.Text = string.Empty;
                    tbSourceFile.Text = dialog.FileName;
                }
                else InspectSourceFile();
            }
        }

        private async void InspectSourceFile()
        {
            inputTextBox2.Text = string.Empty;
            int version = ++_sourceVersion;
            if (_sourceInspection != null) _sourceInspection.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
            _sourceInspection = cancellation;
            _sourceDetails = null;
            tbFileType.Text = string.Empty;
            tbFileInfo.Text = string.Empty;
            UpdateDocumentButtons();
            string source = tbSourceFile.Text;
            try
            {
                if (string.IsNullOrWhiteSpace(source)) return;
                tbFileType.Text = Path.GetExtension(source).TrimStart('.').ToUpperInvariant() + " : 분석 중";
                tbFileInfo.Text = "파일 속성을 읽는 중입니다…";
                var result = await OrderDocumentService.InspectAsync(source, cancellation.Token);
                if (IsDisposed || version != _sourceVersion) return;
                _sourceDetails = result;
                tbFileType.Text = result.FileType;
                tbFileInfo.Text = result.FileInfo;
                tbFileInfo.ToolTipText = result.FileInfo;
                tbFileType.ToolTipText = result.FileType;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed && version == _sourceVersion)
                {
                    tbFileType.Text = Path.GetExtension(source).TrimStart('.').ToUpperInvariant() + " : 확인 필요";
                    tbFileInfo.Text = ex.Message;
                    tbFileInfo.ToolTipText = ex.Message;
                }
            }
            finally
            {
                cancellation.Dispose();
                if (_sourceInspection == cancellation) _sourceInspection = null;
                if (!IsDisposed && version == _sourceVersion) UpdateDocumentButtons();
            }
        }

        private void ShowDocumentPreview(string path, bool outputPreview = false)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "미리보기할 파일을 찾을 수 없습니다.", "파일 미리보기", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var preview = new DialogDocumentPreview(path, outputPreview);
            preview.Show(this);
        }

        private async void GeneratePdf(object sender, EventArgs e)
        {
            if (_generating || _sourceDetails == null) return;
            string source = tbSourceFile.Text;
            PdfGenerationSettings settings;
            try { settings = ReadPdfGenerationSettings(); settings.Validate(); }
            catch (ArgumentException ex)
            {
                MessageBox.Show(this, ex.Message, "PDF 생성 조건", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string output;
            using (var dialog = new SaveFileDialog
            {
                Title = "입력 조건으로 출력용 PDF 저장", Filter = "PDF 문서|*.pdf",
                DefaultExt = "pdf", AddExtension = true, OverwritePrompt = true,
                InitialDirectory = Path.GetDirectoryName(source),
                FileName = Path.GetFileNameWithoutExtension(source) + "_출력용.pdf"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                output = dialog.FileName;
            }
            try
            {
                var result = await GeneratePdfToPathAsync(output, settings);
                if (IsDisposed) return;
                MessageBox.Show(this, "출력용 PDF를 생성했습니다.\r\n" + result.Recipe
                    + "\r\n" + result.OutputPath,
                    "PDF 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed) MessageBox.Show(this, "출력용 PDF를 생성하지 못했습니다.\r\n" + ex.Message,
                    "PDF 생성", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task<OrderDocumentResult> GeneratePdfToPathAsync(string output, PdfGenerationSettings settings)
        {
            if (_generating) throw new InvalidOperationException("PDF 생성 중입니다.");
            settings.Validate();
            _generating = true;
            btnGenerate.Text = "생성 중…";
            UpdateDocumentButtons();
            try
            {
                var result = await OrderDocumentService.GenerateAsync(tbSourceFile.Text, output, settings, _documentLifetime.Token);
                if (!IsDisposed) inputTextBox2.Text = result.OutputPath;
                return result;
            }
            finally
            {
                _generating = false;
                if (!IsDisposed)
                {
                    btnGenerate.Text = "생성";
                    UpdateDocumentButtons();
                }
            }
        }

        private void UpdateDocumentButtons()
        {
            btnSelectFile.Enabled = !_generating;
            btnGenerate.Enabled = !_generating && _sourceDetails != null;
            btnPreview1.Enabled = !_generating && _sourceDetails != null;
            btnPreview2.Enabled = !_generating && File.Exists(inputTextBox2.Text);
            btnOK.Enabled = !_generating;
            foreach (var input in new[] { tbTargetWidth, tbTargetHeight, tbEdgeTop, tbEdgeBottom, tbEdgeLeft, tbEdgeRight })
                input.Enabled = !_generating;
            cbDeploy.Enabled = chkMakeCutline.Enabled = chkMakeHole.Enabled = !_generating;
            UpdatePdfInputState();
        }

        private void btnSelectProduct_Click(object sender, EventArgs e)
        {
            string searchName = SelectedPriceItemSeqNo > 0 ? SelectedPriceItemName : _initialItemName;
            using (DialogPriceItemList dialog = new DialogPriceItemList(searchName, _custCode, _custName))
            {
                PositionPriceItemListDialog(dialog, btnSelectProduct);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplySelectedPriceItem(dialog);
            }
        }

        private void btnSelectProductExclusive_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_custCode))
            {
                MessageBox.Show("거래처를 먼저 선택해주세요.", "전용품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string searchName = SelectedPriceItemSeqNo > 0 ? SelectedPriceItemName : _initialItemName;
            using (DialogPriceItemListExclusive dialog = new DialogPriceItemListExclusive(searchName, _custCode, _custName))
            {
                PositionPriceItemListDialog(dialog, btnSelectProductExclusive);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplySelectedExclusivePriceItem(dialog);
            }
        }

        private void PositionPriceItemListDialog(Form dialog, C1.Win.C1InputPanel.InputButton button)
        {
            Rectangle buttonBounds = c1InputPanel2.GetItemBounds(button);
            Point buttonBottomLeft = c1InputPanel2.PointToScreen(new Point(buttonBounds.Left, buttonBounds.Bottom));
            Rectangle workingArea = Screen.FromPoint(buttonBottomLeft).WorkingArea;
            int x = Math.Max(workingArea.Left, Math.Min(buttonBottomLeft.X, workingArea.Right - dialog.Width));
            int y = Math.Max(workingArea.Top, Math.Min(buttonBottomLeft.Y + 4, workingArea.Bottom - dialog.Height));

            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(x, y);
        }

        private void ApplySelectedPriceItem(DialogPriceItemList dialog)
        {
            SelectedPriceItemSeqNo = dialog.SelectedPriceItemSeqNo;
            SelectedPriceItemPriceId = dialog.SelectedPriceItemPriceId;
            SelectedItemCode = dialog.SelectedItemCode;
            SelectedPriceItemName = dialog.SelectedPriceItemName;
            SelectedSpecText = dialog.SelectedSpecText;
            SelectedBillingBasis = dialog.SelectedBillingBasis;
            SelectedUnitName = dialog.SelectedUnitName;
            SelectedPriceName = dialog.SelectedPriceName;
            SelectedUnitPrice = dialog.SelectedUnitPrice;
            _selectedCustCode = dialog.SelectedCustCode;
            _selectedPriceCategory = dialog.SelectedPriceCategory;
            _selectedCategoryName = dialog.SelectedCategoryName;
            _selectedApplyFrom = dialog.SelectedApplyFrom;

            ApplySelectedPriceItemToInputs();
        }

        private void ApplySelectedExclusivePriceItem(DialogPriceItemListExclusive dialog)
        {
            SelectedPriceItemSeqNo = dialog.SelectedPriceItemSeqNo;
            SelectedPriceItemPriceId = dialog.SelectedPriceItemPriceId;
            SelectedItemCode = dialog.SelectedItemCode;
            SelectedPriceItemName = dialog.SelectedPriceItemName;
            SelectedSpecText = dialog.SelectedSpecText;
            SelectedBillingBasis = dialog.SelectedBillingBasis;
            SelectedUnitName = dialog.SelectedUnitName;
            SelectedPriceName = dialog.SelectedPriceName;
            SelectedUnitPrice = dialog.SelectedUnitPrice;
            _selectedCustCode = dialog.SelectedCustCode;
            _selectedPriceCategory = dialog.SelectedPriceCategory;
            _selectedCategoryName = dialog.SelectedCategoryName;
            _selectedApplyFrom = dialog.SelectedApplyFrom;

            ApplySelectedPriceItemToInputs();
        }

        private void ApplySelectedPriceItemToInputs()
        {
            tbItemName.Text = SelectedPriceItemName;
            if (string.IsNullOrWhiteSpace(tbCustomName.Text))
            {
                tbCustomName.Text = SelectedPriceItemName;
            }
            tbUnitName.Text = SelectedUnitName;
            tbSpec.Text = SelectedSpecText;
            tbPrice.Value = SelectedUnitPrice;
            UpdateOrderCalculations();
        }

        private void ConfirmSelectedOrderItem()
        {
            try
            {
                if (SelectedPriceItemSeqNo <= 0)
                {
                    MessageBox.Show("품목 선택 버튼을 눌러 품목을 선택해주세요.", "주문 품목 입력", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateOrderCalculations();
                SaveDedicatedPriceItemIfNeeded();

                SelectedOrderItem = new DialogPriceItemOrderItem
                {
                    PriceItemSeqNo = SelectedPriceItemSeqNo,
                    PriceItemPriceId = SelectedPriceItemPriceId,
                    ItemCode = SelectedItemCode,
                    PriceItemName = tbItemName.Text.Trim(),
                    CustomName = tbCustomName.Text.Trim(),
                    SpecText = tbSpec.Text,
                    BillingBasis = SelectedBillingBasis,
                    UnitName = tbUnitName.Text.Trim(),
                    PriceName = SelectedPriceName,
                    WidthMm = GetNullableDecimal(tbWidth.Value),
                    HeightMm = GetNullableDecimal(tbHeight.Value),
                    Qty = GetQtyForCalculation(),
                    AreaQty = string.Equals(tbUnitName.Text.Trim(), "회배", StringComparison.Ordinal) ? GetBillingQtyForAmount() : (decimal?)null,
                    UnitPrice = GetDecimal(tbPrice.Value),
                    SupplyAmount = GetDecimal(tbNetPrice.Value),
                    VatAmount = GetDecimal(tbVat.Value),
                    TotalAmount = GetDecimal(tbSum.Value),
                    Remark = tbRemark.Text,
                    RequestFilePath = tbSourceFile.Text,
                    OutputFilePath = inputTextBox2.Text,
                    ProcessRules = _processRuleEditor.GetRules()
                };

                SelectedPriceItemName = SelectedOrderItem.PriceItemName;
                SelectedSpecText = SelectedOrderItem.SpecText;
                SelectedUnitName = SelectedOrderItem.UnitName;
                SelectedUnitPrice = SelectedOrderItem.UnitPrice;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("품목 정보를 확정하지 못했습니다.\r\n" + ex.Message, "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveDedicatedPriceItemIfNeeded()
        {
            if (!chkCustItem.Checked)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_custCode))
            {
                MessageBox.Show("거래처가 지정되지 않아 거래처 전용품목으로 저장할 수 없습니다.", "품목 선택", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.Equals(_selectedCustCode, _custCode, StringComparison.Ordinal))
            {
                return;
            }

            DateTime applyFrom;
            if (!DateTime.TryParse(_selectedApplyFrom, out applyFrom))
            {
                applyFrom = DateTime.Today;
            }

            PriceItemRepository.DedicatedPriceItemSaveResult result = PriceItemRepository.InsertDedicatedPriceItem(
                _custCode,
                _selectedPriceCategory,
                _selectedCategoryName,
                tbItemName.Text.Trim(),
                SelectedItemCode,
                tbSpec.Text,
                tbUnitName.Text,
                SelectedBillingBasis,
                GetNullableDecimal(tbWidth.Value),
                GetNullableDecimal(tbHeight.Value),
                SelectedPriceName,
                GetDecimal(tbPrice.Value),
                applyFrom,
                tbRemark.Text);

            SelectedPriceItemSeqNo = result.PriceItemSeqNo;
            SelectedPriceItemPriceId = result.PriceItemPriceId;
            SelectedItemCode = result.ItemCode;
        }

        private void OrderInput_ValueChanged(object sender, EventArgs e)
        {
            UpdateOrderCalculations();
        }

        private void OrderPrice_ValueChanged(object sender, EventArgs e)
        {
            UpdateAmounts();
        }

        private void UpdateOrderCalculations()
        {
            UpdateAreaAndSpec();
            UpdateAmounts();
        }

        private void UpdateAreaAndSpec()
        {
            if (!string.Equals(tbUnitName.Text.Trim(), "회배", StringComparison.Ordinal))
            {
                tbArea.Value = 0M;
                return;
            }

            decimal width = GetDecimal(tbWidth.Value);
            decimal height = GetDecimal(tbHeight.Value);
            decimal qty = GetQtyForCalculation();
            decimal area = IsWidthOrHeightEmpty() ? qty : width * height * qty / 1000000M;
            tbArea.Value = area;

            if (width > 0M && height > 0M)
            {
                tbSpec.Text = string.Format("{0}*{1}_{2}장", FormatDimension(width), FormatDimension(height), FormatQty(qty));
            }
        }

        private decimal GetQtyForCalculation()
        {
            decimal qty = GetDecimal(tbQty.Value);
            return qty <= 0M ? 1M : qty;
        }

        private string FormatDimension(decimal value)
        {
            return value.ToString("0.####");
        }

        private string FormatQty(decimal value)
        {
            return value.ToString("0.####");
        }

        private void UpdateAmounts()
        {
            decimal unitPrice = GetDecimal(tbPrice.Value);
            decimal billingQty = GetBillingQtyForAmount();
            decimal supplyAmount = Math.Round(unitPrice * billingQty, 0, MidpointRounding.AwayFromZero);
            decimal vat = Math.Round(supplyAmount * 0.1M, 0, MidpointRounding.AwayFromZero);
            decimal totalAmount = supplyAmount + vat;

            tbNetPrice.Value = supplyAmount;
            tbVat.Value = vat;
            tbSum.Value = totalAmount;
        }

        private decimal GetBillingQtyForAmount()
        {
            if (string.Equals(tbUnitName.Text.Trim(), "회배", StringComparison.Ordinal))
            {
                if (IsWidthOrHeightEmpty())
                {
                    return GetQtyForCalculation();
                }

                decimal width = GetDecimal(tbWidth.Value);
                decimal height = GetDecimal(tbHeight.Value);
                return width * height * GetQtyForCalculation() / 1000000M;
            }

            return GetQtyForCalculation();
        }

        private bool IsWidthOrHeightEmpty()
        {
            return IsInputNumericEmpty(tbWidth) || IsInputNumericEmpty(tbHeight);
        }

        private bool IsInputNumericEmpty(C1.Win.C1InputPanel.InputNumericBox input)
        {
            return string.IsNullOrWhiteSpace(input.Text);
        }

        private decimal GetDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0M;
            }

            decimal result;
            return decimal.TryParse(value.ToString(), out result) ? result : 0M;
        }

        private decimal? GetNullableDecimal(object value)
        {
            decimal result = GetDecimal(value);
            return result > 0M ? (decimal?)result : null;
        }

        private void DialogOrderItem_Load(object sender, EventArgs e)
        {
            c1DockingTab1.VisualStyle = C1.Win.C1Command.VisualStyle.Office2010Black;
        }
    }

    public class DialogPriceItemOrderItem
    {
        public long PriceItemSeqNo { get; set; }
        public long? PriceItemPriceId { get; set; }
        public string ItemCode { get; set; }
        public string PriceItemName { get; set; }
        public string CustomName { get; set; }
        public string SpecText { get; set; }
        public string BillingBasis { get; set; }
        public string UnitName { get; set; }
        public string PriceName { get; set; }
        public decimal? WidthMm { get; set; }
        public decimal? HeightMm { get; set; }
        public decimal Qty { get; set; }
        public decimal? AreaQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SupplyAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Remark { get; set; }
        public string RequestFilePath { get; set; }
        public string OutputFilePath { get; set; }
        public System.Collections.Generic.List<OrderProcessRuleData> ProcessRules { get; set; }
    }
}
