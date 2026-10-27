using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace PineTree.Classes
{
    public static class PriceItemRepository
    {
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

        public static DataTable GetPriceItems(string priceCategory, string categoryName, string priceItemName)
        {
            StringBuilder sql = new StringBuilder();
            List<ParamValuePair> parameters = new List<ParamValuePair>();

            sql.AppendLine("SELECT");
            sql.AppendLine("    pi.SeqNo,");
            sql.AppendLine("    pi.PriceCategory,");
            sql.AppendLine("    pi.CategoryName,");
            sql.AppendLine("    pi.PriceItemName,");
            sql.AppendLine("    pi.ItemCode,");
            sql.AppendLine("    pi.SpecText,");
            sql.AppendLine("    pi.UnitName,");
            sql.AppendLine("    pi.BillingBasis,");
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
