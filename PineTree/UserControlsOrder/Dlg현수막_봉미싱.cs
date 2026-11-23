using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_봉미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_봉미싱() : this(OrderPostProcessSetting.Create("POLE_SEW")) { }
        public Dlg현수막_봉미싱(OrderPostProcessSetting setting) : this(setting, 1m) { }

        public Dlg현수막_봉미싱(OrderPostProcessSetting setting, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("POLE_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            decimal DefaultLength()
            {
                switch (cbType.SelectedIndex)
                {
                    case 0:
                    case 1: return 100m;
                    case 4: return 80m;
                    default: return 0m;
                }
            }
            decimal length = draft.SeqNo == 0 && draft.Parameters.DimensionMm == 0
                ? DefaultLength() : draft.Parameters.DimensionMm;
            PostProcessDialogBinding.SourceNumber(tbLength, length, sourceScaleDivisor);
            PostProcessDialogBinding.UpdateSourceDefaultsOnTypeChange(cbType, sourceScaleDivisor, DefaultLength, tbLength);
            chkCutLine.Checked = draft.Parameters.MakeSlit || (draft.SeqNo == 0 && cbType.SelectedIndex == 4);
            cbType.SelectedIndexChanged += delegate { chkCutLine.Checked = cbType.SelectedIndex == 4; };
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbLength) * sourceScaleDivisor;
                value.Parameters.MakeSlit = chkCutLine.Checked;
                return value;
            }, value => Setting = value);
        }
    }
}
