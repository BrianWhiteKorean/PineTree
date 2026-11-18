using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogDesignModeExit1 : Form
    {
        public DialogDesignModeExit1()
        {
            InitializeComponent();
            btnExit.DialogResult = DialogResult.Yes;
            btnStay.DialogResult = DialogResult.No;
            btnManagement.DialogResult = DialogResult.Retry;
            AcceptButton = CancelButton = btnStay;
            ActiveControl = btnStay;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
        }
    }
}
