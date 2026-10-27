using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using C1.Win.C1InputPanel;
using PineTree.Dialogs;

namespace PineTree.Classes
{
    // Both order dialogs use the same selection state and unlimited row layout.
    internal sealed class OrderProcessRuleEditor
    {
        private readonly Form _owner;
        private readonly C1InputPanel _panel;
        private readonly List<InputTextBox> _texts = new List<InputTextBox>();
        private readonly Dictionary<int, OrderProcessRuleData> _rules = new Dictionary<int, OrderProcessRuleData>();
        private readonly InputButton _addRow = new InputButton { Name = "btnAddProcessRuleRow", Text = "후가공 추가" };

        public OrderProcessRuleEditor(Form owner, C1InputPanel panel, InputTextBox[] texts, InputButton[] select, InputButton[] remove)
        {
            _owner = owner;
            _panel = panel;
            for (int i = 0; i < texts.Length; i++) BindRow(texts[i], select[i], remove[i]);
            _addRow.Click += delegate { AddRow(); };
            _panel.Items.Add(_addRow);
        }

        private void BindRow(InputTextBox text, InputButton select, InputButton remove)
        {
            int slot = _texts.Count + 1;
            _texts.Add(text);
            text.ReadOnly = true;
            select.Click += delegate
            {
                using (var dialog = new DialogProcessRuleList())
                {
                    if (dialog.ShowDialog(_owner) != DialogResult.OK) return;
                    _rules[slot] = new OrderProcessRuleData {
                        RuleID = dialog.SelectedRuleID, SortOrder = slot,
                        ProcessName = dialog.SelectedProcessName, Summary = dialog.SelectedSummary,
                        Steps = dialog.SelectedSteps
                    };
                    text.Text = dialog.SelectedSummary;
                }
            };
            remove.Click += delegate { _rules.Remove(slot); text.Text = string.Empty; };
        }

        private void AddRow()
        {
            int slot = _texts.Count + 1;
            _panel.Items.Remove(_addRow);
            var label = new InputLabel { Name = "lblProcessRule" + slot, Text = "후가공 " + slot, Width = 80,
                HorizontalAlign = InputContentAlignment.Far };
            var text = new InputTextBox { Name = "tbRule" + slot, Width = 412, Height = 42, Multiline = true,
                Break = BreakType.None, Padding = new Padding(0, 3, 0, 0), VerticalAlign = InputContentAlignment.Spread };
            var select = new InputButton { Name = "btnAdd" + slot, Text = "선택", Break = BreakType.None };
            var remove = new InputButton { Name = "btnRemove" + slot, Text = "제거" };
            select.Image = ((InputButton)_panel.Items["btnAdd1"]).Image;
            remove.Image = ((InputButton)_panel.Items["btnRemove1"]).Image;
            _panel.Items.Add(label);
            _panel.Items.Add(text);
            _panel.Items.Add(select);
            _panel.Items.Add(remove);
            BindRow(text, select, remove);
            _panel.Items.Add(_addRow);
        }

        public void LoadRules(List<OrderProcessRuleData> rules)
        {
            _rules.Clear();
            foreach (var text in _texts) text.Text = string.Empty;
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule.SortOrder <= 0) throw new ArgumentException("후가공 순서가 올바르지 않습니다.");
                while (_texts.Count < rule.SortOrder) AddRow();
                _rules.Add(rule.SortOrder, Copy(rule));
                _texts[rule.SortOrder - 1].Text = rule.Summary;
            }
        }

        public List<OrderProcessRuleData> GetRules()
        {
            return _rules.OrderBy(pair => pair.Key).Select(pair => Copy(pair.Value)).ToList();
        }

        private static OrderProcessRuleData Copy(OrderProcessRuleData rule)
        {
            return new OrderProcessRuleData { RuleID = rule.RuleID, SortOrder = rule.SortOrder,
                ProcessName = rule.ProcessName, Summary = rule.Summary,
                Steps = rule.Steps == null ? null : rule.Steps.ConvertAll(step => step.Copy()) };
        }

        public static void BindFilePicker(Form owner, InputButton button, InputTextBox target)
        {
            button.Click += delegate
            {
                using (var dialog = new OpenFileDialog { CheckFileExists = true, FileName = target.Text, Title = "작업요청파일 선택" })
                    if (dialog.ShowDialog(owner) == DialogResult.OK) target.Text = dialog.FileName;
            };
        }
    }
}
