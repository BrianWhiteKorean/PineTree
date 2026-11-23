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
            PostProcessDialogBinding.SourceNumber(tbTop, draft.Parameters.CutInsetTopMm ?? inset, sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbBottom, draft.Parameters.CutInsetBottomMm ?? inset, sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbLeft, draft.Parameters.CutInsetLeftMm ?? inset, sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbRight, draft.Parameters.CutInsetRightMm ?? inset, sourceScaleDivisor);
            foreach (var input in new[] { tbTop, tbBottom, tbLeft, tbRight }) {
                input.ToolTipText = "선택한 원본 축척에서 페이지 가장자리부터 재단선까지의 거리(mm).";
                input.Enabled = !draft.Parameters.FromSource;
            }
            cbCutType.ToolTipText = "입력한 사방 여백에 맞춰 사각 재단선을 생성합니다. 이미지와 페이지 크기는 유지됩니다.";
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
