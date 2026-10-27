using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogProcessRuleList : Form
    {
        private const string AllText = "[전체]";
        private bool _binding;
        private long _previewRuleID;
        private List<ProcessRuleData> _previewSteps;

        public long SelectedRuleID { get; private set; }
        public string SelectedProcessName { get; private set; }
        public string SelectedSummary { get; private set; }
        public List<ProcessRuleData> SelectedSteps { get; private set; }

        public DialogProcessRuleList()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MinimumSize = Size;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            c1TextBox1.ReadOnly = true;
            c1TextBox1.TextDetached = true;
            c1TextBox1.ScrollBars = ScrollBars.Vertical;
            c1TextBox1.Text = string.Empty;
            btnOK.Enabled = false;
            btnOK.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            c1Button1.Click += delegate { LoadRules(); };
            btnOK.Click += delegate { SelectCurrentRule(); };
            FGrid1.AfterRowColChange += delegate { if (!_binding) UpdatePreview(); };
            FGrid1.AfterSort += delegate { if (!_binding) UpdatePreview(); };
            FGrid1.DoubleClick += delegate { SelectCurrentRule(); };
            FGrid1.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                SelectCurrentRule();
            };
            tbItemName.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                LoadRules();
            };
            Load += delegate
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode) return;
                try
                {
                    cbCategory.Items.Clear();
                    cbCategory.Items.Add(AllText);
                    foreach (DataRow row in ProcessRuleRepository.GetRuleCategories().Rows)
                        cbCategory.Items.Add(Convert.ToString(row["Category"]));
                    cbCategory.SelectedIndex = 0;
                    LoadRules();
                }
                catch (Exception ex) { ShowLoadError(ex); }
            };
        }

        private void LoadRules()
        {
            ClearPreview();
            _binding = true;
            try
            {
                string category = cbCategory.Text.Trim();
                FGrid1.DataSource = ProcessRuleRepository.GetRuleList(category == AllText ? string.Empty : category, tbItemName.Text);
                FGrid1.Cols.Fixed = 0;
                FGrid1.BackColor = Color.MintCream;
                FGrid1.AllowSorting = AllowSortingEnum.SingleColumn;
                FGrid1.SelectionMode = SelectionModeEnum.Row;
                FGrid1.Cols["RuleID"].Visible = false;
                SetColumn("Category", "카테고리", 85);
                SetColumn("ProcessName", "후가공 규칙명", 140);
                SetColumn("WorkName", "작업명", 120);
                SetColumn("EditDescription", "편집방법 / 기본값 설명", 220);
                FGrid1.Rows[0].TextAlign = TextAlignEnum.CenterCenter;
                if (FGrid1.Rows.Count > FGrid1.Rows.Fixed)
                    FGrid1.Row = FGrid1.Rows.Fixed;
            }
            catch (Exception ex)
            {
                FGrid1.DataSource = null;
                ShowLoadError(ex);
            }
            finally { _binding = false; }
            UpdatePreview();
        }

        private void SetColumn(string name, string caption, int width)
        {
            FGrid1.Cols[name].Caption = caption;
            FGrid1.Cols[name].Width = width;
            FGrid1.Cols[name].TextAlign = TextAlignEnum.LeftCenter;
            FGrid1.Cols[name].AllowEditing = false;
        }

        private long CurrentRuleID()
        {
            if (FGrid1.Row < FGrid1.Rows.Fixed || FGrid1.Row >= FGrid1.Rows.Count || !FGrid1.Cols.Contains("RuleID")) return 0;
            long id;
            return long.TryParse(Convert.ToString(FGrid1[FGrid1.Row, "RuleID"]), out id) ? id : 0;
        }

        private void ClearPreview()
        {
            _previewRuleID = 0;
            _previewSteps = null;
            c1TextBox1.Text = string.Empty;
            btnOK.Enabled = false;
        }

        private void UpdatePreview()
        {
            long id = CurrentRuleID();
            if (id > 0 && id == _previewRuleID) return;
            ClearPreview();
            if (id <= 0) return;
            try
            {
                var steps = ProcessRuleRepository.GetRuleSteps(id);
                if (steps.Count == 0)
                {
                    c1TextBox1.Text = "등록된 활성 후가공 명령이 없습니다.";
                    return;
                }
                var summaries = new List<string>();
                foreach (var step in steps) summaries.Add(step.BuildSummary());
                c1TextBox1.Text = string.Join(Environment.NewLine + Environment.NewLine, summaries);
                _previewSteps = steps;
                _previewRuleID = id;
                btnOK.Enabled = true;
            }
            catch (Exception ex)
            {
                c1TextBox1.Text = "후가공 내용을 읽을 수 없습니다.\r\n" + ex.Message;
            }
        }

        private void SelectCurrentRule()
        {
            UpdatePreview();
            if (_previewSteps == null || _previewRuleID != CurrentRuleID()) return;
            SelectedRuleID = _previewRuleID;
            SelectedProcessName = _previewSteps[0].ProcessName;
            SelectedSummary = c1TextBox1.Text;
            SelectedSteps = _previewSteps.ConvertAll(step => step.Copy());
            DialogResult = DialogResult.OK;
        }

        private void ShowLoadError(Exception ex)
        {
            MessageBox.Show("후가공 규칙 목록을 조회하지 못했습니다.\r\n" + ex.Message,
                "후가공 규칙 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
