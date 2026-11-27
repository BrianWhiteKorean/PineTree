using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg페트_AL족자 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg페트_AL족자() : this(OrderPostProcessSetting.Create("AL_SCROLL")) { }
        public Dlg페트_AL족자(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            PostProcessDialogBinding.Number(tbLineWidth, draft.Parameters.LineWidthMm, 10);
            PostProcessDialogBinding.Select(cbSpot, new[] { "SPOT" }, draft.Parameters.ColorMode);
            tbColorName.Text = draft.Parameters.SpotColorName;
            PostProcessDialogBinding.Number(tbTop, draft.Parameters.ScrollTopMm);
            PostProcessDialogBinding.Number(tbBottom, draft.Parameters.ScrollBottomMm);
            tbLineWidth.ToolTipText = "단위: mm. 추가 여백을 포함한 페이지 가장자리에서 2mm 안쪽에 재단선을 표시합니다.";
            tbColorName.ToolTipText = "커팅용 별색 채널 이름. 고객사 샘플: thru";
            tbTop.ToolTipText = tbBottom.ToolTipText = "단위: mm. 확대된 원본 위·아래에 추가할 흰 여백. 이미지 크기는 유지됩니다.";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);
                value.Parameters.ColorMode = PostProcessDialogBinding.Selected(cbSpot, new[] { "SPOT" });
                value.Parameters.SpotColorName = tbColorName.Text.Trim();
                value.Parameters.ScrollTopMm = PostProcessDialogBinding.Read(tbTop);
                value.Parameters.ScrollBottomMm = PostProcessDialogBinding.Read(tbBottom);
                return value;
            }, value => Setting = value);
        }
    }
}
