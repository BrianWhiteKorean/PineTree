using System;
using System.Windows.Forms;
using PineTree.PdfEngine;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg타공_사방타공 : Form
    {
        public HoleMarkPlacement Placement { get; private set; }

        public Dlg타공_사방타공() : this(0, 0) { }

        public Dlg타공_사방타공(decimal width, decimal height) : this(width, height, 1m) { }

        public Dlg타공_사방타공(decimal width, decimal height, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            var toggles = new[] { inputCheckBox1, inputCheckBox2, inputCheckBox3, inputCheckBox4 };
            foreach (var toggle in toggles)
            {
                toggle.Checked = true;
                toggle.ToolTipText = "체크한 방향에 타공점을 배치합니다. 인접 방향은 모서리를 공유합니다.";
            }
            foreach (var input in new[] { tbTop, tbBottom, tbLeft, tbRight })
            {
                PostProcessDialogBinding.SourceNumber(input, 20m, sourceScaleDivisor);
                input.ToolTipText = "선택한 원본 축척에서 재단선부터 타공 중심까지의 거리(mm). 인접 방향의 끝점에도 적용됩니다.";
            }

            foreach (var toggle in toggles) toggle.CheckedChanged += delegate { UpdateCount(); };
            UpdateCount();
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOK.Click += delegate
            {
                try
                {
                    var edges = SelectedEdges();
                    if (!edges.HasEnabledEdge()) throw new ArgumentException("타공할 방향을 하나 이상 선택해주세요.");
                    edges.TopMm = (double)(PostProcessDialogBinding.Read(tbTop) * sourceScaleDivisor);
                    edges.BottomMm = (double)(PostProcessDialogBinding.Read(tbBottom) * sourceScaleDivisor);
                    edges.LeftMm = (double)(PostProcessDialogBinding.Read(tbLeft) * sourceScaleDivisor);
                    edges.RightMm = (double)(PostProcessDialogBinding.Read(tbRight) * sourceScaleDivisor);
                    var placement = new HoleMarkPlacement { TypeCode = "FOUR_CORNERS", Edges = edges };
                    placement.Validate();
                    if (width > 0 && height > 0)
                        edges.GetMarkCount((double)width, (double)height, placement.TypeCode, placement.SpacingMm);
                    Placement = placement;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(this, ex.Message, "사방타공", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
        }

        private HoleMarkEdges SelectedEdges()
        {
            return new HoleMarkEdges
            {
                TopEnabled = inputCheckBox1.Checked,
                BottomEnabled = inputCheckBox2.Checked,
                LeftEnabled = inputCheckBox3.Checked,
                RightEnabled = inputCheckBox4.Checked
            };
        }

        private void UpdateCount()
        {
            inputGroupHeader1.Text = $"사방타공 ({SelectedEdges().GetCornerCount()}개)";
        }
    }
}
