using System;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcProcessRuleType1 : UserControl
    {
        private long _priceItemSeqNo;
        private ProcessRuleData _rule;
        private ProcessRuleData _original;
        private bool _isSaving;
        public event EventHandler Saved;
        public event EventHandler Cancelled;
        public event EventHandler Deleted;
        public event EventHandler SavingChanged;
        public bool IsSaving { get { return _isSaving; } }

        public UcProcessRuleType1()
        {
            InitializeComponent();

        }

        public void SetRule(long priceItemSeqNo, ProcessRuleData rule)
        {
        }


    }
}
