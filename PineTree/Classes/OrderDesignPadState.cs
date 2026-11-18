using System;
using System.Data;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public sealed class OrderDesignPadState
    {
        public string EquipmentName { get; set; }
        public string PriceCategory { get; set; }
        public string CategoryName { get; set; }
        public string ProductName { get; set; }
        public string SpecText { get; set; }
        public decimal? UnitPrice { get; set; }
        public BannerOrderEditorState Editor { get; set; }
        public static string FormatSpecification(decimal width, decimal height, decimal quantity)
        {
            if (width <= 0 || height <= 0) return string.Empty;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.####}*{1:0.####}_{2:0}장",
                width, height, Math.Max(0m, Math.Round(quantity, 0, MidpointRounding.AwayFromZero)));
        }
        public string ToJson() { return new JavaScriptSerializer().Serialize(this); }
        public static OrderDesignPadState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var state = new JavaScriptSerializer().Deserialize<OrderDesignPadState>(json);
            if (state != null && state.Editor != null) BannerOrderEditorState.FromJson(state.Editor.ToJson());
            return state;
        }
    }

    public static class OrderDesignPadStateRepository
    {
        public static string Get(long orderId)
        {
            using (DataTable rows = DB.MySqlQueryRead("SELECT StateJSON FROM OrderDesignPadStates WHERE OrderId = @OrderId",
                new[] { new ParamValuePair("@OrderId", orderId) }))
                return rows.Rows.Count == 0 ? null : Convert.ToString(rows.Rows[0][0]);
        }

        internal static void Save(MySqlConnection conn, MySqlTransaction transaction, long orderId, string json)
        {
            // Other order editors must preserve snapshots they do not understand.
            if (string.IsNullOrWhiteSpace(json)) return;
            OrderDesignPadState.FromJson(json);
            using (var command = new MySqlCommand(@"INSERT INTO OrderDesignPadStates (OrderId, StateJSON)
VALUES (@OrderId, @State) ON DUPLICATE KEY UPDATE StateJSON = VALUES(StateJSON)", conn, transaction))
            {
                command.Parameters.AddWithValue("@OrderId", orderId);
                command.Parameters.AddWithValue("@State", json);
                command.ExecuteNonQuery();
            }
        }
    }
}
