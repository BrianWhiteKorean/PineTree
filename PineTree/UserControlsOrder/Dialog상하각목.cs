using System;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dialog상하각목 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dialog상하각목() : this(OrderPostProcessSetting.Create("WOOD")) { }
        public Dialog상하각목(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            PostProcessDialogBinding.Number(tbExtraLine, draft.Parameters.ExtraLineMm ?? draft.Parameters.DimensionMm);
            PostProcessDialogBinding.Number(tbDesignLine, draft.Parameters.DesignLineMm);
            chkDesignLine.Checked = draft.Parameters.MakeDesignLine;
            tbDesignLine.Enabled = chkDesignLine.Checked;
            chkDesignLine.CheckedChanged += delegate { tbDesignLine.Enabled = chkDesignLine.Checked; };
            tbExtraLine.ToolTipText = "상하 각각 추가할 흰 여백(mm). 빈값은 0 / 원본 그림 크기는 유지합니다.";
            tbDesignLine.ToolTipText = "원본 이미지의 상하 경계에서 여백 쪽으로 이격(mm). 이 값으로 출력 폭을 더 늘리지 않습니다.";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = "LEFT_RIGHT";
                value.Parameters.ExtraLineMm = ReadOrZero(tbExtraLine);
                value.Parameters.DimensionMm = value.Parameters.ExtraLineMm.Value;
                value.Parameters.MakeDesignLine = chkDesignLine.Checked;
                value.Parameters.DesignLineMm = ReadOrZero(tbDesignLine);
                return value;
            }, value => Setting = value);
        }
        private static decimal ReadOrZero(C1.Win.C1InputPanel.InputNumericBox input)
        {
            return input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text) ? 0 : Convert.ToDecimal(input.Value);
        }
    }
}
