using System;
using System.Drawing;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogFeeding : Form
    {
        internal OrderProductionStatus? SelectedStatus { get; private set; }

        public DialogFeeding()
        {
            InitializeComponent();
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            btnOK.Enabled = false;
            btnPrintRoom.Click += delegate { SelectDestination(OrderProductionStatus.Printing); };
            btnWorkRoom.Click += delegate { SelectDestination(OrderProductionStatus.PostProcess); };
            btnOK.Click += delegate
            {
                if (SelectedStatus.HasValue) DialogResult = DialogResult.OK;
            };
            btnCancel.DialogResult = DialogResult.Cancel;
        }

        private void SelectDestination(OrderProductionStatus status)
        {
            SelectedStatus = status;
            btnPrintRoom.BackColor = status == OrderProductionStatus.Printing ? SystemColors.Highlight : SystemColors.Control;
            btnPrintRoom.ForeColor = status == OrderProductionStatus.Printing ? SystemColors.HighlightText : SystemColors.ControlText;
            btnWorkRoom.BackColor = status == OrderProductionStatus.PostProcess ? SystemColors.Highlight : SystemColors.Control;
            btnWorkRoom.ForeColor = status == OrderProductionStatus.PostProcess ? SystemColors.HighlightText : SystemColors.ControlText;
            btnOK.Enabled = true;
        }
    }
}
