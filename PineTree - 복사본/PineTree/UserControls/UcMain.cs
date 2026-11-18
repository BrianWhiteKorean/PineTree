using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public partial class UcMain : UserControl, INavigationView
    {
        private readonly NavigationController _navigationController;
        private readonly Dictionary<OrderProductionStatus, Label> _statusCountLabels = new Dictionary<OrderProductionStatus, Label>();

        public UcMain()
        {
            InitializeComponent();

            _navigationController = new NavigationController(picBackground);
            _navigationController.NavigationRequested += NavigationController_NavigationRequested;
            InitStatusBoard();
            Load += UcMain_Load;
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;

        private async void UcMain_Load(object sender, EventArgs e)
        {
            InitTodayOrderGrid();
            await Task.WhenAll(LoadTodayOrdersAsync(), LoadStatusBoardAsync());
        }

        private void NavigationController_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }

        private async Task LoadTodayOrdersAsync()
        {
            try
            {
                DateTime today = DateTime.Today;
                DataTable orders = await Task.Run(() => OrderRepository.GetOrdersByOrderDate(today));

                if (IsDisposed)
                {
                    return;
                }

                BindTodayOrders(orders);
            }
            catch (Exception ex)
            {
                MessageBox.Show("오늘 주문 내역을 조회하지 못했습니다.\r\n" + ex.Message, "오늘 주문", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task LoadStatusBoardAsync()
        {
            try
            {
                DateTime today = DateTime.Today;
                Dictionary<OrderProductionStatus, int> counts =
                    await Task.Run(() => OrderRepository.GetStatusCountsByOrderDate(today));

                if (IsDisposed)
                {
                    return;
                }

                BindStatusBoard(counts);
            }
            catch (Exception ex)
            {
                MessageBox.Show("오늘의 현황을 조회하지 못했습니다.\r\n" + ex.Message, "오늘의 현황", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitStatusBoard()
        {
            foreach (StatusBoardCountTarget target in MainStatusBoardLayout.CountTargets)
            {
                Label label = new Label();
                label.BackColor = Color.Transparent;
                label.Bounds = target.Bounds;
                label.Font = new Font("맑은 고딕", 30F, FontStyle.Regular, GraphicsUnit.Point, 129);
                label.ForeColor = Color.Black;
                label.Text = "0";
                label.TextAlign = ContentAlignment.MiddleRight;

                _statusCountLabels[target.Status] = label;
                picBackground.Controls.Add(label);
                label.BringToFront();
            }
        }

        private void BindStatusBoard(Dictionary<OrderProductionStatus, int> counts)
        {
            foreach (KeyValuePair<OrderProductionStatus, Label> item in _statusCountLabels)
            {
                int count;
                if (!counts.TryGetValue(item.Key, out count))
                {
                    count = 0;
                }

                item.Value.Text = count.ToString("#,##0");
            }
        }

        private void BindTodayOrders(DataTable orders)
        {
            PrepareTodayOrderGridData(orders);
            FGrid1.DataSource = orders;
            InitTodayOrderGrid();
        }

        private void PrepareTodayOrderGridData(DataTable orders)
        {
            if (!orders.Columns.Contains("No"))
            {
                orders.Columns.Add("No", typeof(int)).SetOrdinal(0);
            }

            for (int i = 0; i < orders.Rows.Count; i++)
            {
                orders.Rows[i]["No"] = i + 1;
            }
        }

        private void InitTodayOrderGrid()
        {
            FGrid1.Cols.Fixed = 0;
            FGrid1.BackColor = Color.MintCream;
            FGrid1.AllowMergingFixed = AllowMergingEnum.FixedOnly;
            FGrid1.AllowSorting = AllowSortingEnum.SingleColumn;
            FGrid1.SelectionMode = SelectionModeEnum.Row;
            FGrid1.Rows[0].TextAlign = TextAlignEnum.CenterCenter;

            SetColumn("OrderId", string.Empty, 0, TextAlignEnum.CenterCenter, false, false);
            SetColumn("No", "번호", 60, TextAlignEnum.CenterCenter, false, true);
            SetColumn("OrderDate", "주문일", 100, TextAlignEnum.CenterCenter, false, true, "yyyy-MM-dd");
            SetColumn("ReceivedAt", "접수일시", 140, TextAlignEnum.CenterCenter, false, true, "yyyy-MM-dd HH:mm");
            SetColumn("CustomerName", "거래처명", 180, TextAlignEnum.LeftCenter, false, true);
            SetColumn("CustCode", "거래처코드", 100, TextAlignEnum.CenterCenter, false, true);
            SetColumn("OrderNo", "주문번호", 150, TextAlignEnum.CenterCenter, false, true);
            SetColumn("OrderTitle", "주문명", 280, TextAlignEnum.LeftCenter, false, true);
            SetColumn("ProductionStatus", "진행상태", 110, TextAlignEnum.CenterCenter, false, true);
            SetColumn("RequestedDueAt", "요청납기", 140, TextAlignEnum.CenterCenter, false, true, "yyyy-MM-dd HH:mm");
            SetColumn("SourceChannel", "접수채널", 100, TextAlignEnum.CenterCenter, false, true);
        }

        private void SetColumn(string columnName, string caption, int width, TextAlignEnum textAlign, bool allowEditing, bool visible, string format = null)
        {
            if (!FGrid1.Cols.Contains(columnName))
            {
                return;
            }

            FGrid1.Cols[columnName].Caption = caption;
            FGrid1.Cols[columnName].TextAlign = textAlign;
            FGrid1.Cols[columnName].AllowEditing = allowEditing;
            FGrid1.Cols[columnName].Visible = visible;
            FGrid1.Cols[columnName].Width = width;

            if (!string.IsNullOrWhiteSpace(format))
            {
                FGrid1.Cols[columnName].Format = format;
            }
        }
    }
}
