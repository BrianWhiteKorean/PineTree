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
SELECT PriceCategory
FROM PriceItems
WHERE IsActive = 1
GROUP BY PriceCategory
ORDER BY PriceCategory";

            return DB.MySqlQueryRead(sql, null);
        }

        public static DataTable GetCategoryNames(string priceCategory)
        {
            StringBuilder sql = new StringBuilder();
            List<ParamValuePair> parameters = new List<ParamValuePair>();

            sql.AppendLine("SELECT CategoryName");
            sql.AppendLine("FROM PriceItems");
            sql.AppendLine("WHERE IsActive = 1");
            sql.AppendLine("AND CategoryName IS NOT NULL");
            sql.AppendLine("AND CategoryName <> ''");

            if (!string.IsNullOrWhiteSpace(priceCategory))
            {
                sql.AppendLine("AND PriceCategory = @PriceCategory");
                parameters.Add(new ParamValuePair("@PriceCategory", priceCategory.Trim()));
            }

            sql.AppendLine("GROUP BY CategoryName");
            sql.AppendLine("ORDER BY CategoryName");

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
            sql.AppendLine("    pi.MatchedItemCode,");
            sql.AppendLine("    pi.SpecText,");
            sql.AppendLine("    price.UnitPrice AS CurrentUnitPrice,");
            sql.AppendLine("    price.ApplyFrom,");
            sql.AppendLine("    price.Remark");
            sql.AppendLine("FROM PriceItems pi");
            sql.AppendLine("LEFT JOIN PriceItemPrices price");
            sql.AppendLine("    ON price.PriceItemSeqNo = pi.SeqNo");
            sql.AppendLine("    AND price.ApplyFrom = (");
            sql.AppendLine("        SELECT MAX(p2.ApplyFrom)");
            sql.AppendLine("        FROM PriceItemPrices p2");
            sql.AppendLine("        WHERE p2.PriceItemSeqNo = pi.SeqNo");
            sql.AppendLine("        AND p2.ApplyFrom <= CURDATE()");
            sql.AppendLine("        AND (p2.EffectiveTo IS NULL OR p2.EffectiveTo >= CURDATE())");
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

            sql.AppendLine("ORDER BY pi.PriceCategory, pi.CategoryName, pi.PriceItemName");

            return DB.MySqlQueryRead(sql.ToString(), parameters.ToArray());
        }
    }
}
