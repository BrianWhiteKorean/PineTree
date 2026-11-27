using System;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcProcessRuleSummary : UserControl
    {
        private long _priceItemSeqNo;
        private ProcessRuleData _rule;
        private ProcessRuleData _original;
        private bool _isSaving;
        public event EventHandler Saved;
        public event EventHandler Cancelled;
        public event EventHandler Deleted;
        public event EventHandler SavingChanged;
        public bool IsSaving { get { return _isSaving; } }

        public UcProcessRuleSummary()
        {
            InitializeComponent();
            tbDescription.ReadOnly = true;
            tbDescription.MaxLength = 0;
            tbDescription.ScrollBars = ScrollBars.Vertical;
            btnApply.Click += BtnApply_Click;
            btnCancel.Click += BtnCancel_Click;
            tbDescription.DoubleClick += delegate { EditParameters(); };
            var editTip = new ToolTip();
            editTip.SetToolTip(tbDescription, "두 번 클릭하면 후가공 규칙과 파라미터를 수정합니다.");
            Disposed += delegate { editTip.Dispose(); };
        }

        public void SetRule(long priceItemSeqNo, ProcessRuleData rule)
        {
            _priceItemSeqNo = priceItemSeqNo;
            _rule = null;
            if (rule.MappingSeqNo > 0 && rule.SavedVersion == null)
                rule.SavedVersion = rule.Copy();
            _original = rule.SavedVersion ?? rule.Copy();
            tbDescription.Text = BuildSummary(rule);
            _rule = rule;
        }

        private static string BuildSummary(ProcessRuleData rule)
        {
            var summary = new StringBuilder();
            string actionName = ActionName(rule.ActionCode);
            summary.Append(string.IsNullOrWhiteSpace(rule.ProcessName) ? "규칙명 미입력" : rule.ProcessName.Trim());
            summary.Append(" · ").Append(string.IsNullOrWhiteSpace(actionName) ? "명령 미선택" : actionName);
            try
            {
                var p = ProcessRuleParameters.FromJson(rule.ActionCode, rule.ParamsJSON);
                if (rule.ActionCode == "ADD_PUNCH_MARK")
                {
                    string mode = p.PunchMode == "FOUR_SIDES" ? "사방타공"
                        : p.PunchMode == "CUSTOM_SPACING" ? "요청 간격별 타공" : "타공 방식 미선택";
                    summary.AppendLine().Append(mode).Append(" · 개수 ").Append(Quantity(p.PunchCount, "개"))
                        .Append(" · 직경 ").Append(Quantity(p.DiameterMm, "mm"));
                    if (p.PunchMode == "CUSTOM_SPACING")
                        summary.Append(" · 간격 ").Append(Quantity(p.SpacingMm, "mm"));
                }
                else if (ProcessRuleParameters.UsesHorizontalMargins(rule.ActionCode) || ProcessRuleParameters.UsesVerticalMargins(rule.ActionCode))
                {
                    summary.AppendLine();
                    if (ProcessRuleParameters.UsesVerticalMargins(rule.ActionCode))
                        summary.Append("상 ").Append(Quantity(p.TopMm, "mm"))
                            .Append(" / 하 ").Append(Quantity(p.BottomMm, "mm"));
                    if (ProcessRuleParameters.UsesHorizontalMargins(rule.ActionCode))
                    {
                        if (ProcessRuleParameters.UsesVerticalMargins(rule.ActionCode)) summary.Append(" / ");
                        summary.Append("좌 ").Append(Quantity(p.LeftMm, "mm"))
                            .Append(" / 우 ").Append(Quantity(p.RightMm, "mm"));
                    }
                }
            }
            catch (ArgumentException)
            {
                summary.AppendLine().Append("파라미터 확인 필요 (규칙 편집창에서 확인하세요)");
            }
            if (!string.IsNullOrWhiteSpace(rule.WorkName) && rule.WorkName.Trim() != actionName)
                summary.AppendLine().Append("작업: ").Append(rule.WorkName.Trim());
            if (!string.IsNullOrWhiteSpace(rule.EditDescription))
                summary.AppendLine().Append("설명: ").Append(rule.EditDescription.Trim());
            return summary.ToString();
        }

        private static string Quantity(decimal? value, string unit)
        {
            return value.HasValue ? value.Value.ToString("0.############################", CultureInfo.CurrentCulture) + unit : "미입력";
        }

        private async void BtnApply_Click(object sender, EventArgs e)
        {
            if (_rule == null || _isSaving) return;
            try
            {
                ProcessRuleData draft = _rule.Copy();
                draft.SavedVersion = _rule.SavedVersion;
                draft.Validate();
                _isSaving = true;
                Enabled = false;
                SavingChanged?.Invoke(this, EventArgs.Empty);
                ProcessRuleData saved = await Task.Run(() => ProcessRuleRepository.Save(_priceItemSeqNo, draft));
                _rule.MappingSeqNo = saved.MappingSeqNo;
                _rule.RuleID = saved.RuleID;
                _rule.ProcessName = saved.ProcessName;
                _rule.Category = saved.Category;
                _rule.RuleStepID = saved.RuleStepID;
                _rule.ParamsJSON = saved.ParamsJSON;
                _rule.RuleVersion = saved.RuleVersion;
                _rule.UpdatedAt = saved.UpdatedAt;
                _original = saved.Copy();
                _rule.SavedVersion = saved.Copy();
                if (!IsDisposed)
                {
                    SetRule(_priceItemSeqNo, _rule);
                    Saved?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show(this, "규칙을 저장하지 못했습니다.\r\n" + ex.Message, "후가공 규칙", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isSaving = false;
                if (!IsDisposed)
                {
                    Enabled = true;
                    SavingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void DeleteRule()
        {
            BtnDelete_Click(this, EventArgs.Empty);
        }

        private void EditParameters()
        {
            if (_rule == null || _isSaving || _rule.RuleID == 0) return;
            using (var dialog = new DialogProcessRule(_priceItemSeqNo, _rule.RuleID, _rule.MappingSeqNo))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Rule == null) return;
                var saved = dialog.Rule;
                _rule.RuleID = saved.RuleID;
                _rule.RuleStepID = saved.RuleStepID;
                _rule.ProcessName = saved.ProcessName;
                _rule.Category = saved.Category;
                _rule.WorkName = saved.WorkName;
                _rule.EditDescription = saved.EditDescription;
                _rule.ActionCode = saved.ActionCode;
                _rule.ParamsJSON = saved.ParamsJSON;
                _rule.RuleVersion = saved.RuleVersion;
                _rule.UpdatedAt = saved.UpdatedAt;
                _rule.SavedVersion = saved.Copy();
                SetRule(_priceItemSeqNo, _rule);
                Saved?.Invoke(this, EventArgs.Empty);
            }
        }

        private static string ActionName(string action)
        {
            switch (action)
            {
                case "ADD_MARGIN": return "여백 / 이미지 확장";
                case "ADD_CUT_LINE": return "재단선 생성";
                case "ADD_GUIDE_LINE": return "미싱 가이드선 생성";
                case "ADD_PUNCH_MARK": return "타공마크 생성";
                case "ADD_WOOD_BAR_LR": return "좌우각목";
                case "ADD_WOOD_BAR_TB": return "상하각목";
                case "ADD_POLE_HEM_LR": return "좌우봉미싱";
                case "ADD_POLE_HEM_TB": return "상하봉미싱";
                default: return action ?? string.Empty;
            }
        }

        private async void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_rule == null || _isSaving) return;
            if (_rule.MappingSeqNo == 0)
            {
                Deleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            bool deleted = false;
            try
            {
                long itemSeqNo = _priceItemSeqNo;
                long mappingSeqNo = _rule.MappingSeqNo;
                long ruleID = _rule.RuleID;
                _isSaving = true;
                Enabled = false;
                SavingChanged?.Invoke(this, EventArgs.Empty);
                await Task.Run(() => ProcessRuleRepository.Delete(itemSeqNo, mappingSeqNo, ruleID));
                deleted = true;
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show(this, "규칙을 삭제하지 못했습니다.\r\n" + ex.Message,
                        "후가공 규칙", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isSaving = false;
                if (!IsDisposed)
                {
                    Enabled = true;
                    SavingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            if (deleted && !IsDisposed)
                Deleted?.Invoke(this, EventArgs.Empty);
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (_rule == null || _isSaving) return;
            if (_rule.MappingSeqNo == 0)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
                return;
            }
            _rule.ProcessName = _original.ProcessName;
            _rule.Category = _original.Category;
            _rule.EditDescription = _original.EditDescription;
            SetRule(_priceItemSeqNo, _rule);
        }
    }
}
