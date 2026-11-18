using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_재단선 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_재단선() : this(OrderPostProcessSetting.Create("CUT")) { }
        public Dlg현수막_재단선(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("CUT");
            PostProcessDialogBinding.Select(cbCutType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Select(cbSpot, new[] { "SPOT" }, draft.Parameters.ColorMode);
            PostProcessDialogBinding.Number(tbLineWidth, draft.Parameters.LineWidthMm, 10);
            tbLineWidth.ToolTipText = "최종 출력 기준 mm. 확대비율과 관계없이 입력한 선두께를 적용합니다.";
            PostProcessDialogBinding.Number(tbTop, draft.Parameters.CutInsetTopMm ?? 0m);
            PostProcessDialogBinding.Number(tbBottom, draft.Parameters.CutInsetBottomMm ?? 0m);
            PostProcessDialogBinding.Number(tbLeft, draft.Parameters.CutInsetLeftMm ?? 0m);
            PostProcessDialogBinding.Number(tbRight, draft.Parameters.CutInsetRightMm ?? 0m);
            foreach (var input in new[] { tbTop, tbBottom, tbLeft, tbRight }) {
                input.ToolTipText = "최종 출력 기준 mm. 출력 페이지 가장자리에서 안쪽으로 재단선까지의 거리입니다. 확대비율과 관계없이 입력값을 적용합니다.";
                input.Enabled = !draft.Parameters.FromSource;
            }
            cbCutType.ToolTipText = "입력한 사방 여백에 맞춰 사각 재단선을 생성합니다. 이미지와 페이지 크기는 유지됩니다.";
            tbColorName.ToolTipText = "커팅용 별색 채널 이름. 고객사 샘플: thru";
            tbColorName.Text = draft.Parameters.SpotColorName;
            tbLineWidth.Enabled = cbSpot.Enabled = tbColorName.Enabled = !draft.Parameters.FromSource;
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbCutType, codes);
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);
                value.Parameters.CutInsetTopMm = PostProcessDialogBinding.Read(tbTop);
                value.Parameters.CutInsetBottomMm = PostProcessDialogBinding.Read(tbBottom);
                value.Parameters.CutInsetLeftMm = PostProcessDialogBinding.Read(tbLeft);
                value.Parameters.CutInsetRightMm = PostProcessDialogBinding.Read(tbRight);
                value.Parameters.ColorMode = PostProcessDialogBinding.Selected(cbSpot, new[] { "SPOT" });
                value.Parameters.SpotColorName = tbColorName.Text.Trim();
                return value;
            }, value => Setting = value);
        }
    }
}
