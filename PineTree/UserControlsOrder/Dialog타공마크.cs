using System;
using System.Windows.Forms;
using C1.Win.C1InputPanel;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dialog타공마크 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        private readonly InputNumericBox _spacingInput;
        public Dialog타공마크() : this(OrderPostProcessSetting.Create("HOLE"), 0, 0) { }
        public Dialog타공마크(OrderPostProcessSetting setting, decimal width, decimal height)
        {
            InitializeComponent();
            var draft = setting.Copy();
            var codes = OrderPostProcessSetting.OptionsFor("HOLE");
            PostProcessDialogBinding.Select(cbHoleType, codes, draft.Parameters.TypeCode);
            PostProcessDialogBinding.Number(tbMarkLeftRight, draft.Parameters.MarkLeftRightMm);
            PostProcessDialogBinding.Number(tbMarkTopBottom, draft.Parameters.MarkTopBottomMm);
            tbMarkLeftRight.ToolTipText = "좌우 재단선에서 타공 중심까지 안쪽 거리(mm). 고객사 기본 20mm / 도련 2mm이면 페이지 끝에서 22mm";
            tbMarkTopBottom.ToolTipText = "상하 재단선에서 타공 중심까지 안쪽 거리(mm). 고객사 기본 20mm";
            // The designer owns this optional field; never create a replacement at runtime.
            _spacingInput = c1InputPanel1.Items["tbHoleSpacing"] as InputNumericBox;
            if (_spacingInput != null) PostProcessDialogBinding.Number(_spacingInput, draft.Parameters.HoleSpacingMm);
            tbHoleCount.ReadOnly = true; tbHoleCount.Maximum = 2000; tbHoleCount.DecimalPlaces = 0;
            Func<OrderPostProcessSetting> read = () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = PostProcessDialogBinding.Selected(cbHoleType, codes);
                value.Parameters.MarkLeftRightMm = PostProcessDialogBinding.Read(tbMarkLeftRight);
                value.Parameters.MarkTopBottomMm = PostProcessDialogBinding.Read(tbMarkTopBottom);
                if (_spacingInput != null) value.Parameters.HoleSpacingMm = PostProcessDialogBinding.Read(_spacingInput);
                else if (value.Parameters.TypeCode == "CUSTOM_SPACING") throw new ArgumentException("디자이너에 최대 간격 입력란 tbHoleSpacing을 추가해주세요.");
                return value;
            };
            Action updateCount = () => {
                if (_spacingInput != null) _spacingInput.Enabled = cbHoleType.SelectedIndex == 1;
                try {
                    var value = read();
                    tbHoleCount.Value = width > 0 && height > 0 ? value.GetMarkCount(width, height) : value.Parameters.TypeCode == "FOUR_CORNERS" ? 4 : 0;
                    tbHoleCount.ToolTipText = width > 0 && height > 0 ? "출력 치수에 따라 자동 계산" : "출력 치수를 입력하면 개수를 계산합니다.";
                }
                catch (ArgumentException ex) { tbHoleCount.Value = 0; tbHoleCount.ToolTipText = ex.Message; }
            };
            cbHoleType.SelectedIndexChanged += delegate { updateCount(); };
            tbMarkLeftRight.ValueChanged += delegate { updateCount(); };
            tbMarkTopBottom.ValueChanged += delegate { updateCount(); };
            if (_spacingInput != null) _spacingInput.ValueChanged += delegate { updateCount(); };
            updateCount();
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = read(); value.Validate();
                if (width > 0 && height > 0) value.GetMarkCount(width, height);
                return value;
            }, value => Setting = value);
        }
    }
}
