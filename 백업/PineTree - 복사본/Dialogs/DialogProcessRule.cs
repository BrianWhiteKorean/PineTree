using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1InputPanel;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogProcessRule : Form
    {
        private readonly Dictionary<InputOption, string> _actionCodes;
        private readonly long? _priceItemSeqNo;
        private readonly long? _ruleID;
        private readonly long _mappingSeqNo;
        private ProcessRuleData _loaded;
        private bool _saving;
        private const string CategoryPlaceholder = "[선택하세요]";
        public ProcessRuleData Rule { get; private set; }
        public string InitialProcessName { get; set; }
        public string InitialEditDescription { get; set; }

        public DialogProcessRule() : this(null, null, 0) { }
        public DialogProcessRule(long ruleID) : this(null, ruleID, 0) { }
        public DialogProcessRule(long? priceItemSeqNo, long? ruleID, long mappingSeqNo = 0)
        {
            _priceItemSeqNo = priceItemSeqNo;
            _ruleID = ruleID;
            _mappingSeqNo = mappingSeqNo;
            InitializeComponent();
            _actionCodes = new Dictionary<InputOption, string>
            {
                { inputOption1, "ADD_MARGIN" },
                { inputOption2, "ADD_CUT_LINE" },
                { inputOption3, "ADD_PUNCH_MARK" },
                { inputOption4, "ADD_GUIDE_LINE" },
                { inputOption8, "ADD_WOOD_BAR_LR" },
                { inputOption9, "ADD_WOOD_BAR_TB" },
                { inputOption10, "ADD_POLE_HEM_LR" },
                { inputOption11, "ADD_POLE_HEM_TB" }
            };
            cbWorkName.SelectedOption = inputOption7;
            cbCategory.DropDownStyle = InputComboBoxStyle.DropDownList;
            BindCategories(new DataTable(), null);
            btnOK.Enabled = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            cbWorkName.DropDownStyle = InputComboBoxStyle.DropDownList;
            cbHoleType.DropDownStyle = InputComboBoxStyle.DropDownList;
            //foreach (var input in new[] { tbHoleCnt, tbHoleSize, tbHoleGap, tbMarginTop, tbMarginBottom, tbMarginLeft, inputNumericBox6 })
            //{
            //    input.Minimum = 0;
            //    input.Maximum = int.MaxValue;
            //    input.DecimalPlaces = input == tbHoleCnt ? 0 : 3;
            //    input.Format = input == tbHoleCnt ? "0" : "0.###";
            //    input.Increment = input == tbHoleCnt ? 1 : 0.1m;
            //    input.ValueIsNull = true;
            //}
            btnOK.Click += BtnOK_Click;
            cbWorkName.SelectedIndexChanged += delegate { UpdateParameterControlsEnabled(); };
            cbHoleType.SelectedIndexChanged += delegate { UpdateParameterControlsEnabled(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (_saving) e.Cancel = true; };
            Load += LoadRule;
            UpdateParameterControlsEnabled();
        }

        private async void LoadRule(object sender, EventArgs e)
        {
            btnOK.Enabled = false;
            c1InputPanel1.Enabled = false;
            try
            {
                DataTable categories = await Task.Run(() => CommonCodeRepository.GetItems("규칙카테고리"));
                if (IsDisposed) return;
                BindCategories(categories, null);
                if (!_ruleID.HasValue)
                {
                    c1InputPanel1.Enabled = true;
                    btnOK.Enabled = true;
                    return;
                }
                _loaded = await Task.Run(() => ProcessRuleRepository.GetRule(_ruleID.Value));
                if (IsDisposed) return;
                BindCategories(categories, _loaded.Category);
                _loaded.MappingSeqNo = _mappingSeqNo;
                if (_loaded.LinkedItemCount > 1)
                    Text += _priceItemSeqNo.HasValue
                        ? string.Format(" · {0}개 상품 공유 (선택 상품용으로 복제 저장)", _loaded.LinkedItemCount)
                        : string.Format(" · {0}개 상품 공유 (상품 화면에서 수정 필요)", _loaded.LinkedItemCount);
                tbProcessName.Text = InitialProcessName ?? _loaded.ProcessName;
                tbEditDescription.Text = InitialEditDescription ?? _loaded.EditDescription;
                if (!SelectActionCode(_loaded.ActionCode) && _loaded.RuleStepID != 0)
                    throw new ArgumentException("지원하지 않는 PDF 편집명령입니다. 기존 데이터는 보존됩니다.");
                var p = ProcessRuleParameters.FromJson(_loaded.ActionCode, _loaded.ParamsJSON);
                cbHoleType.SelectedIndex = p.PunchMode == "FOUR_SIDES" ? 0 : p.PunchMode == "CUSTOM_SPACING" ? 1 : -1;
                SetNumber(tbHoleCnt, p.PunchCount);
                SetNumber(tbHoleSize, p.DiameterMm);
                SetNumber(tbHoleGap, p.SpacingMm);
                SetNumber(tbMarginTop, p.TopMm);
                SetNumber(tbMarginBottom, p.BottomMm);
                SetNumber(tbMarginLeft, p.LeftMm);
                SetNumber(inputNumericBox6, p.RightMm);
                UpdateParameterControlsEnabled();
                c1InputPanel1.Enabled = true;
                btnOK.Enabled = true;
            }
            catch (Exception ex)
            {
                if (!IsDisposed) MessageBox.Show(this, "규칙을 열지 못했습니다.\r\n" + ex.Message, "후가공 규칙", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BindCategories(DataTable categories, string selectedCategory)
        {
            cbCategory.Items.Clear();
            var placeholder = new InputOption { Text = CategoryPlaceholder };
            cbCategory.Items.Add(placeholder);
            cbCategory.SelectedOption = placeholder;
            var added = new HashSet<string>(StringComparer.Ordinal);
            foreach (DataRow row in categories.Rows)
            {
                string category = Convert.ToString(row["ItemName"]).Trim();
                if (string.IsNullOrEmpty(category) || category == CategoryPlaceholder || !added.Add(category)) continue;
                var option = new InputOption { Text = category };
                cbCategory.Items.Add(option);
                if (category == (selectedCategory ?? string.Empty).Trim()) cbCategory.SelectedOption = option;
            }
        }

        private string GetSelectedCategory()
        {
            return cbCategory.SelectedIndex > 0 && cbCategory.SelectedOption != null
                ? cbCategory.SelectedOption.Text.Trim() : null;
        }

        private static void SetNumber(InputNumericBox input, decimal? value)
        {
            if (value.HasValue)
            {
                // Preserve existing precision and range rather than silently clamping values on load.
                input.Maximum = Math.Max(input.Maximum, value.Value);
                input.Minimum = Math.Min(input.Minimum, value.Value);
                input.DecimalPlaces = Math.Max(input.DecimalPlaces, (decimal.GetBits(value.Value)[3] >> 16) & 255);
                input.Format = input.DecimalPlaces == 0 ? "0" : "0." + new string('#', input.DecimalPlaces);
                input.Value = value.Value;
            }
            input.ValueIsNull = !value.HasValue;
        }

        private static decimal? Number(InputNumericBox input)
        {
            if (input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text)) return null;
            decimal value;
            if (!decimal.TryParse(input.Text, out value)) throw new ArgumentException("숫자 입력값을 확인해주세요.");
            return value;
        }

        private string GetSelectedActionCode()
        {
            string action;
            return cbWorkName.SelectedOption != null && _actionCodes.TryGetValue(cbWorkName.SelectedOption, out action)
                ? action : null;
        }

        private bool SelectActionCode(string action)
        {
            foreach (var option in _actionCodes)
            {
                if (option.Value != action) continue;
                cbWorkName.SelectedOption = option.Key;
                return true;
            }
            cbWorkName.SelectedOption = inputOption7;
            return false;
        }

        private void UpdateParameterControlsEnabled()
        {
            string action = GetSelectedActionCode();
            bool holeEnabled = action == "ADD_PUNCH_MARK";
            bool horizontalEnabled = ProcessRuleParameters.UsesHorizontalMargins(action);
            bool verticalEnabled = ProcessRuleParameters.UsesVerticalMargins(action);
            bool spacingEnabled = holeEnabled && cbHoleType.SelectedIndex == 1;
            inputLabel4.Enabled = cbHoleType.Enabled = holeEnabled;
            inputLabelHole1.Enabled = tbHoleCnt.Enabled = holeEnabled;
            inputLabelHole2.Enabled = tbHoleSize.Enabled = holeEnabled;
            inputLabelHole3.Enabled = tbHoleGap.Enabled = spacingEnabled;
            inputLabelMargin1.Enabled = horizontalEnabled || verticalEnabled;
            inputLabelMargin2.Enabled = tbMarginTop.Enabled = verticalEnabled;
            inputLabelMargin3.Enabled = tbMarginBottom.Enabled = verticalEnabled;
            inputLabelMargin4.Enabled = tbMarginLeft.Enabled = horizontalEnabled;
            inputLabelMargin5.Enabled = inputNumericBox6.Enabled = horizontalEnabled;
        }

        private async void BtnOK_Click(object sender, EventArgs e)
        {
            if (_saving) return;
            try
            {
                var draft = _loaded == null ? new ProcessRuleData() : _loaded.Copy();
                draft.SavedVersion = _loaded == null ? null : _loaded.SavedVersion;
                draft.ProcessName = tbProcessName.Text.Trim();
                draft.Category = GetSelectedCategory();
                // WorkName is independent business text on existing rules.
                draft.WorkName = _loaded == null ? cbWorkName.Text : _loaded.WorkName;
                draft.EditDescription = tbEditDescription.Text;
                draft.ActionCode = GetSelectedActionCode();
                if (string.IsNullOrWhiteSpace(draft.ProcessName)) throw new ArgumentException("후가공 규칙명을 입력해주세요.");
                if (string.IsNullOrWhiteSpace(draft.Category)) throw new ArgumentException("카테고리를 선택해주세요.");
                if (draft.ActionCode == null) throw new ArgumentException("PDF 편집명령을 선택해주세요.");
                var p = new ProcessRuleParameters();
                if (draft.ActionCode == "ADD_PUNCH_MARK")
                {
                    p.PunchMode = cbHoleType.SelectedIndex == 0 ? "FOUR_SIDES" : cbHoleType.SelectedIndex == 1 ? "CUSTOM_SPACING" : null;
                    p.PunchCount = Number(tbHoleCnt);
                    p.DiameterMm = Number(tbHoleSize);
                    p.SpacingMm = cbHoleType.SelectedIndex == 1 ? Number(tbHoleGap) : null;
                }
                else
                {
                    if (ProcessRuleParameters.UsesVerticalMargins(draft.ActionCode))
                    {
                        p.TopMm = Number(tbMarginTop);
                        p.BottomMm = Number(tbMarginBottom);
                    }
                    if (ProcessRuleParameters.UsesHorizontalMargins(draft.ActionCode))
                    {
                        p.LeftMm = Number(tbMarginLeft);
                        p.RightMm = Number(inputNumericBox6);
                    }
                }
                draft.ParamsJSON = p.ToJson(draft.ActionCode);
                draft.Validate();
                _saving = true;
                btnOK.Enabled = btnCancel.Enabled = c1InputPanel1.Enabled = false;
                Rule = await Task.Run(() => _priceItemSeqNo.HasValue
                    ? ProcessRuleRepository.Save(_priceItemSeqNo.Value, draft) : ProcessRuleRepository.SaveRule(draft));
                _saving = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "규칙을 저장하지 못했습니다.\r\n" + ex.Message, "후가공 규칙", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _saving = false;
                if (!IsDisposed) btnOK.Enabled = btnCancel.Enabled = c1InputPanel1.Enabled = true;
            }
        }
    }
}
