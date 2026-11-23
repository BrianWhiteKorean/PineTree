using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg페트_AL족자 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg페트_AL족자() : this(OrderPostProcessSetting.Create("AL_SCROLL")) { }
        public Dlg페트_AL족자(OrderPostProcessSetting setting) : this(setting, 1m) { }

        public Dlg페트_AL족자(OrderPostProcessSetting setting, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            PostProcessDialogBinding.SourceNumber(tbLineWidth, draft.Parameters.LineWidthMm, sourceScaleDivisor, 10);
            PostProcessDialogBinding.Select(cbSpot, new[] { "SPOT" }, draft.Parameters.ColorMode);
            tbColorName.Text = draft.Parameters.SpotColorName;
            PostProcessDialogBinding.SourceNumber(tbTop, draft.Parameters.ScrollTopMm, sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbBottom, draft.Parameters.ScrollBottomMm, sourceScaleDivisor);
            tbLineWidth.ToolTipText = "선택한 원본 축척에서의 선 두께(mm). 재단선은 완성 기준으로 가장자리에서 2mm 안쪽에 표시합니다.";
            tbColorName.ToolTipText = "커팅용 별색 채널 이름. 고객사 샘플: thru";
            tbTop.ToolTipText = tbBottom.ToolTipText = "선택한 원본 축척에서 이미지 위·아래에 추가할 흰 여백(mm).";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth) * sourceScaleDivisor;
                value.Parameters.ColorMode = PostProcessDialogBinding.Selected(cbSpot, new[] { "SPOT" });
                value.Parameters.SpotColorName = tbColorName.Text.Trim();
                value.Parameters.ScrollTopMm = PostProcessDialogBinding.Read(tbTop) * sourceScaleDivisor;
                value.Parameters.ScrollBottomMm = PostProcessDialogBinding.Read(tbBottom) * sourceScaleDivisor;
                return value;
            }, value => Setting = value);
        }
    }
}
