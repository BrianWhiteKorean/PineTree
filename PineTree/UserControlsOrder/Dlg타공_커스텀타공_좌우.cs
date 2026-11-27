using System;
using System.Windows.Forms;
using C1.Win.C1InputPanel;
using PineTree.PdfEngine;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg타공_커스텀타공_좌우 : Form
    {
        public HoleMarkPlacement Placement { get; private set; }

        public Dlg타공_커스텀타공_좌우() : this(0, 0) { }

        public Dlg타공_커스텀타공_좌우(decimal width, decimal height) : this(width, height, null) { }

        public Dlg타공_커스텀타공_좌우(decimal width, decimal height, HoleMarkPlacement existing)
            : this(width, height, existing, 1m) { }

        public Dlg타공_커스텀타공_좌우(decimal width, decimal height, HoleMarkPlacement existing, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            if (existing != null) Text += " 수정";
            var readLeft = PostProcessDialogBinding.BindHoleRun(true, false, existing?.LeftRun, sourceScaleDivisor, chkLeftHole, inputLabel1, tbLeftHoleCount, rbLeftSameGap, rbLeftFixGap,
                new[] { tbLeftPos1, tbLeftPos2, tbLeftPos3, tbLeftPos4 }, new[] { inputLabel2, inputLabel3, inputLabel4, inputLabel5, inputLabel21, inputLabel22 },
                new[] { tbLeftPos5, tbLeftPos6, tbLeftGap },
                new InputComponent[] { inputLabel6, inputLabel7, inputLabel8, inputLabel17, inputLabel25,
                    inputLabel29, inputLabel30, inputLabel31, tbLeftPos7, tbLeftPos8, inputLabel32 },
                inputLabel4, inputLabel19, inputLabel20, inputLabel7, inputLabel3, inputLabel6);
            var readRight = PostProcessDialogBinding.BindHoleRun(true, true, existing?.RightRun, sourceScaleDivisor, chkRightHole, inputLabel9, tbRightHoleCount, rbRightSameGap, rbRightFixGap,
                new[] { tbRightPos1, tbRightPos2, tbRightPos3, tbRightPos4 }, new[] { inputLabel10, inputLabel11, inputLabel12, inputLabel13, inputLabel26, inputLabel27 },
                new[] { tbRightPos5, tbRightPos6, tbRightGap },
                new InputComponent[] { inputLabel14, inputLabel15, inputLabel16, inputLabel18, inputLabel28,
                    inputLabel33, inputLabel34, inputLabel35, tbRightPos7, tbRightPos8, inputLabel36 },
                inputLabel11, inputLabel24, inputLabel23, inputLabel15, inputLabel10, inputLabel14);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOK.Click += delegate
            {
                try
                {
                    if (!chkLeftHole.Checked && !chkRightHole.Checked)
                        throw new ArgumentException("좌측과 우측 중 타공할 구간을 하나 이상 선택해주세요.");
                    var placement = new HoleMarkPlacement
                    {
                        TypeCode = "CUSTOM_SPACING",
                        ImageCoordinates = existing?.ImageCoordinates ?? true,
                        TopRun = existing?.TopRun,
                        BottomRun = existing?.BottomRun,
                        LeftRun = readLeft(),
                        RightRun = readRight()
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
