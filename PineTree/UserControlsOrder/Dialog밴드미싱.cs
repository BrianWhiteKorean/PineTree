using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dialog밴드미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dialog밴드미싱() : this(OrderPostProcessSetting.Create("BAND_SEW")) { }
        public Dialog밴드미싱(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("BAND_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Number(tbLineWidth, draft.Parameters.DimensionMm);
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbLineWidth);
                return value;
            }, value => Setting = value);
        }
    }
}
