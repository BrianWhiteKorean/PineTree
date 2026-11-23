using System;
using System.Windows.Forms;
using PineTree.PdfEngine;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg타공_커스텀타공_상단하단 : Form
    {
        public HoleMarkPlacement Placement { get; private set; }

        public Dlg타공_커스텀타공_상단하단() : this(0, 0) { }

        public Dlg타공_커스텀타공_상단하단(decimal width, decimal height) : this(width, height, null) { }

        public Dlg타공_커스텀타공_상단하단(decimal width, decimal height, HoleMarkPlacement existing)
            : this(width, height, existing, 1m) { }

        public Dlg타공_커스텀타공_상단하단(decimal width, decimal height, HoleMarkPlacement existing, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            if (existing != null) Text += " 수정";
            var readTop = PostProcessDialogBinding.BindHoleRun(false, false, existing?.TopRun, sourceScaleDivisor, chkTopHole, inputLabel1, tbTopHoleCount, rbTopSameGap, rbTopFixGap,
                new[] { tbTopPos1, tbTopPos2, tbTopPos3, tbTopPos4 }, new[] { inputLabel2, inputLabel3, inputLabel4, inputLabel5, inputLabel21, inputLabel22 },
                new[] { tbTopPos5, tbTopPos6, tbTopGap }, new[] { inputLabel6, inputLabel7, inputLabel8, inputLabel17, inputLabel25 },
                inputLabel4, inputLabel19, inputLabel20, inputLabel7, inputLabel3, inputLabel6);
            var readBottom = PostProcessDialogBinding.BindHoleRun(false, true, existing?.BottomRun, sourceScaleDivisor, chkBottomHole, inputLabel9, tbBottomHoleCount, rbBottomSameGap, rbBottomFixGap,
                new[] { tbBottomPos1, tbBottomPos2, tbBottomPos3, tbBottomPos4 }, new[] { inputLabel10, inputLabel11, inputLabel12, inputLabel13, inputLabel26, inputLabel27 },
                new[] { tbBottomPos5, tbBottomPos6, tbBottomGap }, new[] { inputLabel14, inputLabel15, inputLabel16, inputLabel18, inputLabel28 },
                inputLabel11, inputLabel24, inputLabel23, inputLabel15, inputLabel10, inputLabel14);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOK.Click += delegate
            {
                try
                {
                    if (!chkTopHole.Checked && !chkBottomHole.Checked)
                        throw new ArgumentException("상단과 하단 중 타공할 구간을 하나 이상 선택해주세요.");
                    var placement = new HoleMarkPlacement
                    {
                        TypeCode = "CUSTOM_SPACING",
                        ImageCoordinates = existing?.ImageCoordinates ?? true,
                        TopRun = readTop(),
                        BottomRun = readBottom()
                    };
                    placement.GetMarkCount((double)width, (double)height);
                    Placement = placement;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(this, ex.Message, "요청 간격별 타공", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
        }
    }
}
