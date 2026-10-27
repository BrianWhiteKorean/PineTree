using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dialog재단선 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dialog재단선() : this(OrderPostProcessSetting.Create("CUT")) { }
        public Dialog재단선(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("CUT");
            PostProcessDialogBinding.Select(cbCutType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Select(cbSpot, new[] { "SPOT" }, draft.Parameters.ColorMode);
            PostProcessDialogBinding.Number(tbLineWidth, draft.Parameters.LineWidthMm, 10);
            tbLineWidth.ToolTipText = "단위: mm. 고객사 기준 0.18mm / 완성 영역 네 변을 연결하는 사각 외곽선";
            cbCutType.ToolTipText = "재단선은 완성 폭·길이의 사방 외곽선입니다. 도련 2mm이면 페이지 네 가장자리에서 2mm 안쪽입니다.";
            tbColorName.ToolTipText = "커팅용 별색 채널 이름. 고객사 샘플: thru";
            tbColorName.Text = draft.Parameters.SpotColorName;
            tbLineWidth.Enabled = cbSpot.Enabled = tbColorName.Enabled = !draft.Parameters.FromSource;
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbCutType, codes);
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);
                value.Parameters.ColorMode = PostProcessDialogBinding.Selected(cbSpot, new[] { "SPOT" });
                value.Parameters.SpotColorName = tbColorName.Text.Trim();
                return value;
            }, value => Setting = value);
        }
    }
}
