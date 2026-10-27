using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public static class ProcessRuleRepository
    {
        public static DataTable GetRuleList(string category, string keyword)
        {
            const string sql = @"
SELECT RuleID, Category, ProcessName, WorkName, EditDescription
FROM ProcessRule
WHERE IsActive = 1 AND (@Category = '' OR Category = @Category)
    AND (@Keyword = '' OR ProcessName LIKE @Search OR WorkName LIKE @Search OR EditDescription LIKE @Search)
ORDER BY Category, ProcessName, RuleID";
            string search = (keyword ?? string.Empty).Trim();
            return DB.MySqlQueryRead(sql, new[] {
                new ParamValuePair("@Category", (category ?? string.Empty).Trim()),
                new ParamValuePair("@Keyword", search),
                new ParamValuePair("@Search", "%" + search + "%") });
        }

        public static DataTable GetRuleCategories()
        {
            return DB.MySqlQueryRead(@"
SELECT DISTINCT Category FROM ProcessRule
WHERE IsActive = 1 AND Category IS NOT NULL AND TRIM(Category) <> ''
ORDER BY Category", new ParamValuePair[0]);
        }

        public static List<ProcessRuleData> GetRuleSteps(long ruleID)
        {
            DataTable table = DB.MySqlQueryRead(@"
SELECT r.RuleID, r.Category, r.ProcessName, r.WorkName, r.EditDescription,
       s.RuleStepID, s.ActionCode, s.ParamsJSON
FROM ProcessRule r
INNER JOIN ProcessRuleStep s ON s.RuleID = r.RuleID AND s.IsActive = 1
WHERE r.RuleID = @Rule AND r.IsActive = 1
ORDER BY s.StepSeq, s.RuleStepID", new[] { new ParamValuePair("@Rule", ruleID) });
            var steps = new List<ProcessRuleData>();
            foreach (DataRow row in table.Rows)
            {
                steps.Add(new ProcessRuleData {
                    RuleID = Convert.ToInt64(row["RuleID"]),
                    Category = Convert.ToString(row["Category"]),
                    ProcessName = Convert.ToString(row["ProcessName"]),
                    WorkName = Convert.ToString(row["WorkName"]),
                    EditDescription = Convert.ToString(row["EditDescription"]),
                    RuleStepID = Convert.ToInt64(row["RuleStepID"]),
                    ActionCode = Convert.ToString(row["ActionCode"]),
                    ParamsJSON = Convert.ToString(row["ParamsJSON"])
                });
            }
            return steps;
        }

        public static List<ProcessRuleData> GetRules(long priceItemSeqNo)
        {
            const string sql = @"
SELECT m.SeqNo AS MappingSeqNo, r.*,
    (SELECT s.RuleStepID FROM ProcessRuleStep s
     WHERE s.RuleID = r.RuleID AND s.IsActive = 1 ORDER BY s.StepSeq, s.RuleStepID LIMIT 1) AS RuleStepID,
    (SELECT s.ParamsJSON FROM ProcessRuleStep s
     WHERE s.RuleID = r.RuleID AND s.IsActive = 1 ORDER BY s.StepSeq, s.RuleStepID LIMIT 1) AS ParamsJSON,
    (SELECT s.ActionCode FROM ProcessRuleStep s
     WHERE s.RuleID = r.RuleID AND s.IsActive = 1 ORDER BY s.StepSeq, s.RuleStepID LIMIT 1) AS ActionCode
FROM PriceItemProcessRule m
INNER JOIN ProcessRule r ON r.RuleID = m.RuleID
WHERE m.PriceItemSeqNo = @PriceItemSeqNo AND m.IsActive = 1 AND r.IsActive = 1
ORDER BY m.SortOrder, m.SeqNo";
            DataTable table = DB.MySqlQueryRead(sql, new[] { new ParamValuePair("@PriceItemSeqNo", priceItemSeqNo) });
            List<ProcessRuleData> rules = new List<ProcessRuleData>();
            foreach (DataRow row in table.Rows)
            {
                var rule = new ProcessRuleData
                {
                    MappingSeqNo = Convert.ToInt64(row["MappingSeqNo"]),
                    RuleID = Convert.ToInt64(row["RuleID"]),
                    ProcessName = Convert.ToString(row["ProcessName"]),
                    Category = Convert.ToString(row["Category"]),
                    WorkName = Convert.ToString(row["WorkName"]),
                    EditDescription = Convert.ToString(row["EditDescription"]),
                    ActionCode = Convert.ToString(row["ActionCode"]),
                    ParamsJSON = row["ParamsJSON"] == DBNull.Value ? null : Convert.ToString(row["ParamsJSON"]),
                    RuleStepID = row["RuleStepID"] == DBNull.Value ? 0 : Convert.ToInt64(row["RuleStepID"]),
                    UpdatedAt = Convert.ToDateTime(row["UpdatedAt"]),
                    RuleVersion = table.Columns.Contains("RuleVersion") ? Convert.ToInt32(row["RuleVersion"]) : 0
                };
                rule.SavedVersion = rule.Copy();
                rules.Add(rule);
            }
            return rules;
        }

        public static void Delete(long priceItemSeqNo, long mappingSeqNo, long ruleID)
        {
            using (MySqlConnection connection = DB.CreateConnection())
            {
                connection.Open();
                using (MySqlTransaction transaction = connection.BeginTransaction())
                {
                    DeleteCore(connection, transaction, priceItemSeqNo, mappingSeqNo, ruleID);
                    transaction.Commit();
                }
            }
        }

        internal static void DeleteCore(MySqlConnection connection, MySqlTransaction transaction,
            long priceItemSeqNo, long mappingSeqNo, long ruleID)
        {
            using (MySqlCommand command = CreateCommand(connection, transaction, @"
UPDATE PriceItemProcessRule SET IsActive = 0
WHERE SeqNo = @Mapping AND PriceItemSeqNo = @Item AND RuleID = @Rule AND IsActive = 1",
                new[] { new ParamValuePair("@Mapping", mappingSeqNo), new ParamValuePair("@Item", priceItemSeqNo),
                    new ParamValuePair("@Rule", ruleID) }))
            {
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("규칙 연결이 변경되었습니다. 다시 조회해주세요.");
            }
        }

        public static ProcessRuleData GetRule(long ruleID)
        {
            using (MySqlConnection connection = DB.CreateConnection())
            {
                connection.Open();
                return LoadRule(connection, null, ruleID, false);
            }
        }

        private static bool HasColumn(MySqlConnection connection, MySqlTransaction transaction, string column)
        {
            return Convert.ToInt32(Scalar(connection, transaction, @"
SELECT COUNT(*) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ProcessRule' AND COLUMN_NAME = @Column",
                new ParamValuePair("@Column", column))) > 0;
        }

        private static ProcessRuleData LoadRule(MySqlConnection connection, MySqlTransaction transaction, long ruleID, bool forUpdate)
        {
            bool hasVersion = HasColumn(connection, transaction, "RuleVersion");
            var rule = new ProcessRuleData();
            using (var command = CreateCommand(connection, transaction,
                "SELECT RuleID, ProcessName, Category, WorkName, EditDescription, UpdatedAt, "
                + (hasVersion ? "RuleVersion" : "0 AS RuleVersion")
                + " FROM ProcessRule WHERE RuleID = @Rule AND IsActive = 1" + (forUpdate ? " FOR UPDATE" : ""),
                new[] { new ParamValuePair("@Rule", ruleID) }))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidOperationException("규칙을 찾을 수 없습니다. 다시 조회해주세요.");
                rule.RuleID = ruleID;
                rule.ProcessName = Convert.ToString(reader["ProcessName"]);
                rule.Category = Convert.ToString(reader["Category"]);
                rule.WorkName = Convert.ToString(reader["WorkName"]);
                rule.EditDescription = Convert.ToString(reader["EditDescription"]);
                rule.RuleVersion = Convert.ToInt32(reader["RuleVersion"]);
                rule.UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"]);
            }
            // Include inactive steps: editing must never silently discard a multi-step rule.
            using (var command = CreateCommand(connection, transaction,
                "SELECT RuleStepID, ActionCode, ParamsJSON, IsActive FROM ProcessRuleStep WHERE RuleID = @Rule ORDER BY StepSeq, RuleStepID"
                + (forUpdate ? " FOR UPDATE" : ""), new[] { new ParamValuePair("@Rule", ruleID) }))
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    rule.RuleStepID = Convert.ToInt64(reader["RuleStepID"]);
                    rule.ActionCode = Convert.ToString(reader["ActionCode"]);
                    rule.ParamsJSON = reader["ParamsJSON"] == DBNull.Value ? null : Convert.ToString(reader["ParamsJSON"]);
                    if (!Convert.ToBoolean(reader["IsActive"]))
                        throw new InvalidOperationException("비활성 명령이 있는 규칙은 이 화면에서 편집할 수 없습니다.");
                    if (reader.Read()) throw new InvalidOperationException("여러 명령이 있는 규칙입니다. 모든 명령을 보존하기 위해 단일 명령 화면에서는 수정을 지원하지 않습니다.");
                }
            }
            rule.LinkedItemCount = Convert.ToInt32(Scalar(connection, transaction,
                "SELECT COUNT(*) FROM PriceItemProcessRule WHERE RuleID = @Rule AND IsActive = 1", new ParamValuePair("@Rule", ruleID)));
            rule.SavedVersion = rule.Copy();
            return rule;
        }

        public static ProcessRuleData SaveRule(ProcessRuleData rule)
        {
            return SaveTransaction(null, rule);
        }

        public static ProcessRuleData Save(long priceItemSeqNo, ProcessRuleData rule)
        {
            return SaveTransaction(priceItemSeqNo, rule);
        }

        private static ProcessRuleData SaveTransaction(long? item, ProcessRuleData rule)
        {
            rule.Validate();
            using (var connection = DB.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var saved = item.HasValue ? SaveCore(connection, transaction, item.Value, rule)
                            : SaveRuleCore(connection, transaction, rule);
                        transaction.Commit();
                        saved.SavedVersion = saved.Copy();
                        return saved;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        internal static ProcessRuleData SaveCore(MySqlConnection connection, MySqlTransaction transaction,
            long priceItemSeqNo, ProcessRuleData rule)
        {
            rule.Validate();
            if (Scalar(connection, transaction,
                "SELECT SeqNo FROM PriceItems WHERE SeqNo = @Item AND CustCode IS NULL AND IsActive = 1 FOR UPDATE",
                new ParamValuePair("@Item", priceItemSeqNo)) == null)
                throw new InvalidOperationException("저장할 기본 품목을 찾을 수 없습니다. 다시 조회해주세요.");
            if (rule.MappingSeqNo > 0 && Scalar(connection, transaction, @"
SELECT SeqNo FROM PriceItemProcessRule WHERE SeqNo = @Mapping AND PriceItemSeqNo = @Item
AND RuleID = @Rule AND IsActive = 1 FOR UPDATE", new ParamValuePair("@Mapping", rule.MappingSeqNo),
                new ParamValuePair("@Item", priceItemSeqNo), new ParamValuePair("@Rule", rule.RuleID)) == null)
                throw new InvalidOperationException("규칙 연결이 변경되었습니다. 다시 조회해주세요.");

            var saved = SaveRuleCore(connection, transaction, rule, true);
            if (rule.MappingSeqNo == 0)
            {
                Execute(connection, transaction, @"
INSERT INTO PriceItemProcessRule (PriceItemSeqNo, RuleID, IsDefault, SortOrder, IsActive)
SELECT @Item, @Rule, 1, COALESCE(MAX(SortOrder), 0) + 1, 1
FROM PriceItemProcessRule WHERE PriceItemSeqNo = @Item",
                    new ParamValuePair("@Item", priceItemSeqNo), new ParamValuePair("@Rule", saved.RuleID));
                saved.MappingSeqNo = Convert.ToInt64(Scalar(connection, transaction, "SELECT LAST_INSERT_ID()"));
            }
            else if (saved.RuleID != rule.RuleID)
                Execute(connection, transaction, "UPDATE PriceItemProcessRule SET RuleID = @Rule WHERE SeqNo = @Mapping",
                    new ParamValuePair("@Rule", saved.RuleID), new ParamValuePair("@Mapping", rule.MappingSeqNo));
            saved.SavedVersion = saved.Copy();
            return saved;
        }

        private static ProcessRuleData SaveRuleCore(MySqlConnection connection, MySqlTransaction transaction,
            ProcessRuleData rule, bool copyShared = false)
        {
            rule.Validate();
            bool hasVersion = HasColumn(connection, transaction, "RuleVersion");
            var saved = rule.Copy();
            ProcessRuleData current = null;
            if (rule.RuleID > 0)
            {
                current = LoadRule(connection, transaction, rule.RuleID, true);
                var original = rule.SavedVersion;
                if (original == null || current.UpdatedAt != original.UpdatedAt || current.RuleVersion != original.RuleVersion
                    || current.ProcessName != original.ProcessName || current.Category != original.Category || current.WorkName != original.WorkName
                    || current.EditDescription != original.EditDescription || current.RuleStepID != original.RuleStepID
                    || current.ActionCode != original.ActionCode || current.ParamsJSON != original.ParamsJSON)
                    throw new InvalidOperationException("규칙이 다른 화면에서 변경되었습니다. 다시 열어주세요.");
                if (!copyShared && current.LinkedItemCount > 1)
                    throw new InvalidOperationException("여러 상품이 공유하는 규칙입니다. 상품 화면에서 선택 상품용으로 복제하여 수정해주세요.");
            }
            bool clone = current != null && copyShared && current.LinkedItemCount > 1;
            if (current == null || clone)
            {
                // Existing InputMode defaults are respected; never alter the legacy schema.
                Execute(connection, transaction,
                    "INSERT INTO ProcessRule (ProcessName, Category, WorkName, EditDescription, IsActive"
                    + (hasVersion ? ", RuleVersion" : "") + ") VALUES (@Name, @Category, @Work, @Description, 1"
                    + (hasVersion ? ", @Version" : "") + ")",
                    new ParamValuePair("@Name", rule.ProcessName.Trim()), new ParamValuePair("@Work", rule.WorkName),
                    new ParamValuePair("@Category", rule.Category.Trim()),
                    new ParamValuePair("@Description", rule.EditDescription),
                    new ParamValuePair("@Version", current == null ? 1 : current.RuleVersion + 1));
                saved.RuleID = Convert.ToInt64(Scalar(connection, transaction, "SELECT LAST_INSERT_ID()"));
                saved.RuleStepID = 0;
                if (clone)
                {
                    Execute(connection, transaction, @"
UPDATE ProcessRule target INNER JOIN ProcessRule source ON source.RuleID = @OldRule
SET target.Remark = source.Remark WHERE target.RuleID = @Rule",
                        new ParamValuePair("@OldRule", rule.RuleID), new ParamValuePair("@Rule", saved.RuleID));
                    Execute(connection, transaction, @"
INSERT INTO ProcessRuleStep (RuleID, StepSeq, ActionCode, ParamsJSON, Remark, IsActive)
SELECT @Rule, StepSeq, ActionCode, ParamsJSON, Remark, IsActive FROM ProcessRuleStep WHERE RuleID = @OldRule",
                        new ParamValuePair("@Rule", saved.RuleID), new ParamValuePair("@OldRule", rule.RuleID));
                    saved.RuleStepID = Convert.ToInt64(Scalar(connection, transaction,
                        "SELECT COALESCE(MAX(RuleStepID), 0) FROM ProcessRuleStep WHERE RuleID = @Rule", new ParamValuePair("@Rule", saved.RuleID)));
                }
            }
            else
            {
                Execute(connection, transaction,
                    "UPDATE ProcessRule SET ProcessName = @Name, Category = @Category, WorkName = @Work, EditDescription = @Description"
                    + (hasVersion ? ", RuleVersion = RuleVersion + 1" : "") + " WHERE RuleID = @Rule",
                    new ParamValuePair("@Name", rule.ProcessName.Trim()), new ParamValuePair("@Work", rule.WorkName),
                    new ParamValuePair("@Category", rule.Category.Trim()),
                    new ParamValuePair("@Description", rule.EditDescription), new ParamValuePair("@Rule", rule.RuleID));
            }
            if (saved.RuleStepID == 0)
            {
                Execute(connection, transaction, @"
INSERT INTO ProcessRuleStep (RuleID, StepSeq, ActionCode, ParamsJSON, IsActive) VALUES (@Rule, 1, @Action, @Json, 1)",
                    new ParamValuePair("@Rule", saved.RuleID), new ParamValuePair("@Action", rule.ActionCode), new ParamValuePair("@Json", rule.ParamsJSON));
            }
            else
                Execute(connection, transaction,
                    "UPDATE ProcessRuleStep SET ActionCode = @Action, ParamsJSON = @Json WHERE RuleStepID = @Step AND RuleID = @Rule",
                    new ParamValuePair("@Action", rule.ActionCode), new ParamValuePair("@Json", rule.ParamsJSON),
                    new ParamValuePair("@Step", saved.RuleStepID), new ParamValuePair("@Rule", saved.RuleID));
            var reloaded = LoadRule(connection, transaction, saved.RuleID, true);
            reloaded.MappingSeqNo = saved.MappingSeqNo;
            return reloaded;
        }
        private static object Scalar(MySqlConnection connection, MySqlTransaction transaction, string sql, params ParamValuePair[] parameters)
        {
            using (MySqlCommand command = CreateCommand(connection, transaction, sql, parameters))
                return command.ExecuteScalar();
        }

        private static void Execute(MySqlConnection connection, MySqlTransaction transaction, string sql, params ParamValuePair[] parameters)
        {
            using (MySqlCommand command = CreateCommand(connection, transaction, sql, parameters))
                command.ExecuteNonQuery();
        }

        private static MySqlCommand CreateCommand(MySqlConnection connection, MySqlTransaction transaction, string sql, ParamValuePair[] parameters)
        {
            MySqlCommand command = new MySqlCommand(sql, connection, transaction);
            foreach (ParamValuePair parameter in parameters)
                command.Parameters.AddWithValue(parameter.ParameterName, parameter.Value ?? DBNull.Value);
            return command;
        }
    }
}

