using System;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_사방시접미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_사방시접미싱() : this(OrderPostProcessSetting.Create("HEM_SEW")) { }
        public Dlg현수막_사방시접미싱(OrderPostProcessSetting setting) : this(setting, 1m) { }

        public Dlg현수막_사방시접미싱(OrderPostProcessSetting setting, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("HEM_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            inputLabel2.Text = "사방 여유(mm)";
            inputLabel2.Width = 95;
            tbLength.ReadOnly = true;
            Action updateMargin = () => PostProcessDialogBinding.SourceNumber(tbLength, (cbType.SelectedIndex == 1 ? 30m : 15m), sourceScaleDivisor);
            cbType.SelectedIndexChanged += delegate { updateMargin(); };
            updateMargin();
            tbLength.ToolTipText = "선택한 원본 축척에서의 사방 여유입니다. 완성 기준으로 1번은 15mm이고 2번은 30mm입니다.";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = value.Parameters.HemMarginMm();
                return value;
            }, value => Setting = value);
        }
    }
}
