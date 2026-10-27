using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dialog봉미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dialog봉미싱() : this(OrderPostProcessSetting.Create("POLE_SEW")) { }
        public Dialog봉미싱(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("POLE_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Number(tbLength, draft.Parameters.DimensionMm);
            chkCutLine.Checked = draft.Parameters.MakeSlit;
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbLength);
                value.Parameters.MakeSlit = chkCutLine.Checked;
                return value;
            }, value => Setting = value);
        }
    }
}
