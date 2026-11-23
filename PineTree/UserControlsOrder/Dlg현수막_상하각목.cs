using System;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg현수막_상하각목 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg현수막_상하각목() : this(OrderPostProcessSetting.Create("WOOD_TB")) { }
        public Dlg현수막_상하각목(OrderPostProcessSetting setting) : this(setting, 1m) { }
        public Dlg현수막_상하각목(OrderPostProcessSetting setting, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var draft = setting.Copy();
            if (sourceScaleDivisor <= 0) sourceScaleDivisor = 1m;
            if (draft.SeqNo == 0 && !draft.Parameters.ExtraLineMm.HasValue && draft.Parameters.DimensionMm == 0)
                draft.Parameters.ExtraLineMm = 80m;
            PostProcessDialogBinding.SourceNumber(tbExtraLine, (draft.Parameters.ExtraLineMm ?? draft.Parameters.DimensionMm), sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbDesignLine, draft.Parameters.DesignLineMm, sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbExtraLineThickness, (draft.Parameters.ExtraLineThicknessMm ?? .6m), sourceScaleDivisor);
            PostProcessDialogBinding.SourceNumber(tbDesignLineThickness, (draft.Parameters.DesignLineThicknessMm ?? .6m), sourceScaleDivisor);
            chkDesignLine.Checked = draft.Parameters.MakeDesignLine;
            tbDesignLine.Enabled = chkDesignLine.Checked;
            tbDesignLineThickness.Enabled = chkDesignLine.Checked;
            chkDesignLine.CheckedChanged += delegate { tbDesignLine.Enabled = tbDesignLineThickness.Enabled = chkDesignLine.Checked; };
            tbExtraLine.ToolTipText = "상하 각각 추가할 흰 여백(mm). 빈값은 0 / 원본 그림 크기는 유지합니다.";
            tbDesignLine.ToolTipText = "원본 이미지의 상하 경계에서 여백 쪽으로 이격(mm). 이 값으로 출력 높이를 더 늘리지 않습니다.";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.TypeCode = "TOP_BOTTOM";
                value.Parameters.ExtraLineMm = ReadOrZero(tbExtraLine) * sourceScaleDivisor;
                value.Parameters.DimensionMm = value.Parameters.ExtraLineMm.Value;
                value.Parameters.MakeDesignLine = chkDesignLine.Checked;
                value.Parameters.DesignLineMm = ReadOrZero(tbDesignLine) * sourceScaleDivisor;
                value.Parameters.ExtraLineThicknessMm = PostProcessDialogBinding.Read(tbExtraLineThickness) * sourceScaleDivisor;
                value.Parameters.DesignLineThicknessMm = PostProcessDialogBinding.Read(tbDesignLineThickness) * sourceScaleDivisor;
                return value;
            }, value => Setting = value);
        }
        private static decimal ReadOrZero(C1.Win.C1InputPanel.InputNumericBox input)
        {
            return input.ValueIsNull || string.IsNullOrWhiteSpace(input.Text) ? 0 : Convert.ToDecimal(input.Value);
        }
    }
}
