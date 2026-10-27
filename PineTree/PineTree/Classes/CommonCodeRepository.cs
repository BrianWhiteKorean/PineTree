using System.Collections.Generic;
using System.Data;

namespace PineTree.Classes
{
    public static class CommonCodeRepository
    {
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
