using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dialog사방시접미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dialog사방시접미싱() : this(OrderPostProcessSetting.Create("HEM_SEW")) { }
        public Dialog사방시접미싱(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("HEM_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Number(tbLength, draft.Parameters.DimensionMm);
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbLength);
                return value;
            }, value => Setting = value);
        }
    }
}
