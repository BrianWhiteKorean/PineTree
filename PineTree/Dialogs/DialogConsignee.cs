using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using C1.Win.C1InputPanel;
using MySql.Data.MySqlClient;
using PineTree.Classes;

namespace PineTree.Dialogs
{
    public partial class DialogConsignee : Form
    {
        public OrderShippingDetails ShippingDetails { get; private set; }

        public DialogConsignee() : this(null) { }

        public DialogConsignee(OrderShippingDetails details)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            cbShipper.DropDownStyle = InputComboBoxStyle.DropDown;
            c1Button1.Click += ConfirmShippingDetails;
            c1Button2.DialogResult = DialogResult.Cancel;
            AcceptButton = c1Button1;
            CancelButton = c1Button2;
            btnCAddress.Click += (sender, e) => SearchAddress(tbCZipCode, tbCAddress1, tbCAddress2);
            btnSAddress.Click += (sender, e) => SearchAddress(tbSZipCode, tbSAddress1, tbSAddress2);
            Shown += async (sender, e) => await LoadShippersAsync();

            if (details == null) return;

            tbConsignee.Text = details.ConsigneeName ?? string.Empty;
            tbCPhone.Text = details.ConsigneePhone ?? string.Empty;
            tbCZipCode.Text = details.ConsigneeZipCode ?? string.Empty;
            tbCAddress1.Text = details.ConsigneeAddress ?? string.Empty;
            tbCAddress2.Text = details.ConsigneeDetailAddress ?? string.Empty;
            cbShipper.Text = details.ShipperName ?? string.Empty;
            tbSPhone.Text = details.ShipperPhone ?? string.Empty;
            tbSZipCode.Text = details.ShipperZipCode ?? string.Empty;
            tbSAddress1.Text = details.ShipperAddress ?? string.Empty;
            tbSAddress2.Text = details.ShipperDetailAddress ?? string.Empty;
        }

        private async Task LoadShippersAsync()
        {
            try
            {
                using (DataTable shippers = await Task.Run(OrderRepository.GetShippers))
                {
                    if (!IsDisposed && Visible) BindShippers(shippers);
                }
            }
            catch (MySqlException)
            {
                if (!IsDisposed && Visible)
                    MessageBox.Show(this, "보내는 분 목록을 불러오지 못했습니다. 보내는 분 정보를 직접 입력할 수 있습니다.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BindShippers(DataTable shippers)
        {
            string shipperName = cbShipper.Text;
            foreach (DataRow row in shippers.Rows)
            {
                string name = Convert.ToString(row["ShipperName"]);
                if (string.IsNullOrWhiteSpace(name)) continue;

                cbShipper.Items.Add(new InputOption(name)
                {
                    Tag = new OrderShippingDetails
                    {
                        ShipperAddress = Convert.ToString(row["Address1"]),
                        ShipperDetailAddress = Convert.ToString(row["Address2"]),
                        ShipperZipCode = Convert.ToString(row["Zipcode"]),
                        ShipperPhone = Convert.ToString(row["Phone"])
                    }
                });
            }

            cbShipper.Text = shipperName;
            cbShipper.SelectedIndexChanged += ShipperSelectionChanged;
        }

        private void ShipperSelectionChanged(object sender, EventArgs e)
        {
            if (!(cbShipper.SelectedOption?.Tag is OrderShippingDetails shipper)) return;

            tbSAddress1.Text = shipper.ShipperAddress;
            tbSAddress2.Text = shipper.ShipperDetailAddress;
            tbSZipCode.Text = shipper.ShipperZipCode;
            tbSPhone.Text = shipper.ShipperPhone;
        }

        private void ConfirmShippingDetails(object sender, EventArgs e)
        {
            ShippingDetails = new OrderShippingDetails
            {
                ConsigneeName = tbConsignee.Text.Trim(),
                ConsigneePhone = tbCPhone.Text.Trim(),
                ConsigneeZipCode = tbCZipCode.Text.Trim(),
                ConsigneeAddress = tbCAddress1.Text.Trim(),
                ConsigneeDetailAddress = tbCAddress2.Text.Trim(),
                ShipperName = cbShipper.Text.Trim(),
                ShipperPhone = tbSPhone.Text.Trim(),
                ShipperZipCode = tbSZipCode.Text.Trim(),
                ShipperAddress = tbSAddress1.Text.Trim(),
                ShipperDetailAddress = tbSAddress2.Text.Trim()
            };
            DialogResult = DialogResult.OK;
        }

        private void SearchAddress(InputTextBox zipCode, InputTextBox roadAddress, InputTextBox detailAddress)
        {
            using (var dialog = new DialogAddressSearch())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var address = dialog.SelectedAddress;
                zipCode.Text = address.ZipCode;
                roadAddress.Text = address.RoadAddress;
                detailAddress.Text = address.DetailAddress;
            }
        }
    }
}
