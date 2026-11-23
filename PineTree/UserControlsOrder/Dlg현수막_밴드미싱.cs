using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_밴드미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_밴드미싱() : this(OrderPostProcessSetting.Create("BAND_SEW")) { }
        public Dlg현수막_밴드미싱(OrderPostProcessSetting setting) : this(setting, 1m) { }

        public Dlg현수막_밴드미싱(OrderPostProcessSetting setting, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("BAND_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            decimal DefaultMargin() => cbType.SelectedIndex == 1 ? 40m : 30m;
            decimal margin = draft.SeqNo == 0 && draft.Parameters.DimensionMm == 0
                ? DefaultMargin() : draft.Parameters.DimensionMm;
            PostProcessDialogBinding.SourceNumber(tbLineWidth, margin, sourceScaleDivisor);
            PostProcessDialogBinding.UpdateSourceDefaultsOnTypeChange(cbType, sourceScaleDivisor, DefaultMargin, tbLineWidth);
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbLineWidth) * sourceScaleDivisor;
                return value;
            }, value => Setting = value);
        }
    }
}
