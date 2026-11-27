using System;
using System.Data;
using System.Web.Script.Serialization;

namespace PineTree.Classes
{
    public sealed class OrderShippingDetails
    {
        public string ConsigneeName { get; set; }
        public string ConsigneePhone { get; set; }
        public string ConsigneeZipCode { get; set; }
        public string ConsigneeAddress { get; set; }
        public string ConsigneeDetailAddress { get; set; }
        public string ShipperName { get; set; }
        public string ShipperPhone { get; set; }
        public string ShipperZipCode { get; set; }
        public string ShipperAddress { get; set; }
        public string ShipperDetailAddress { get; set; }

        [ScriptIgnore]
        public string DeliveryAddress => (ConsigneeAddress + " " + ConsigneeDetailAddress).Trim();

        internal string ToJson() => new JavaScriptSerializer().Serialize(this);

        internal static OrderShippingDetails FromOrder(DataRow row)
        {
            string address = row.Table.Columns.Contains("DeliveryAddress")
                ? Convert.ToString(row["DeliveryAddress"]) : string.Empty;
            string json = row.Table.Columns.Contains("ShippingDetailsJSON")
                ? Convert.ToString(row["ShippingDetailsJSON"]) : string.Empty;
            var details = string.IsNullOrEmpty(json)
                ? new OrderShippingDetails() : new JavaScriptSerializer().Deserialize<OrderShippingDetails>(json);

            // Older clients can still change DeliveryAddress independently.
            if (!string.Equals(details.DeliveryAddress, address, StringComparison.Ordinal))
            {
                details.ConsigneeAddress = address;
                details.ConsigneeDetailAddress = string.Empty;
                details.ConsigneeZipCode = string.Empty;
            }

            return details;
        }
    }
}
