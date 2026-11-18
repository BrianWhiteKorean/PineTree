using System;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public class UcDesignModeContainer : UserControl
    {
        private readonly UcOrderDesignPad _orderDesigner;

        public event EventHandler OrderSaved;

        public UcDesignModeContainer() : this(new UcOrderDesignPad()) { }

        internal UcDesignModeContainer(UcOrderDesignPad orderDesigner)
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.AppBackground;
            _orderDesigner = orderDesigner;
            _orderDesigner.Dock = DockStyle.Fill;
            _orderDesigner.OrderSaved += OrderDesigner_OrderSaved;
            _orderDesigner.CancelRequested += OrderDesigner_CancelRequested;
            Controls.Add(_orderDesigner);
            Controls.Add(new Panel
            {
                Dock = DockStyle.Top,
                Height = UiTheme.TopBarHeight,
                BackColor = UiTheme.TopBarBackground
            });
        }

        private void OrderDesigner_OrderSaved(object sender, EventArgs e)
        {
            EventHandler handler = OrderSaved;
            if (handler != null) handler(this, EventArgs.Empty);
            _orderDesigner.ResetNewOrder();
        }

        private void OrderDesigner_CancelRequested(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "작성 중인 내용을 지우고 새 주문을 준비할까요?", "새 주문",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _orderDesigner.ResetNewOrder();
            }
        }
    }
}
