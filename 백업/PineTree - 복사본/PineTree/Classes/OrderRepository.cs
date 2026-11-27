using System;
using System.Collections.Generic;
using System.Data;

namespace PineTree.Classes
{
    public static class OrderRepository
    {
        public static DataTable GetOrdersByOrderDate(DateTime orderDate)
        {
            string sql = @"
SELECT
    oi.OrderId,
    oi.OrderDate,
    oi.ReceivedAt,
    oi.OrderNo,
    oi.CustCode,
    c.CustomerName,
    oi.OrderTitle,
    oi.ProductionStatus,
    oi.RequestedDueAt,
    oi.SourceChannel
FROM OrderInfo oi
LEFT JOIN Customers c
    ON c.CustCode = oi.CustCode
WHERE oi.OrderDate = @OrderDate
ORDER BY oi.ReceivedAt, oi.OrderId";

            return DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@OrderDate", orderDate.Date)
            });
        }

        public static Dictionary<OrderProductionStatus, int> GetStatusCountsByOrderDate(DateTime orderDate)
        {
            string sql = @"
SELECT
    ProductionStatus,
    COUNT(*) AS StatusCount
FROM OrderInfo
WHERE OrderDate = @OrderDate
GROUP BY ProductionStatus";

            DataTable dt = DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@OrderDate", orderDate.Date)
            });

            Dictionary<OrderProductionStatus, int> counts = CreateEmptyStatusCounts();
            foreach (DataRow row in dt.Rows)
            {
                OrderProductionStatus status;
                if (!OrderProductionStatusHelper.TryParse(row["ProductionStatus"].ToString(), out status))
                {
                    continue;
                }

                int count;
                if (!int.TryParse(row["StatusCount"].ToString(), out count))
                {
                    count = 0;
                }

                counts[status] = count;
            }

            return counts;
        }

        private static Dictionary<OrderProductionStatus, int> CreateEmptyStatusCounts()
        {
            Dictionary<OrderProductionStatus, int> counts = new Dictionary<OrderProductionStatus, int>();
            counts[OrderProductionStatus.New] = 0;
            counts[OrderProductionStatus.Design] = 0;
            counts[OrderProductionStatus.Printing] = 0;
            counts[OrderProductionStatus.PostProcess] = 0;
            counts[OrderProductionStatus.PackingWaiting] = 0;
            counts[OrderProductionStatus.PackingComplete] = 0;
            counts[OrderProductionStatus.DeliveryComplete] = 0;
            counts[OrderProductionStatus.SettlementComplete] = 0;

            return counts;
        }
    }
}
