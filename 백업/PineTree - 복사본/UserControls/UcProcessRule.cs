using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcProcessRule : UserControl
    {
        private DataTable _priceItems;
        private long? _selectedPriceItemSeqNo;
        private bool _isLoading;
        private bool _isBinding;
        private readonly Dictionary<long, List<ProcessRuleData>> _rulesByItem = new Dictionary<long, List<ProcessRuleData>>();
        private long? _rulesForItemSeqNo;
        private int _ruleRequestVersion;
        private UcProcessRuleSummary _activeRuleEditor;

        public UcProcessRule()
        {
            InitializeComponent();

            priceItemGrid.Columns.Add(new PagedGridColumn("No", "번호", 50, ContentAlignment.MiddleCenter));
            priceItemGrid.Columns.Add(new PagedGridColumn("PriceCategory", "카테고리", 85, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("CategoryName", "품목분류", 100, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("PriceItemName", "품목명", 0, ContentAlignment.MiddleLeft));
            priceItemGrid.Columns.Add(new PagedGridColumn("HasProcessRule", "후가공", 65, ContentAlignment.MiddleCenter));
            UpdatePageSize();
            priceItemGrid.TotalText = "전체 0건";
            priceItemGrid.PageChanged += delegate { BindPriceItemPage(); };
            priceItemGrid.CellClicked += delegate(object sender, PagedGridCellClickEventArgs e)
            {
                priceItemGrid.SelectedRowIndex = e.RowIndex;
            };
            priceItemGrid.SelectionChanged += delegate { UpdateSelectedItem(); };
            priceItemGrid.Resize += delegate
            {
                UpdatePageSize();
                priceItemGrid.PageIndex = priceItemGrid.PageIndex;
                BindPriceItemPage();
            };
            btnSelect.Click += async delegate { await LoadPriceItemsAsync(); };
            btnAddRule.Click += BtnAddRule_Click;
            btnDeleteRule.Click += delegate { _activeRuleEditor?.DeleteRule(); };
            ruleList.Resize += delegate { ResizeRuleEditors(); };
            Load += UcProcessRule_Load;
            UpdateSelectedItem();
        }

        private async void UcProcessRule_Load(object sender, EventArgs e)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
            {
                return;
            }

            await LoadPriceItemsAsync();
        }

        private void UpdatePageSize()
        {
            priceItemGrid.PageSize = Math.Max(1, (priceItemGrid.Height - 36 - 52) / 41);
        }

        private async Task LoadPriceItemsAsync()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;
            btnSelect.Enabled = false;
            try
            {
                DataTable items = await Task.Run(() => PriceItemRepository.GetBasicPriceItems());
                if (IsDisposed)
                {
                    return;
                }

                _priceItems = items;
                _rulesByItem.Clear();
                _rulesForItemSeqNo = null;
                ++_ruleRequestVersion;
                _selectedPriceItemSeqNo = null;
                priceItemGrid.TotalCount = items.Rows.Count;
                priceItemGrid.PageIndex = 1;
                priceItemGrid.TotalText = string.Format("전체 {0:#,##0}건", items.Rows.Count);
                BindPriceItemPage();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    MessageBox.Show("품목 목록을 불러오지 못했습니다.\r\n" + ex.Message,
                        "후가공 기본 규칙", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                _isLoading = false;
                if (!IsDisposed)
                {
                    btnSelect.Enabled = true;
                }
            }
        }

        private void BindPriceItemPage()
        {
            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            int selectedIndex = -1;
            if (_priceItems != null)
            {
                int startIndex = (priceItemGrid.PageIndex - 1) * priceItemGrid.PageSize;
                int endIndex = Math.Min(startIndex + priceItemGrid.PageSize, _priceItems.Rows.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    DataRow item = _priceItems.Rows[index];
                    if (_selectedPriceItemSeqNo == Convert.ToInt64(item["SeqNo"]))
                    {
                        selectedIndex = rows.Count;
                    }
                    Dictionary<string, PagedGridCell> row = new Dictionary<string, PagedGridCell>
                    {
                        { "SeqNo", new PagedGridCell(Convert.ToString(item["SeqNo"])) },
                        { "No", new PagedGridCell((index + 1).ToString()) }
                    };
                    foreach (PagedGridColumn column in priceItemGrid.Columns)
                    {
                        if (column.Key == "HasProcessRule")
                        {
                            bool hasProcessRule = item["HasProcessRule"] != DBNull.Value && Convert.ToBoolean(item["HasProcessRule"]);
                            row.Add(column.Key, new PagedGridCell(hasProcessRule ? "등록" : "미등록")
                            {
                                CellType = PagedGridCellType.Image,
                                Image = hasProcessRule ? Properties.Resources.Check_Work : Properties.Resources.Check_None
                            });
                        }
                        else if (column.Key != "No")
                        {
                            row.Add(column.Key, new PagedGridCell(Convert.ToString(item[column.Key])));
                        }
                    }
                    rows.Add(row);
                }
            }

            _isBinding = true;
            try
            {
                priceItemGrid.SetRows(rows);
                priceItemGrid.SelectedRowIndex = selectedIndex >= 0 ? selectedIndex : rows.Count > 0 ? 0 : -1;
            }
            finally
            {
                _isBinding = false;
            }
            UpdateSelectedItem();
        }

        private void UpdateSelectedItem()
        {
            if (_isBinding)
            {
                return;
            }

            int index = (priceItemGrid.PageIndex - 1) * priceItemGrid.PageSize + priceItemGrid.SelectedRowIndex;
            if (_priceItems == null || priceItemGrid.SelectedRowIndex < 0 || index < 0 || index >= _priceItems.Rows.Count)
            {
                _selectedPriceItemSeqNo = null;
                lblSelectedItem.Text = "선택된 품목이 없습니다.";
                lblSelectedCategory.Text = "좌측 목록에서 품목을 선택하세요.";
                lblSelectedItemCode.Text = string.Empty;
                ruleEditorPanel.Enabled = false;
                ShowRulesForItem(null);
                return;
            }

            DataRow item = _priceItems.Rows[index];
            _selectedPriceItemSeqNo = Convert.ToInt64(item["SeqNo"]);
            lblSelectedItem.Text = Convert.ToString(item["PriceItemName"]);
            lblSelectedCategory.Text = string.Format("카테고리: {0}   /   품목분류: {1}", item["PriceCategory"], item["CategoryName"]);
            lblSelectedItemCode.Text = "품목코드: " + Convert.ToString(item["ItemCode"]);
            ruleEditorPanel.Enabled = true;
            ShowRulesForItem(_selectedPriceItemSeqNo);
        }

        private async void ShowRulesForItem(long? itemSeqNo)
        {
            if (_rulesForItemSeqNo == itemSeqNo && itemSeqNo.HasValue)
                return;
            _rulesForItemSeqNo = itemSeqNo;
            int requestVersion = ++_ruleRequestVersion;
            ClearRuleEditors();
            btnAddRule.Enabled = false;
            if (!itemSeqNo.HasValue)
            {
                lblRuleHint.Text = "좌측 목록에서 품목을 선택하세요.";
                return;
            }

            List<ProcessRuleData> rules;
            if (_rulesByItem.TryGetValue(itemSeqNo.Value, out rules))
            {
                RenderRules(itemSeqNo.Value, rules);
                return;
            }
            lblRuleHint.Text = "후가공 규칙을 불러오는 중입니다.";
            try
            {
                rules = await Task.Run(() => ProcessRuleRepository.GetRules(itemSeqNo.Value));
                if (IsDisposed || requestVersion != _ruleRequestVersion)
                    return;
                _rulesByItem[itemSeqNo.Value] = rules;
                RenderRules(itemSeqNo.Value, rules);
            }
            catch (Exception ex)
            {
                if (IsDisposed || requestVersion != _ruleRequestVersion)
                    return;
                _rulesForItemSeqNo = null;
                lblRuleHint.Text = "규칙을 불러오지 못했습니다. 품목을 다시 선택해주세요.";
                MessageBox.Show(this, "후가공 규칙을 불러오지 못했습니다.\r\n" + ex.Message,
                    "후가공 규칙", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnAddRule_Click(object sender, EventArgs e)
        {
            if (!_selectedPriceItemSeqNo.HasValue) return;
            long itemSeqNo = _selectedPriceItemSeqNo.Value;
            List<ProcessRuleData> rules;
            if (!_rulesByItem.TryGetValue(itemSeqNo, out rules)) return;
            using (DialogProcessRule dialog = new DialogProcessRule(itemSeqNo, null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Rule == null) return;
            }
            foreach (DataRow item in _priceItems.Rows)
                if (Convert.ToInt64(item["SeqNo"]) == itemSeqNo) item["HasProcessRule"] = 1;
            _rulesByItem.Remove(itemSeqNo);
            _rulesForItemSeqNo = null;
            BindPriceItemPage();
        }

        private void ClearRuleEditors()
        {
            _activeRuleEditor = null;
            btnDeleteRule.Enabled = false;
            while (ruleList.Controls.Count > 0)
            {
                Control editor = ruleList.Controls[0];
                ruleList.Controls.Remove(editor);
                editor.Dispose();
            }
        }

        private void RenderRules(long itemSeqNo, List<ProcessRuleData> rules)
        {
            ruleList.SuspendLayout();
            ClearRuleEditors();
            foreach (ProcessRuleData rule in rules)
            {
                UcProcessRuleSummary editor = new UcProcessRuleSummary();
                editor.SetRule(itemSeqNo, rule);
                editor.Margin = new Padding(0, 0, 0, 12);
                editor.Enter += delegate { SetActiveRuleEditor(editor); };
                TrackRuleEditorClicks(editor, editor);
                editor.Cancelled += delegate
                {
                    rules.Remove(rule);
                    RenderRules(itemSeqNo, rules);
                };
                editor.Deleted += delegate
                {
                    rules.Remove(rule);
                    bool hasRegisteredRules = rules.Exists(remaining => remaining.MappingSeqNo > 0);
                    foreach (DataRow item in _priceItems.Rows)
                    {
                        if (Convert.ToInt64(item["SeqNo"]) == itemSeqNo)
                        {
                            item["HasProcessRule"] = hasRegisteredRules ? 1 : 0;
                            break;
                        }
                    }
                    BindPriceItemPage();
                    RenderRules(itemSeqNo, rules);
                };
                editor.SavingChanged += delegate
                {
                    priceItemGrid.Enabled = !editor.IsSaving;
                    btnSelect.Enabled = !editor.IsSaving;
                    btnAddRule.Enabled = !editor.IsSaving;
                    btnDeleteRule.Enabled = !editor.IsSaving && _activeRuleEditor != null;
                    ruleList.Enabled = !editor.IsSaving;
                };
                editor.Saved += delegate
                {
                    foreach (DataRow item in _priceItems.Rows)
                    {
                        if (Convert.ToInt64(item["SeqNo"]) == itemSeqNo)
                        {
                            item["HasProcessRule"] = 1;
                            break;
                        }
                    }
                    BindPriceItemPage();
                    UpdateRuleHint(rules);
                };
                ruleList.Controls.Add(editor);
            }
            ResizeRuleEditors();
            UpdateRuleHint(rules);
            btnAddRule.Enabled = true;
            if (ruleList.Controls.Count > 0)
                SetActiveRuleEditor((UcProcessRuleSummary)ruleList.Controls[0]);
            ruleList.ResumeLayout();
        }

        private void SetActiveRuleEditor(UcProcessRuleSummary editor)
        {
            _activeRuleEditor = editor;
            btnDeleteRule.Enabled = editor != null && !editor.IsSaving;
            foreach (UcProcessRuleSummary child in ruleList.Controls)
                child.BorderStyle = child == editor ? BorderStyle.FixedSingle : BorderStyle.None;
        }

        private void TrackRuleEditorClicks(Control control, UcProcessRuleSummary editor)
        {
            control.MouseDown += delegate { SetActiveRuleEditor(editor); };
            foreach (Control child in control.Controls)
                TrackRuleEditorClicks(child, editor);
        }

        private void UpdateRuleHint(List<ProcessRuleData> rules)
        {
            int registeredCount = rules.FindAll(rule => rule.MappingSeqNo > 0).Count;
            lblRuleHint.Text = registeredCount == 0 ? "등록된 후가공 규칙이 없습니다"
                : string.Format("등록된 후가공 규칙 {0}개", registeredCount);
            if (rules.Count > registeredCount)
                lblRuleHint.Text += " · 적용 전 규칙은 적용 버튼을 눌러 저장하세요.";
        }

        private void ResizeRuleEditors()
        {
            int width = Math.Max(300, ruleList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
            foreach (Control editor in ruleList.Controls)
                editor.Width = width;
        }
    }
}
