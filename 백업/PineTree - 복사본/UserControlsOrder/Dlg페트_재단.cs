using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg페트_재단 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg페트_재단() : this(OrderPostProcessSetting.Create("CUT")) { }
        public Dlg페트_재단(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            PostProcessDialogBinding.Number(tbLineWidth, draft.Parameters.LineWidthMm, 10);
            tbLineWidth.ToolTipText = "단위: mm. 고객사 기준 0.18mm / 입력한 여백 안쪽의 사각 재단선";
            PostProcessDialogBinding.Number(tbTop, draft.Parameters.CutInsetTopMm ?? (draft.Parameters.FromSource ? 0m : 2m));
            PostProcessDialogBinding.Number(tbBottom, draft.Parameters.CutInsetBottomMm ?? (draft.Parameters.FromSource ? 0m : 2m));
            PostProcessDialogBinding.Number(tbLeft, draft.Parameters.CutInsetLeftMm ?? (draft.Parameters.FromSource ? 0m : 2m));
            PostProcessDialogBinding.Number(tbRight, draft.Parameters.CutInsetRightMm ?? (draft.Parameters.FromSource ? 0m : 2m));
            tbLineWidth.Enabled = tbTop.Enabled = tbBottom.Enabled = tbLeft.Enabled = tbRight.Enabled = !draft.Parameters.FromSource;
            foreach (var input in new[] { tbTop, tbBottom, tbLeft, tbRight })
                input.ToolTipText = "단위: mm. 출력 페이지 가장자리에서 안쪽으로 재단선까지의 거리. 이미지와 페이지 크기는 유지됩니다.";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);
                value.Parameters.CutInsetTopMm = PostProcessDialogBinding.Read(tbTop);
                value.Parameters.CutInsetBottomMm = PostProcessDialogBinding.Read(tbBottom);
                value.Parameters.CutInsetLeftMm = PostProcessDialogBinding.Read(tbLeft);
                value.Parameters.CutInsetRightMm = PostProcessDialogBinding.Read(tbRight);
                return value;
            }, value => Setting = value);
        }
    }
}
