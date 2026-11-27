using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg후가공공통_재단선 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg후가공공통_재단선() : this(OrderPostProcessSetting.Create("CUT")) { }
        public Dlg후가공공통_재단선(OrderPostProcessSetting setting) : this(setting, 0m) { }
        public Dlg후가공공통_재단선(OrderPostProcessSetting setting, decimal defaultInsetMm)
            : this(setting, defaultInsetMm, 1m) { }

        public Dlg후가공공통_재단선(OrderPostProcessSetting setting, decimal defaultInsetMm, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("CUT");
            PostProcessDialogBinding.Select(cbCutType, codes, draft.Parameters.TypeCode);
            var colorModes = new[] { "SPOT", "BLACK" };
            PostProcessDialogBinding.Select(cbSpot, colorModes, draft.Parameters.ColorMode);
            decimal lineWidthDivisor = draft.Parameters.FromSource ? 1m : sourceScaleDivisor;
            PostProcessDialogBinding.SourceNumber(tbLineWidth, draft.Parameters.LineWidthMm, lineWidthDivisor, 10);
            tbLineWidth.ToolTipText = "선택한 원본 축척에서의 재단선 두께(mm).";
            var inset = draft.Parameters.FromSource ? 0m : defaultInsetMm;
            var sides = new[] { tbTop, tbBottom, tbLeft, tbRight };
            var insets = new[] { draft.Parameters.CutInsetTopMm, draft.Parameters.CutInsetBottomMm,
                draft.Parameters.CutInsetLeftMm, draft.Parameters.CutInsetRightMm };
            const decimal minimumInsetMm = -100000m;
            for (int i = 0; i < sides.Length; i++)
            {
                var input = sides[i];
                PostProcessDialogBinding.SourceNumber(input, insets[i] ?? inset, sourceScaleDivisor, minimum: minimumInsetMm);
                input.ToolTipText = "원본 축척 기준 mm. 음수는 이미지 경계에서 입력한 거리만큼 안쪽으로 재단선을 이동합니다.";
                input.Enabled = !draft.Parameters.FromSource;
            }
            PostProcessDialogBinding.SourceNumber(tbAll, 0m, sourceScaleDivisor, minimum: minimumInsetMm);
            tbAll.ToolTipText = "모두 체크 시 입력값을 사방 여백에 적용합니다. 적용 후 각 방향을 따로 수정할 수 있습니다.";
            chkAll.Enabled = tbAll.Enabled = !draft.Parameters.FromSource;
            System.EventHandler applyAll = delegate {
                if (!chkAll.Enabled || !chkAll.Checked || tbAll.ValueIsNull) return;
                foreach (var side in sides) side.Value = tbAll.Value;
            };
            chkAll.CheckedChanged += applyAll;
            tbAll.ValueChanged += applyAll;
            cbCutType.ToolTipText = "입력한 사방 여백에 맞춰 사각 재단선을 생성합니다.";
            tbColorName.ToolTipText = "커팅용 별색 채널 이름. 고객사 샘플: thru";
            tbColorName.Text = draft.Parameters.SpotColorName;
            tbLineWidth.Enabled = cbSpot.Enabled = !draft.Parameters.FromSource;
            tbColorName.Enabled = cbSpot.Enabled && cbSpot.SelectedOption == inputOption1;
            cbSpot.SelectedIndexChanged += delegate {
                tbColorName.Enabled = cbSpot.Enabled && cbSpot.SelectedOption == inputOption1;
            };
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbCutType, codes);
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth) * lineWidthDivisor;
                value.Parameters.CutInsetTopMm = PostProcessDialogBinding.Read(tbTop) * sourceScaleDivisor;
                value.Parameters.CutInsetBottomMm = PostProcessDialogBinding.Read(tbBottom) * sourceScaleDivisor;
                value.Parameters.CutInsetLeftMm = PostProcessDialogBinding.Read(tbLeft) * sourceScaleDivisor;
                value.Parameters.CutInsetRightMm = PostProcessDialogBinding.Read(tbRight) * sourceScaleDivisor;
                value.Parameters.ColorMode = PostProcessDialogBinding.Selected(cbSpot, colorModes);
                value.Parameters.SpotColorName = tbColorName.Text.Trim();
                return value;
            }, value => Setting = value);
        }
    }
}
