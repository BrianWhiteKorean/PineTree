using System;
using System.Windows.Forms;
using C1.Win.C1Input;
using C1.Win.C1InputPanel;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    internal interface IPostProcessDialog { OrderPostProcessSetting Setting { get; } }
    internal static class PostProcessDialogBinding
    {
        public static void Bind(Form form, C1Button ok, C1Button cancel, Func<OrderPostProcessSetting> read, Action<OrderPostProcessSetting> accept)
        {
            form.AcceptButton = ok; form.CancelButton = cancel;
            cancel.DialogResult = DialogResult.Cancel;
            ok.Click += delegate {
                try { var setting = read(); setting.Validate(); accept(setting); form.DialogResult = DialogResult.OK; form.Close(); }
                catch(ArgumentException ex) { MessageBox.Show(form, ex.Message, "후가공 설정", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
        }
        public static void Number(InputNumericBox input, decimal value, decimal maximum = 100000)
        {
            input.Minimum = 0; input.Maximum = Math.Max(maximum, value); input.DecimalPlaces = 3;
            input.Format = "#,##0.###"; input.Value = value; input.ToolTipText = "단위: mm";
        }
        public static decimal Read(InputNumericBox input)
        {
            if(input == null || input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text)) throw new ArgumentException("필요한 후가공 치수와 간격을 입력해주세요.");
            return Convert.ToDecimal(input.Value);
        }
        public static void Select(InputComboBox combo, string[] codes, string value)
        {
            combo.DropDownStyle = InputComboBoxStyle.DropDownList;
            combo.SelectedIndex = Array.IndexOf(codes, value);
        }
        public static string Selected(InputComboBox combo, string[] codes)
        {
            if(combo.SelectedIndex < 0 || combo.SelectedIndex >= codes.Length) throw new ArgumentException("후가공 작업종류를 선택해주세요.");
            return codes[combo.SelectedIndex];
        }
    }
}
