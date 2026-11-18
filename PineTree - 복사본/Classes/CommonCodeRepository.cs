using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public static class CommonCodeRepository
    {
        public static int DeleteItems(IEnumerable<long> seqNos)
        {
            if (seqNos == null) throw new ArgumentNullException("seqNos");
            HashSet<long> uniqueIds = new HashSet<long>();
            foreach (long seqNo in seqNos)
            {
                if (seqNo <= 0) throw new ArgumentOutOfRangeException("seqNos");
                uniqueIds.Add(seqNo);
            }
            if (uniqueIds.Count == 0) return 0;

            List<long> ids = new List<long>(uniqueIds);
            ids.Sort();
            List<string> names = new List<string>();
            List<ParamValuePair> parameters = new List<ParamValuePair>();
            for (int index = 0; index < ids.Count; index++)
            {
                string name = "@SeqNo" + index;
                names.Add(name);
                parameters.Add(new ParamValuePair(name, ids[index]));
            }
            return DB.Execute("DELETE FROM CommonCode WHERE SeqNo IN (" + string.Join(", ", names) + ")",
                parameters.ToArray());
        }

        public static DataRow GetCommonCode(long seqNo)
        {
            DataTable table = DB.MySqlQueryRead(
                "SELECT SeqNo, GroupName, ItemName FROM CommonCode WHERE SeqNo = @SeqNo",
                new[] { new ParamValuePair("@SeqNo", seqNo) });
            return table.Rows.Count == 0 ? null : table.Rows[0];
        }

        public static void ValidateItem(string groupName, string itemName)
        {
            if (string.IsNullOrWhiteSpace(groupName) || groupName == "[전체]")
                throw new ArgumentException("항목을 추가할 그룹을 선택해주세요.");
            if (string.IsNullOrWhiteSpace(itemName))
                throw new ArgumentException("항목이름을 입력해주세요.");
            if (groupName.Length > 50 || itemName.Trim().Length > 50)
                throw new ArgumentException("그룹이름과 항목이름은 각각 50자까지 입력할 수 있습니다.");
        }

        public static long SaveItem(long? seqNo, string groupName, string itemName)
        {
            ValidateItem(groupName, itemName);
            using (MySqlConnection connection = DB.CreateConnection())
            {
                connection.Open();
                string sql = seqNo.HasValue
                    ? "UPDATE CommonCode SET ItemName = @ItemName WHERE SeqNo = @SeqNo AND GroupName = @GroupName"
                    : "INSERT INTO CommonCode (GroupName, ItemName) VALUES (@GroupName, @ItemName)";
                using (MySqlCommand command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@GroupName", groupName);
                    command.Parameters.AddWithValue("@ItemName", itemName.Trim());
                    if (seqNo.HasValue)
                        command.Parameters.AddWithValue("@SeqNo", seqNo.Value);
                    int affected = command.ExecuteNonQuery();
                    if (seqNo.HasValue && affected == 0)
                    {
                        DataRow existing = GetCommonCode(seqNo.Value);
                        if (existing == null || Convert.ToString(existing["GroupName"]) != groupName
                            || Convert.ToString(existing["ItemName"]) != itemName.Trim())
                            throw new InvalidOperationException("수정할 항목이 삭제되었거나 그룹이 변경되었습니다. 다시 조회해주세요.");
                    }
                    return seqNo ?? command.LastInsertedId;
                }
            }
        }

        public static DataTable GetGroupNames()
        {
            const string sql = @"
SELECT GroupName
FROM CommonCode
WHERE GroupName IS NOT NULL AND GroupName <> ''
GROUP BY GroupName
ORDER BY GroupName";

            return DB.MySqlQueryRead(sql, null);
        }

        public static DataTable GetCommonCodes(string groupName)
        {
            string sql = "SELECT * FROM CommonCode";
            List<ParamValuePair> parameters = new List<ParamValuePair>();
            if (groupName != null)
            {
                sql += " WHERE GroupName = @GroupName";
                parameters.Add(new ParamValuePair("@GroupName", groupName));
            }

            sql += " ORDER BY GroupName, SeqNo";
            return DB.MySqlQueryRead(sql, parameters.ToArray());
        }

        public static DataTable GetOrderEntryItems()
        {
            const string sql = @"
SELECT SeqNo, GroupName, ItemName
FROM CommonCode
WHERE GroupName IN (@ChannelGroup, @DeliveryGroup)
AND ItemName IS NOT NULL
AND ItemName <> ''
ORDER BY SeqNo";

            return DB.MySqlQueryRead(sql, new[]
            {
                new ParamValuePair("@ChannelGroup", "주문채널"),
                new ParamValuePair("@DeliveryGroup", "배송방법")
            });
        }

        public static DataTable GetItems(string groupName)
        {
            string sql = @"
SELECT SeqNo, GroupName, ItemName
FROM CommonCode
WHERE GroupName = @GroupName
AND ItemName IS NOT NULL
AND ItemName <> ''
ORDER BY SeqNo";

            List<ParamValuePair> parameters = new List<ParamValuePair>
            {
                new ParamValuePair("@GroupName", groupName)
            };

            return DB.MySqlQueryRead(sql, parameters.ToArray());
        }
    }
}
