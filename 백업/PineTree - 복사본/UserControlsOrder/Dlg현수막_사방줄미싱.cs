using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_사방줄미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_사방줄미싱() : this(OrderPostProcessSetting.Create("ROPE_SEW")) { }
        public Dlg현수막_사방줄미싱(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("ROPE_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Number(tbTop, draft.Parameters.RopeMargin(draft.Parameters.RopeTopMm));
            PostProcessDialogBinding.Number(tbBottom, draft.Parameters.RopeMargin(draft.Parameters.RopeBottomMm));
            PostProcessDialogBinding.Number(tbLeft, draft.Parameters.RopeMargin(draft.Parameters.RopeLeftMm));
            PostProcessDialogBinding.Number(tbRight, draft.Parameters.RopeMargin(draft.Parameters.RopeRightMm));
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbTop);
                value.Parameters.RopeTopMm = value.Parameters.DimensionMm;
                value.Parameters.RopeBottomMm = PostProcessDialogBinding.Read(tbBottom);
                value.Parameters.RopeLeftMm = PostProcessDialogBinding.Read(tbLeft);
                value.Parameters.RopeRightMm = PostProcessDialogBinding.Read(tbRight);
                return value;
            }, value => Setting = value);
        }
    }
}
