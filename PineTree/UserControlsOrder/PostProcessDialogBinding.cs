using System;
using System.Linq;
using System.Windows.Forms;
using C1.Win.C1Input;
using C1.Win.C1InputPanel;
using PineTree.Classes;
using PineTree.PdfEngine;

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
        public static void Number(InputNumericBox input, decimal value, decimal maximum = 100000, int decimalPlaces = 3, decimal minimum = 0)
        {
            input.Minimum = minimum; input.Maximum = Math.Max(maximum, value); input.DecimalPlaces = decimalPlaces;
            input.Format = "#,##0." + new string('#', decimalPlaces); input.Value = value; input.ToolTipText = "단위: mm";
        }
        // Order settings use finished-size millimetres. Dialogs show dimensions at the source scale.
        public static void SourceNumber(InputNumericBox input, decimal finishedMm, decimal sourceScaleDivisor, decimal maximum = 100000, decimal minimum = 0)
        {
            Number(input, finishedMm / sourceScaleDivisor, maximum / sourceScaleDivisor, sourceScaleDivisor == 10m ? 4 : 3, minimum / sourceScaleDivisor);
        }

        public static void UpdateSourceDefaultsOnTypeChange(InputComboBox combo, decimal sourceScaleDivisor,
            Func<decimal> defaultFinishedMm, params InputNumericBox[] inputs)
        {
            decimal previousDefault = defaultFinishedMm() / sourceScaleDivisor;
            combo.SelectedIndexChanged += delegate
            {
                decimal nextDefault = defaultFinishedMm() / sourceScaleDivisor;
                foreach (var input in inputs)
                    if (input.Value is decimal value && value == previousDefault)
                        input.Value = nextDefault;
                previousDefault = nextDefault;
            };
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

        public static Func<HoleMarkRun> BindHoleRun(bool vertical, bool farSide, HoleMarkRun existing, decimal sourceScaleDivisor, InputCheckBox enabled, InputLabel countLabel, InputNumericBox count,
            InputRadioButton uniform, InputRadioButton fixedGap, InputNumericBox[] uniformInputs, InputLabel[] uniformLabels,
            InputNumericBox[] fixedInputs, InputComponent[] fixedAdditionalControls, InputLabel uniformStartY, InputLabel uniformEndY,
            InputLabel uniformEndX, InputLabel fixedStartY, InputLabel uniformStartX, InputLabel fixedStartX)
        {
            uniform.GroupName = fixedGap.GroupName = farSide ? "Far" : "Near";
            enabled.Checked = existing != null;
            bool isFixed = existing?.Mode == "FIXED";
            if (isFixed) fixedGap.Checked = true;
            else uniform.Checked = true;
            Number(count, existing != null && !isFixed ? existing.Count : 4, 2000m);
            count.DecimalPlaces = 0;
            count.Format = "#,##0";
            count.ToolTipText = "타공점 개수";
            foreach (var input in uniformInputs.Concat(fixedInputs).Concat(fixedAdditionalControls.OfType<InputNumericBox>()))
                SourceNumber(input, 20m, sourceScaleDivisor);
            SourceNumber(fixedInputs[2], 0m, sourceScaleDivisor);

            bool startFromRight = existing?.StartRightMm.HasValue ?? (vertical && farSide);
            bool startFromBottom = existing?.StartBottomMm.HasValue ?? (!vertical && farSide);
            bool endFromBottom = existing != null && !isFixed ? existing.EndBottomMm.HasValue : vertical || farSide;
            bool endFromRight = existing != null && !isFixed ? existing.EndRightMm.HasValue : !vertical || farSide;
            if (existing != null)
            {
                var inputs = isFixed ? fixedInputs : uniformInputs;
                SourceNumber(inputs[0], (decimal)(existing.StartRightMm ?? existing.StartLeftMm), sourceScaleDivisor);
                SourceNumber(inputs[1], (decimal)(existing.StartBottomMm ?? existing.StartTopMm), sourceScaleDivisor);
                SourceNumber(inputs[2], (decimal)(isFixed ? existing.SpacingMm : existing.EndRightMm ?? existing.EndLeftMm), sourceScaleDivisor);
                if (!isFixed) SourceNumber(inputs[3], (decimal)(existing.EndBottomMm ?? existing.EndTopMm), sourceScaleDivisor);
            }

            uniformStartX.Text = fixedStartX.Text = startFromRight ? "우(Right)" : "좌(Left)";
            uniformStartY.Text = fixedStartY.Text = startFromBottom ? "하(Bottom)" : "상(Top)";
            uniformEndY.Text = endFromBottom ? "하(Bottom)" : "상(Top)";
            uniformEndX.Text = endFromRight ? "우(Right)" : "좌(Left)";
            uniformInputs[0].ToolTipText = fixedInputs[0].ToolTipText = HorizontalPositionToolTip(startFromRight);
            uniformInputs[2].ToolTipText = HorizontalPositionToolTip(endFromRight);
            uniformInputs[1].ToolTipText = fixedInputs[1].ToolTipText = VerticalPositionToolTip(startFromBottom);
            uniformInputs[3].ToolTipText = VerticalPositionToolTip(endFromBottom);
            fixedInputs[2].ToolTipText = vertical
                ? "상·하단에 같은 여백을 적용합니다. 그 사이 길이를 입력 간격으로 나눈 몫만큼 균등 분할합니다."
                : "좌·우에 같은 여백을 적용합니다. 그 사이 길이를 입력 간격으로 나눈 몫만큼 균등 분할합니다.";

            var uniformControls = uniformInputs.Concat<InputComponent>(uniformLabels).Concat(new[] { uniformEndY, uniformEndX }).ToArray();
            var fixedControls = fixedInputs.Concat(fixedAdditionalControls).ToArray();
            Action refresh = () =>
            {
                bool runEnabled = enabled.Checked;
                uniform.Enabled = fixedGap.Enabled = runEnabled;
                countLabel.Enabled = count.Enabled = runEnabled && uniform.Checked;
                foreach (var control in uniformControls) control.Enabled = runEnabled && uniform.Checked;
                foreach (var control in fixedControls) control.Enabled = runEnabled && fixedGap.Checked;
            };
            enabled.CheckedChanged += delegate { refresh(); };
            uniform.CheckedChanged += delegate { refresh(); };
            fixedGap.CheckedChanged += delegate { refresh(); };
            refresh();

            return () =>
            {
                if (!enabled.Checked) return null;
                bool uniformSpacing = uniform.Checked;
                decimal number = uniformSpacing ? Read(count) : 0;
                if (uniformSpacing && (number < 1 || number > 2000 || number != decimal.Truncate(number)))
                    throw new ArgumentException("타공 개수는 1~2,000 사이의 정수로 입력해주세요.");

                var inputs = uniformSpacing ? uniformInputs : fixedInputs;
                var run = new HoleMarkRun
                {
                    Count = (int)number,
                    Mode = uniformSpacing ? "UNIFORM" : "FIXED"
                };
                double startX = (double)(Read(inputs[0]) * sourceScaleDivisor);
                if (startFromRight) run.StartRightMm = startX;
                else run.StartLeftMm = startX;
                double startY = (double)(Read(inputs[1]) * sourceScaleDivisor);
                if (startFromBottom) run.StartBottomMm = startY;
                else run.StartTopMm = startY;
                if (uniformSpacing)
                {
                    double endX = (double)(Read(inputs[2]) * sourceScaleDivisor);
                    if (endFromRight) run.EndRightMm = endX;
                    else run.EndLeftMm = endX;
                    double endY = (double)(Read(inputs[3]) * sourceScaleDivisor);
                    if (endFromBottom) run.EndBottomMm = endY;
                    else run.EndTopMm = endY;
                }
                else run.SpacingMm = (double)(Read(inputs[2]) * sourceScaleDivisor);
                return run;
            };
        }

        private static string VerticalPositionToolTip(bool fromBottom)
        {
            return fromBottom ? "이미지 아래쪽 끝에서 위로 잰 거리(mm)" : "이미지 위쪽 끝에서 아래로 잰 거리(mm)";
        }

        private static string HorizontalPositionToolTip(bool fromRight)
        {
            return fromRight ? "이미지 오른쪽 끝에서 안쪽으로 잰 거리(mm)" : "이미지 왼쪽 끝에서 안쪽으로 잰 거리(mm)";
        }
    }
}
