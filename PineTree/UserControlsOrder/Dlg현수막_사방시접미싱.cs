using System;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_사방시접미싱 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_사방시접미싱() : this(OrderPostProcessSetting.Create("HEM_SEW")) { }
        public Dlg현수막_사방시접미싱(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("HEM_SEW");
            PostProcessDialogBinding.Select(cbType, codes, draft.Parameters.TypeCode);
            inputLabel2.Text = "사방 여유(mm)";
            inputLabel2.Width = 95;
            tbLength.ReadOnly = true;
            tbLength.ToolTipText = "최종 출력 기준. 1번 말아 미싱은 각 15mm, 2번 말아 미싱은 각 30mm입니다. 확대비율과 관계없이 유지됩니다.";
            Action updateMargin = () => PostProcessDialogBinding.Number(tbLength, cbType.SelectedIndex == 1 ? 30m : 15m);
            cbType.SelectedIndexChanged += delegate { updateMargin(); };
            updateMargin();
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbType, codes);
                value.Parameters.DimensionMm = value.Parameters.HemMarginMm();
                return value;
            }, value => Setting = value);
        }
    }
}
