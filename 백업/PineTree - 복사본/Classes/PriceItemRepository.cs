using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace PineTree.Classes
{
    public static class PriceItemRepository
    {
        public static event EventHandler DedicatedPriceItemSaved;

        public class DedicatedPriceItemSaveResult
        {
            public long PriceItemSeqNo { get; set; }
            public long PriceItemPriceId { get; set; }
            public string ItemCode { get; set; }
        }

        public static DataTable GetPriceCategories()
        {
            string sql = @"
SELECT pi.PriceCategory
FROM PriceItems pi
LEFT JOIN ProductCategory pc
    ON pc.Category = pi.PriceCategory
    AND pc.ParentName IS NULL
WHERE pi.IsActive = 1
AND pi.PriceCategory IS NOT NULL
AND pi.PriceCategory <> ''
GROUP BY pi.PriceCategory, pc.SortOrder
ORDER BY pc.SortOrder IS NULL, CAST(NULLIF(pc.SortOrder, '') AS UNSIGNED), pi.PriceCategory";

            return DB.MySqlQueryRead(sql, null);
        }

        public static DataTable GetCategoryNames(string priceCategory)
        {
            StringBuilder sql = new StringBuilder();
            List<ParamValuePair> parameters = new List<ParamValuePair>();

            sql.AppendLine("SELECT pi.CategoryName");
            sql.AppendLine("FROM PriceItems pi");
            sql.AppendLine("LEFT JOIN ProductCategory pc");
            sql.AppendLine("    ON pc.Category = pi.CategoryName");
            sql.AppendLine("    AND pc.ParentName = pi.PriceCategory");
            sql.AppendLine("WHERE pi.IsActive = 1");
            sql.AppendLine("AND pi.CategoryName IS NOT NULL");
            sql.AppendLine("AND pi.CategoryName <> ''");

            if (!string.IsNullOrWhiteSpace(priceCategory))
            {
                sql.AppendLine("AND pi.PriceCategory = @PriceCategory");
                parameters.Add(new ParamValuePair("@PriceCategory", priceCategory.Trim()));
            }

            sql.AppendLine("GROUP BY pi.CategoryName, pc.SortOrder");
            sql.AppendLine("ORDER BY pc.SortOrder IS NULL, CAST(NULLIF(pc.SortOrder, '') AS UNSIGNED), pi.CategoryName");

            return DB.MySqlQueryRead(sql.ToString(), parameters.ToArray());
        }

        public static DataTable GetPriceItemCustomers()
        {
            string sql = @"
SELECT DISTINCT c.CustCode, c.CustName
FROM PriceItems pi
INNER JOIN Customers c ON c.CustCode = pi.CustCode
WHERE pi.CustCode IS NOT NULL
AND pi.CustCode <> ''
ORDER BY c.CustName, c.CustCode";

            return DB.MySqlQueryRead(sql, null);
        }

        public static DataTable GetPriceItemsForCustomer(string priceCategory, string categoryName, string priceItemName, string custCode)
        {
            return GetPriceItems(priceCategory, categoryName, priceItemName, custCode, true, string.IsNullOrWhiteSpace(custCode));
        }

        public static DataTable GetPriceItems(string priceCategory, string categoryName, string priceItemName)
        {
            return GetPriceItems(priceCategory, categoryName, priceItemName, null, false);
        }

        public static DataTable GetPriceItems(string priceCategory, string categoryName, string priceItemName, string custCode)
        {
            return GetPriceItems(priceCategory, categoryName, priceItemName, custCode, false);
        }

        public static DataTable GetPriceItems(string priceCategory, string categoryName, string priceItemName, string custCode, bool dedicatedOnly)
        {
            return GetPriceItems(priceCategory, categoryName, priceItemName, custCode, dedicatedOnly, false);
        }

        public static DataTable GetBasicPriceItems(string priceCategory, string categoryName, string priceItemName)
        {
            return GetPriceItems(priceCategory, categoryName, priceItemName, null, false, true);
        }

        public static DataTable GetBasicPriceItems()
        {
            return GetPriceItems(null, null, null, null, false, false, true);
        }

        private static DataTable GetPriceItems(string priceCategory, string categoryName, string priceItemName, string custCode, bool dedicatedOnly, bool basicOnly, bool nullCustomerOnly = false)
        {
            StringBuilder sql = new StringBuilder();
            List<ParamValuePair> parameters = new List<ParamValuePair>();

            sql.AppendLine("SELECT");
            if (nullCustomerOnly)
            {
                sql.AppendLine("    EXISTS (");
                sql.AppendLine("        SELECT 1 FROM PriceItemProcessRule mapping");
                sql.AppendLine("        INNER JOIN ProcessRule rule ON rule.RuleID = mapping.RuleID");
                sql.AppendLine("        WHERE mapping.PriceItemSeqNo = pi.SeqNo");
                sql.AppendLine("        AND mapping.IsActive = 1 AND rule.IsActive = 1");
                sql.AppendLine("    ) AS HasProcessRule,");
            }
            sql.AppendLine("    pi.SeqNo,");
            sql.AppendLine("    pi.CustCode,");
            sql.AppendLine("    c.CustName AS SpecialCustName,");
            sql.AppendLine("    pi.PriceCategory,");
            sql.AppendLine("    pi.CategoryName,");
            sql.AppendLine("    pi.PriceItemName,");
            sql.AppendLine("    pi.ItemCode,");
            sql.AppendLine("    pi.SpecText,");
            sql.AppendLine("    pi.UnitName,");
            sql.AppendLine("    pi.BillingBasis,");
            sql.AppendLine("    price.PriceItemPriceId,");
            sql.AppendLine("    price.PriceName,");
            sql.AppendLine("    price.UnitPrice AS CurrentUnitPrice,");
            sql.AppendLine("    price.ApplyFrom,");
            sql.AppendLine("    price.Remark");
            sql.AppendLine("FROM PriceItems pi");
            sql.AppendLine("LEFT JOIN ProductCategory pcParent");
            sql.AppendLine("    ON pcParent.Category = pi.PriceCategory");
            sql.AppendLine("    AND pcParent.ParentName IS NULL");
            sql.AppendLine("LEFT JOIN ProductCategory pcChild");
            sql.AppendLine("    ON pcChild.Category = pi.CategoryName");
            sql.AppendLine("    AND pcChild.ParentName = pi.PriceCategory");
            sql.AppendLine("LEFT JOIN Customers c");
            sql.AppendLine("    ON c.CustCode = pi.CustCode");
            sql.AppendLine("LEFT JOIN PriceItemPrices price");
            sql.AppendLine("    ON price.PriceItemPriceId = (");
            sql.AppendLine("        SELECT p2.PriceItemPriceId");
            sql.AppendLine("        FROM PriceItemPrices p2");
            sql.AppendLine("        WHERE p2.PriceItemSeqNo = pi.SeqNo");
            sql.AppendLine("        AND p2.ApplyFrom <= CURDATE()");
            sql.AppendLine("        AND (p2.EffectiveTo IS NULL OR p2.EffectiveTo >= CURDATE())");
            sql.AppendLine("        ORDER BY p2.ApplyFrom DESC, p2.PriceItemPriceId DESC");
            sql.AppendLine("        LIMIT 1");
            sql.AppendLine("    )");
            sql.AppendLine("WHERE pi.IsActive = 1");

            if (nullCustomerOnly)
            {
                sql.AppendLine("AND pi.CustCode IS NULL");
            }
            else if (basicOnly)
            {
                sql.AppendLine("AND (pi.CustCode IS NULL OR pi.CustCode = '')");
            }
            else if (!string.IsNullOrWhiteSpace(custCode) && dedicatedOnly)
            {
                sql.AppendLine("AND pi.CustCode = @CustCode");
                parameters.Add(new ParamValuePair("@CustCode", custCode.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(priceCategory))
            {
                sql.AppendLine("AND pi.PriceCategory = @PriceCategory");
                parameters.Add(new ParamValuePair("@PriceCategory", priceCategory.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                sql.AppendLine("AND pi.CategoryName = @CategoryName");
                parameters.Add(new ParamValuePair("@CategoryName", categoryName.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(priceItemName))
            {
                sql.AppendLine("AND pi.PriceItemName LIKE @PriceItemName");
                parameters.Add(new ParamValuePair("@PriceItemName", "%" + priceItemName.Trim() + "%"));
            }

            sql.AppendLine("ORDER BY");
            sql.AppendLine("    pcParent.SortOrder IS NULL,");
            sql.AppendLine("    CAST(NULLIF(pcParent.SortOrder, '') AS UNSIGNED),");
            sql.AppendLine("    pi.PriceCategory,");
            sql.AppendLine("    pcChild.SortOrder IS NULL,");
            sql.AppendLine("    CAST(NULLIF(pcChild.SortOrder, '') AS UNSIGNED),");
            sql.AppendLine("    pi.CategoryName,");
            sql.AppendLine("    pi.CustCode IS NULL,");
            sql.AppendLine("    c.CustName,");
            sql.AppendLine("    pi.ItemCode,");
            sql.AppendLine("    pi.PriceItemName");

            return DB.MySqlQueryRead(sql.ToString(), parameters.ToArray());
        }

        public static DataTable GetPriceNames()
        {
            string sql = @"
SELECT
    PriceName,
    MAX(ApplyFrom) AS LastApplyFrom
FROM PriceItemPrices
WHERE PriceName IS NOT NULL
AND PriceName <> ''
GROUP BY PriceName
ORDER BY LastApplyFrom DESC, PriceName";

            return DB.MySqlQueryRead(sql, null);
        }

        public static DataRow GetLatestPriceByPriceName(string priceName)
        {
            string sql = @"
SELECT
    PriceName,
    UnitPrice,
    ApplyFrom,
    EffectiveTo,
    Remark
FROM PriceItemPrices
WHERE PriceName = @PriceName
ORDER BY ApplyFrom DESC, PriceItemPriceId DESC
LIMIT 1";

            DataTable dt = DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@PriceName", priceName)
            });

            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataRow GetPriceItem(long seqNo)
        {
            string sql = @"
SELECT
    pi.SeqNo,
    pi.PriceCategory,
    pi.CategoryName,
    pi.PriceItemName,
    pi.ItemCode,
    pi.SpecText,
    pi.UnitName,
    pi.BillingBasis,
    pi.DefaultWidth,
    pi.DefaultHeight,
    pi.IsActive,
    price.PriceName,
    price.UnitPrice,
    price.ApplyFrom,
    price.Remark
FROM PriceItems pi
LEFT JOIN PriceItemPrices price
    ON price.PriceItemPriceId = (
        SELECT p2.PriceItemPriceId
        FROM PriceItemPrices p2
        WHERE p2.PriceItemSeqNo = pi.SeqNo
        ORDER BY p2.ApplyFrom DESC, p2.PriceItemPriceId DESC
        LIMIT 1
    )
WHERE pi.SeqNo = @SeqNo";

            DataTable dt = DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@SeqNo", seqNo)
            });

            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static string GetNextItemCode(int year)
        {
            string prefix = year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + "-";
            string sql = @"
SELECT COALESCE(MAX(CAST(RIGHT(ItemCode, 4) AS UNSIGNED)), 0)
FROM PriceItems
WHERE CHAR_LENGTH(ItemCode) = 9
AND ItemCode REGEXP @ItemCodePattern";

            object value = DB.Scalar(sql, new[]
            {
                new ParamValuePair("@ItemCodePattern", "^" + prefix + "[0-9]{4}$")
            });

            int maxNumber = Convert.ToInt32(value);
            if (maxNumber >= 9999)
            {
                throw new InvalidOperationException("올해의 품목코드 일련번호가 9999에 도달하여 자동할당할 수 없습니다.");
            }

            return prefix + (maxNumber + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool ExistsItemCode(string itemCode, long? exceptSeqNo)
        {
            string sql = @"
SELECT COUNT(*)
FROM PriceItems
WHERE ItemCode = @ItemCode";

            List<ParamValuePair> parameters = new List<ParamValuePair>
            {
                new ParamValuePair("@ItemCode", itemCode)
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

        public static long InsertPriceItem(
            string priceCategory,
            string categoryName,
            string priceItemName,
            string itemCode,
            string specText,
            string unitName,
            string billingBasis,
            decimal? defaultWidth,
            decimal? defaultHeight,
            bool isActive)
        {
            string sql = @"
INSERT INTO PriceItems
(
    PriceCategory,
    CategoryName,
    PriceItemName,
    NormalizedPriceItemName,
    ItemCode,
    SpecText,
    UnitName,
    BillingBasis,
    DefaultWidth,
    DefaultHeight,
    DefaultAreaPerQty,
    IsActive
)
VALUES
(
    @PriceCategory,
    @CategoryName,
    @PriceItemName,
    @NormalizedPriceItemName,
    @ItemCode,
    @SpecText,
    @UnitName,
    @BillingBasis,
    @DefaultWidth,
    @DefaultHeight,
    @DefaultAreaPerQty,
    @IsActive
);
SELECT LAST_INSERT_ID();";

            object value = DB.Scalar(sql, new[]
            {
                new ParamValuePair("@PriceCategory", priceCategory.Trim()),
                new ParamValuePair("@CategoryName", EmptyToNull(categoryName)),
                new ParamValuePair("@PriceItemName", priceItemName.Trim()),
                new ParamValuePair("@NormalizedPriceItemName", NormalizePriceItemName(priceItemName)),
                new ParamValuePair("@ItemCode", EmptyToNull(itemCode)),
                new ParamValuePair("@SpecText", EmptyToNull(specText)),
                new ParamValuePair("@UnitName", EmptyToNull(unitName)),
                new ParamValuePair("@BillingBasis", string.IsNullOrWhiteSpace(billingBasis) ? "AREA" : billingBasis.Trim()),
                new ParamValuePair("@DefaultWidth", defaultWidth),
                new ParamValuePair("@DefaultHeight", defaultHeight),
                new ParamValuePair("@DefaultAreaPerQty", GetDefaultAreaPerQty(defaultWidth, defaultHeight)),
                new ParamValuePair("@IsActive", isActive ? 1 : 0)
            });

            return Convert.ToInt64(value);
        }

        public static void UpdatePriceItem(
            long seqNo,
            string priceCategory,
            string categoryName,
            string priceItemName,
            string itemCode,
            string specText,
            string unitName,
            string billingBasis,
            decimal? defaultWidth,
            decimal? defaultHeight,
            bool isActive)
        {
            string sql = @"
UPDATE PriceItems
SET
    PriceCategory = @PriceCategory,
    CategoryName = @CategoryName,
    PriceItemName = @PriceItemName,
    NormalizedPriceItemName = @NormalizedPriceItemName,
    ItemCode = @ItemCode,
    SpecText = @SpecText,
    UnitName = @UnitName,
    BillingBasis = @BillingBasis,
    DefaultWidth = @DefaultWidth,
    DefaultHeight = @DefaultHeight,
    DefaultAreaPerQty = @DefaultAreaPerQty,
    IsActive = @IsActive
WHERE SeqNo = @SeqNo";

            DB.Execute(sql, new[]
            {
                new ParamValuePair("@SeqNo", seqNo),
                new ParamValuePair("@PriceCategory", priceCategory.Trim()),
                new ParamValuePair("@CategoryName", EmptyToNull(categoryName)),
                new ParamValuePair("@PriceItemName", priceItemName.Trim()),
                new ParamValuePair("@NormalizedPriceItemName", NormalizePriceItemName(priceItemName)),
                new ParamValuePair("@ItemCode", EmptyToNull(itemCode)),
                new ParamValuePair("@SpecText", EmptyToNull(specText)),
                new ParamValuePair("@UnitName", EmptyToNull(unitName)),
                new ParamValuePair("@BillingBasis", string.IsNullOrWhiteSpace(billingBasis) ? "AREA" : billingBasis.Trim()),
                new ParamValuePair("@DefaultWidth", defaultWidth),
                new ParamValuePair("@DefaultHeight", defaultHeight),
                new ParamValuePair("@DefaultAreaPerQty", GetDefaultAreaPerQty(defaultWidth, defaultHeight)),
                new ParamValuePair("@IsActive", isActive ? 1 : 0)
            });
        }

        public static void InsertPriceItemPrice(long priceItemSeqNo, string priceName, decimal unitPrice, DateTime applyFrom, string remark)
        {
            string sql = @"
INSERT INTO PriceItemPrices
(
    PriceItemSeqNo,
    UnitPrice,
    Remark,
    ApplyFrom,
    PriceName,
    SourceRowNo
)
VALUES
(
    @PriceItemSeqNo,
    @UnitPrice,
    @Remark,
    @ApplyFrom,
    @PriceName,
    0
)
ON DUPLICATE KEY UPDATE
    UnitPrice = @UnitPrice,
    Remark = @Remark,
    PriceName = @PriceName";

            DB.Execute(sql, new[]
            {
                new ParamValuePair("@PriceItemSeqNo", priceItemSeqNo),
                new ParamValuePair("@UnitPrice", unitPrice),
                new ParamValuePair("@Remark", EmptyToNull(remark)),
                new ParamValuePair("@ApplyFrom", applyFrom.Date),
                new ParamValuePair("@PriceName", priceName.Trim())
            });
        }

        public static DedicatedPriceItemSaveResult InsertDedicatedPriceItem(
            string custCode,
            string priceCategory,
            string categoryName,
            string priceItemName,
            string baseItemCode,
            string specText,
            string unitName,
            string billingBasis,
            decimal? defaultWidth,
            decimal? defaultHeight,
            string priceName,
            decimal unitPrice,
            DateTime applyFrom,
            string remark)
        {
            DedicatedPriceItemSaveResult result;
            using (MySqlConnection conn = DB.CreateConnection())
            {
                conn.Open();
                using (MySqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string dedicatedItemCode = CreateDedicatedItemCode(conn, transaction, baseItemCode, custCode);
                        long priceItemSeqNo = InsertDedicatedPriceItem(conn, transaction, custCode, priceCategory, categoryName, priceItemName, dedicatedItemCode, specText, unitName, billingBasis, defaultWidth, defaultHeight);
                        long priceItemPriceId = InsertDedicatedPriceItemPrice(conn, transaction, priceItemSeqNo, priceName, unitPrice, applyFrom, remark);
                        transaction.Commit();

                        result = new DedicatedPriceItemSaveResult
                        {
                            PriceItemSeqNo = priceItemSeqNo,
                            PriceItemPriceId = priceItemPriceId,
                            ItemCode = dedicatedItemCode
                        };
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // 저장이 확정되고 연결이 해제된 뒤에 화면에 변경을 알립니다.
            DedicatedPriceItemSaved?.Invoke(null, EventArgs.Empty);
            return result;
        }

        private static long InsertDedicatedPriceItem(
            MySqlConnection conn,
            MySqlTransaction transaction,
            string custCode,
            string priceCategory,
            string categoryName,
            string priceItemName,
            string itemCode,
            string specText,
            string unitName,
            string billingBasis,
            decimal? defaultWidth,
            decimal? defaultHeight)
        {
            string sql = @"
INSERT INTO PriceItems
(
    CustCode,
    PriceCategory,
    CategoryName,
    PriceItemName,
    NormalizedPriceItemName,
    ItemCode,
    SpecText,
    UnitName,
    BillingBasis,
    DefaultWidth,
    DefaultHeight,
    DefaultAreaPerQty,
    IsActive
)
VALUES
(
    @CustCode,
    @PriceCategory,
    @CategoryName,
    @PriceItemName,
    @NormalizedPriceItemName,
    @ItemCode,
    @SpecText,
    @UnitName,
    @BillingBasis,
    @DefaultWidth,
    @DefaultHeight,
    @DefaultAreaPerQty,
    1
);
SELECT LAST_INSERT_ID();";

            object value = Scalar(conn, transaction, sql, new[]
            {
                new ParamValuePair("@CustCode", custCode),
                new ParamValuePair("@PriceCategory", priceCategory.Trim()),
                new ParamValuePair("@CategoryName", EmptyToNull(categoryName)),
                new ParamValuePair("@PriceItemName", priceItemName.Trim()),
                new ParamValuePair("@NormalizedPriceItemName", NormalizePriceItemName(priceItemName)),
                new ParamValuePair("@ItemCode", EmptyToNull(itemCode)),
                new ParamValuePair("@SpecText", EmptyToNull(specText)),
                new ParamValuePair("@UnitName", EmptyToNull(unitName)),
                new ParamValuePair("@BillingBasis", string.IsNullOrWhiteSpace(billingBasis) ? "AREA" : billingBasis.Trim()),
                new ParamValuePair("@DefaultWidth", defaultWidth),
                new ParamValuePair("@DefaultHeight", defaultHeight),
                new ParamValuePair("@DefaultAreaPerQty", GetDefaultAreaPerQty(defaultWidth, defaultHeight))
            });

            return Convert.ToInt64(value);
        }

        private static long InsertDedicatedPriceItemPrice(
            MySqlConnection conn,
            MySqlTransaction transaction,
            long priceItemSeqNo,
            string priceName,
            decimal unitPrice,
            DateTime applyFrom,
            string remark)
        {
            string sql = @"
INSERT INTO PriceItemPrices
(
    PriceItemSeqNo,
    UnitPrice,
    Remark,
    ApplyFrom,
    PriceName,
    SourceRowNo
)
VALUES
(
    @PriceItemSeqNo,
    @UnitPrice,
    @Remark,
    @ApplyFrom,
    @PriceName,
    0
);
SELECT LAST_INSERT_ID();";

            object value = Scalar(conn, transaction, sql, new[]
            {
                new ParamValuePair("@PriceItemSeqNo", priceItemSeqNo),
                new ParamValuePair("@UnitPrice", unitPrice),
                new ParamValuePair("@Remark", EmptyToNull(remark)),
                new ParamValuePair("@ApplyFrom", applyFrom.Date),
                new ParamValuePair("@PriceName", string.IsNullOrWhiteSpace(priceName) ? "거래처 전용 단가표" : priceName.Trim())
            });

            return Convert.ToInt64(value);
        }

        private static string CreateDedicatedItemCode(MySqlConnection conn, MySqlTransaction transaction, string baseItemCode, string custCode)
        {
            string baseCode = string.IsNullOrWhiteSpace(baseItemCode) ? "ITEM" : baseItemCode.Trim();
            string normalizedCustCode = string.IsNullOrWhiteSpace(custCode) ? "CUST" : custCode.Trim();
            string prefix = baseCode + "-" + normalizedCustCode;
            string candidate = TruncateItemCode(prefix);
            int suffix = 2;

            while (ItemCodeExists(conn, transaction, candidate))
            {
                string suffixText = "-" + suffix.ToString();
                candidate = TruncateItemCode(prefix, suffixText);
                suffix++;
            }

            return candidate;
        }

        private static string TruncateItemCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                return string.Empty;
            }

            return itemCode.Length <= 50 ? itemCode : itemCode.Substring(0, 50);
        }

        private static string TruncateItemCode(string itemCode, string suffix)
        {
            string value = itemCode ?? string.Empty;
            string tail = suffix ?? string.Empty;
            int maxPrefixLength = Math.Max(0, 50 - tail.Length);

            if (value.Length > maxPrefixLength)
            {
                value = value.Substring(0, maxPrefixLength);
            }

            return value + tail;
        }

        private static bool ItemCodeExists(MySqlConnection conn, MySqlTransaction transaction, string itemCode)
        {
            object value = Scalar(conn, transaction, @"
SELECT COUNT(*)
FROM PriceItems
WHERE ItemCode = @ItemCode", new[]
            {
                new ParamValuePair("@ItemCode", itemCode)
            });

            int count;
            return value != null && value != DBNull.Value && int.TryParse(value.ToString(), out count) && count > 0;
        }

        private static object Scalar(MySqlConnection conn, MySqlTransaction transaction, string sql, ParamValuePair[] parameters)
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn, transaction))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        private static void AddParameters(MySqlCommand cmd, ParamValuePair[] parameters)
        {
            foreach (ParamValuePair parameter in parameters)
            {
                cmd.Parameters.AddWithValue(parameter.ParameterName, parameter.Value ?? DBNull.Value);
            }
        }

        private static object EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizePriceItemName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static object GetDefaultAreaPerQty(decimal? defaultWidth, decimal? defaultHeight)
        {
            if (!defaultWidth.HasValue || !defaultHeight.HasValue || defaultWidth.Value <= 0 || defaultHeight.Value <= 0)
            {
                return null;
            }

            return decimal.Round(defaultWidth.Value * defaultHeight.Value / 1000000M, 4);
        }
    }
}
