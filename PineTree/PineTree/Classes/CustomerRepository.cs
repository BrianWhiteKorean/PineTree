using System.Collections.Generic;
using System.Data;
using System.Text;
using System;

namespace PineTree.Classes
{
    public static class CustomerRepository
    {
        public static DataTable GetCustomers(string customerName, string customerCode, DateTime? lastOrderDateFrom, DateTime? lastOrderDateTo)
        {
            StringBuilder sql = new StringBuilder();
            List<ParamValuePair> parameters = new List<ParamValuePair>();

            sql.AppendLine("SELECT");
            sql.AppendLine("    c.SeqNo,");
            sql.AppendLine("    c.CustomerName,");
            sql.AppendLine("    c.CustCode,");
            sql.AppendLine("    c.ECountCustomerCode,");
            sql.AppendLine("    c.Phone,");
            sql.AppendLine("    c.BusinessNum,");
            sql.AppendLine("    c.RepresentativeName,");
            sql.AppendLine("    lastOrder.LastOrderDate");
            sql.AppendLine("FROM Customers c");
            sql.AppendLine("LEFT JOIN (");
            sql.AppendLine("    SELECT CustCode, MAX(OrderDate) AS LastOrderDate");
            sql.AppendLine("    FROM OrderInfo");
            sql.AppendLine("    GROUP BY CustCode");
            sql.AppendLine(") lastOrder ON lastOrder.CustCode = c.CustCode");
            sql.AppendLine("WHERE 1 = 1");

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                sql.AppendLine("AND c.CustomerName LIKE @CustomerName");
                parameters.Add(new ParamValuePair("@CustomerName", "%" + customerName.Trim() + "%"));
            }

            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                sql.AppendLine("AND CAST(c.CustCode AS CHAR) LIKE @CustomerCode");
                parameters.Add(new ParamValuePair("@CustomerCode", "%" + customerCode.Trim() + "%"));
            }

            if (lastOrderDateFrom.HasValue)
            {
                sql.AppendLine("AND lastOrder.LastOrderDate >= @LastOrderDateFrom");
                parameters.Add(new ParamValuePair("@LastOrderDateFrom", lastOrderDateFrom.Value.Date));
            }

            if (lastOrderDateTo.HasValue)
            {
                sql.AppendLine("AND lastOrder.LastOrderDate <= @LastOrderDateTo");
                parameters.Add(new ParamValuePair("@LastOrderDateTo", lastOrderDateTo.Value.Date));
            }

            sql.AppendLine("ORDER BY c.CustomerName");

            return DB.MySqlQueryRead(sql.ToString(), parameters.ToArray());
        }

        public static DataRow GetCustomer(long seqNo)
        {
            string sql = @"
SELECT
    SeqNo,
    CustCode,
    ECountCustomerCode,
    CustomerName,
    RepresentativeName,
    Phone,
    BusinessNum,
    IsActive
FROM Customers
WHERE SeqNo = @SeqNo";

            DataTable dt = DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@SeqNo", seqNo)
            });

            if (dt.Rows.Count == 0)
            {
                return null;
            }

            return dt.Rows[0];
        }

        public static bool ExistsCustCode(long custCode, long? exceptSeqNo)
        {
            string sql = @"
SELECT COUNT(*)
FROM Customers
WHERE CustCode = @CustCode";

            List<ParamValuePair> parameters = new List<ParamValuePair>
            {
                new ParamValuePair("@CustCode", custCode)
            };

            if (exceptSeqNo.HasValue)
            {
                sql += " AND SeqNo <> @SeqNo";
                parameters.Add(new ParamValuePair("@SeqNo", exceptSeqNo.Value));
            }

            object value = DB.Scalar(sql, parameters.ToArray());
            int count;
            if (value == null || value == DBNull.Value || !int.TryParse(value.ToString(), out count))
            {
                return false;
            }

            return count > 0;
        }

        public static void InsertCustomer(long custCode, string eCountCustomerCode, string customerName, string representativeName, string phone, string businessNum)
        {
            string sql = @"
INSERT INTO Customers
(
    CustCode,
    ECountCustomerCode,
    CustomerName,
    RepresentativeName,
    Phone,
    BusinessNum,
    NormalizedCustomerName,
    IsActive
)
VALUES
(
    @CustCode,
    @ECountCustomerCode,
    @CustomerName,
    @RepresentativeName,
    @Phone,
    @BusinessNum,
    @NormalizedCustomerName,
    1
)";

            DB.Execute(sql, new[]
            {
                new ParamValuePair("@CustCode", custCode),
                new ParamValuePair("@ECountCustomerCode", EmptyToNull(eCountCustomerCode)),
                new ParamValuePair("@CustomerName", customerName),
                new ParamValuePair("@RepresentativeName", EmptyToNull(representativeName)),
                new ParamValuePair("@Phone", EmptyToNull(phone)),
                new ParamValuePair("@BusinessNum", EmptyToNull(businessNum)),
                new ParamValuePair("@NormalizedCustomerName", NormalizeCustomerName(customerName))
            });
        }

        public static void UpdateCustomer(long seqNo, long custCode, string eCountCustomerCode, string customerName, string representativeName, string phone, string businessNum)
        {
            string sql = @"
UPDATE Customers
SET
    CustCode = @CustCode,
    ECountCustomerCode = @ECountCustomerCode,
    CustomerName = @CustomerName,
    RepresentativeName = @RepresentativeName,
    Phone = @Phone,
    BusinessNum = @BusinessNum,
    NormalizedCustomerName = @NormalizedCustomerName
WHERE SeqNo = @SeqNo";

            DB.Execute(sql, new[]
            {
                new ParamValuePair("@SeqNo", seqNo),
                new ParamValuePair("@CustCode", custCode),
                new ParamValuePair("@ECountCustomerCode", EmptyToNull(eCountCustomerCode)),
                new ParamValuePair("@CustomerName", customerName),
                new ParamValuePair("@RepresentativeName", EmptyToNull(representativeName)),
                new ParamValuePair("@Phone", EmptyToNull(phone)),
                new ParamValuePair("@BusinessNum", EmptyToNull(businessNum)),
                new ParamValuePair("@NormalizedCustomerName", NormalizeCustomerName(customerName))
            });
        }

        private static object EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeCustomerName(string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
            {
                return string.Empty;
            }

            return customerName.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        }
    }
}
