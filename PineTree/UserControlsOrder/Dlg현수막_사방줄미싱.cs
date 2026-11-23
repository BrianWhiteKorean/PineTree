using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_사방줄미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_사방줄미싱() : this(OrderPostProcessSetting.Create("ROPE_SEW")) { }
        public Dlg현수막_사방줄미싱(OrderPostProcessSetting setting) : this(setting, 1m) { }

        public Dlg현수막_사방줄미싱(OrderPostProcessSetting setting, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("ROPE_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            decimal DefaultMargin() => cbType.SelectedIndex == 1 ? 40m : 30m;
            decimal InitialMargin(decimal? value) => draft.SeqNo == 0 && !value.HasValue && draft.Parameters.DimensionMm == 0
                ? DefaultMargin() : draft.Parameters.RopeMargin(value);
            PostProcessDialogBinding.SourceNumber(tbTop, InitialMargin(draft.Parameters.RopeTopMm), sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbBottom, InitialMargin(draft.Parameters.RopeBottomMm), sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbLeft, InitialMargin(draft.Parameters.RopeLeftMm), sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbRight, InitialMargin(draft.Parameters.RopeRightMm), sourceScaleDivisor);
            PostProcessDialogBinding.UpdateSourceDefaultsOnTypeChange(cbType, sourceScaleDivisor, DefaultMargin, tbTop, tbBottom, tbLeft, tbRight);
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = PostProcessDialogBinding.Read(tbTop) * sourceScaleDivisor;
                value.Parameters.RopeTopMm = value.Parameters.DimensionMm;
                value.Parameters.RopeBottomMm = PostProcessDialogBinding.Read(tbBottom) * sourceScaleDivisor;
                value.Parameters.RopeLeftMm = PostProcessDialogBinding.Read(tbLeft) * sourceScaleDivisor;
                value.Parameters.RopeRightMm = PostProcessDialogBinding.Read(tbRight) * sourceScaleDivisor;
                return value;
            }, value => Setting = value);
        }
    }
}
