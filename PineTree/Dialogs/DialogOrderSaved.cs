using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class DialogOrderSaved : Form
    {
        public DialogOrderSaved()
        {
            InitializeComponent();
            btnNewWork.DialogResult = DialogResult.Yes;
            btnManagement.DialogResult = DialogResult.No;
            AcceptButton = btnNewWork;
            CancelButton = btnManagement;
        }
    }
}
