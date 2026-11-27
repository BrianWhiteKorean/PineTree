using System;
using System.Reflection;
using System.Data;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;
using PineTree.UserControls;
using MySql.Data.MySqlClient;
using C1.Win.C1InputPanel;

internal static class ProcessRuleRegression
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (ArgumentException) { checks++; return; }
        throw new Exception(message);
    }
    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
    private static object Call(string name, params object[] args)
    {
        try { return typeof(ProcessRuleRepository).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Sql(MySqlConnection connection, MySqlTransaction transaction, string sql)
    {
        using (var cmd = new MySqlCommand(sql, connection, transaction)) return cmd.ExecuteScalar();
    }
    private static ProcessRuleData Save(MySqlConnection connection, MySqlTransaction transaction, long item, ProcessRuleData rule)
    {
        return (ProcessRuleData)Call("SaveCore", connection, transaction, item, rule);
    }
    private static ProcessRuleParameters Margin()
    {
        return new ProcessRuleParameters { TopMm = 3.125m, BottomMm = 0, LeftMm = 2.5m, RightMm = 10 };
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            foreach (var action in new[] { "ADD_MARGIN", "ADD_CUT_LINE", "ADD_GUIDE_LINE" })
            {
                string json = Margin().ToJson(action);
                var roundTrip = ProcessRuleParameters.FromJson(action, json);
                Check(roundTrip.TopMm == 3.125m && roundTrip.BottomMm == 0 && roundTrip.RightMm == 10, "margin round trip");
                Check(!json.Contains("punchMode"), "margin excludes punch fields");
            }
            var punch = new ProcessRuleParameters { PunchMode = "FOUR_SIDES", PunchCount = 4, DiameterMm = 5.25m, SpacingMm = 300 };
            string punchJson = punch.ToJson("ADD_PUNCH_MARK");
            Check(punchJson.Contains("\"spacingMm\":null") && !punchJson.Contains("topMm"), "four-sides spacing and inactive fields");
            punch.PunchMode = "CUSTOM_SPACING";
            Check(ProcessRuleParameters.FromJson("ADD_PUNCH_MARK", punch.ToJson("ADD_PUNCH_MARK")).SpacingMm == 300, "spacing round trip");
            punch.PunchCount = 2.5m;
            Reject(() => punch.ToJson("ADD_PUNCH_MARK"), "fractional count accepted");
            punch.PunchCount = 0;
            Reject(() => punch.ToJson("ADD_PUNCH_MARK"), "zero count accepted");
            punch.PunchCount = 4;
            punch.DiameterMm = null;
            Reject(() => punch.ToJson("ADD_PUNCH_MARK"), "unknown diameter accepted");
            punch.DiameterMm = 5;
            punch.SpacingMm = 0;
            Reject(() => punch.ToJson("ADD_PUNCH_MARK"), "zero spacing accepted");
            var margin = Margin(); margin.TopMm = -1;
            Reject(() => margin.ToJson("ADD_MARGIN"), "negative margin accepted");
            Reject(() => ProcessRuleParameters.FromJson("ADD_MARGIN", "{bad"), "malformed json accepted");
            Reject(() => ProcessRuleParameters.FromJson("ADD_MARGIN", "{\"topMm\":true}"), "boolean accepted");
            Reject(() => ProcessRuleParameters.FromJson("ADD_MARGIN", "{\"unit\":\"inch\"}"), "unsupported unit accepted");
            Reject(() => Margin().ToJson("UNKNOWN"), "unknown action accepted");
            Reject(() => new ProcessRuleData { Category = "검증카테고리", ProcessName = "", ActionCode = "ADD_MARGIN", ParamsJSON = Margin().ToJson("ADD_MARGIN") }.Validate(), "blank name accepted");
            Check(ProcessRuleParameters.FromJson("ADD_PUNCH_MARK", null).DiameterMm == null, "legacy null must remain unknown");
            var categoryRule = new ProcessRuleData { ProcessName = "카테고리 검증", ActionCode = "ADD_MARGIN", ParamsJSON = Margin().ToJson("ADD_MARGIN") };
            foreach (var invalidCategory in new[] { null, "", "  ", "[선택하세요]", " [선택하세요] " })
            {
                categoryRule.Category = invalidCategory;
                Reject(() => categoryRule.Validate(), "missing category accepted");
            }
            categoryRule.Category = "각목";
            categoryRule.Validate();
            Check(categoryRule.Copy().Category == "각목", "category missing from copied draft");
            using (var categoryDialog = new DialogProcessRule())
            {
                var combo = Field<InputComboBox>(categoryDialog, "cbCategory");
                var bind = typeof(DialogProcessRule).GetMethod("BindCategories", BindingFlags.Instance | BindingFlags.NonPublic);
                var selected = typeof(DialogProcessRule).GetMethod("GetSelectedCategory", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(combo.Text == "[선택하세요]" && selected.Invoke(categoryDialog, null) == null, "category placeholder default");
                var items = new DataTable();
                items.Columns.Add("ItemName");
                items.Rows.Add("각목"); items.Rows.Add("미싱"); items.Rows.Add(" 각목 "); items.Rows.Add(" ");
                bind.Invoke(categoryDialog, new object[] { items, "미싱" });
                Check(combo.Items.Count == 3 && (string)selected.Invoke(categoryDialog, null) == "미싱", "category list and existing selection");
                bind.Invoke(categoryDialog, new object[] { items, "삭제된분류" });
                Check(combo.Text == "[선택하세요]" && selected.Invoke(categoryDialog, null) == null, "missing common code must require reselection");
                combo.SelectedIndex = 1;
                Check((string)selected.Invoke(categoryDialog, null) == "각목", "selected category save value");
            }
            if (Array.IndexOf(args, "--summary") < 0)
            using (var dialog = new DialogProcessRule())
            {
                var action = Field<InputComboBox>(dialog, "cbWorkName");
                var mode = Field<InputComboBox>(dialog, "cbHoleType");
                var gap = Field<InputNumericBox>(dialog, "tbHoleGap");
                var top = Field<InputNumericBox>(dialog, "tbMarginTop");
                var count = Field<InputNumericBox>(dialog, "tbHoleCnt");
                var getAction = typeof(DialogProcessRule).GetMethod("GetSelectedActionCode", BindingFlags.Instance | BindingFlags.NonPublic);
                var selectAction = typeof(DialogProcessRule).GetMethod("SelectActionCode", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(action.SelectedIndex == 0 && getAction.Invoke(dialog, null) == null, "placeholder must be selected and have no action code");
                Check(!gap.Enabled && !top.Enabled && !count.Enabled, "initial enabled state");
                var codes = new[] { "ADD_MARGIN", "ADD_CUT_LINE", "ADD_PUNCH_MARK", "ADD_GUIDE_LINE" };
                for (int i = 0; i < 4; i++)
                {
                    action.SelectedIndex = i + 1;
                    Check(top.Enabled == (i != 2) && count.Enabled == (i == 2), "action enabled state");
                    Check((string)getAction.Invoke(dialog, null) == codes[i], "action code shifted by placeholder");
                    action.SelectedIndex = 0;
                    Check((bool)selectAction.Invoke(dialog, new object[] { codes[i] }) && action.SelectedIndex == i + 1, "saved action restored to wrong option");
                }
                Check(!(bool)selectAction.Invoke(dialog, new object[] { "UNKNOWN" }) && action.SelectedIndex == 0,
                    "unknown action must fall back to placeholder");
                action.SelectedIndex = -1;
                Check(getAction.Invoke(dialog, null) == null && !top.Enabled && !count.Enabled, "unselected action must remain invalid");
                action.SelectedIndex = 3;
                mode.SelectedIndex = 0;
                Check(!gap.Enabled && count.Enabled, "four-sides enabled state");
                mode.SelectedIndex = 1;
                Check(gap.Enabled && !top.Enabled, "custom-spacing enabled state");
                gap.Value = 321.125m;
                action.SelectedIndex = 0;
                Check(getAction.Invoke(dialog, null) == null && !gap.Enabled && !top.Enabled && !count.Enabled,
                    "placeholder must disable all parameters and prevent command saving");
                action.SelectedIndex = 3;
                Check(gap.Value == 321.125m, "switch must preserve inactive values");
                Check(top.DecimalPlaces >= 3 && Field<InputNumericBox>(dialog, "tbHoleSize").ValueIsNull, "decimal support and no diameter default");
            }
            foreach (var code in new[] { "ADD_WOOD_BAR_LR", "ADD_WOOD_BAR_TB", "ADD_POLE_HEM_LR", "ADD_POLE_HEM_TB" })
            using (var dialog = new DialogProcessRule())
            using (var editor = new UcProcessRuleEach())
            {
                bool horizontal = code.EndsWith("_LR");
                var select = typeof(DialogProcessRule).GetMethod("SelectActionCode", BindingFlags.Instance | BindingFlags.NonPublic);
                var get = typeof(DialogProcessRule).GetMethod("GetSelectedActionCode", BindingFlags.Instance | BindingFlags.NonPublic);
                Check((bool)select.Invoke(dialog, new object[] { code }) && (string)get.Invoke(dialog, null) == code, "directional command selection round trip");
                Check(Field<InputNumericBox>(dialog, "tbMarginLeft").Enabled == horizontal
                    && Field<InputNumericBox>(dialog, "inputNumericBox6").Enabled == horizontal
                    && Field<InputNumericBox>(dialog, "tbMarginTop").Enabled != horizontal
                    && Field<InputNumericBox>(dialog, "tbMarginBottom").Enabled != horizontal
                    && !Field<InputNumericBox>(dialog, "tbHoleCnt").Enabled, "directional margin controls");
                Check(Field<InputLabel>(dialog, "inputLabelMargin1").Enabled
                    && Field<InputLabel>(dialog, "inputLabelMargin4").Enabled == horizontal
                    && Field<InputLabel>(dialog, "inputLabelMargin5").Enabled == horizontal
                    && Field<InputLabel>(dialog, "inputLabelMargin2").Enabled != horizontal
                    && Field<InputLabel>(dialog, "inputLabelMargin3").Enabled != horizontal, "directional margin labels");
                var p = horizontal ? new ProcessRuleParameters { LeftMm = 2.5m, RightMm = 0 }
                    : new ProcessRuleParameters { TopMm = 2.5m, BottomMm = 0 };
                string json = p.ToJson(code);
                Check(json.Contains("leftMm") == horizontal && json.Contains("rightMm") == horizontal
                    && json.Contains("topMm") != horizontal && json.Contains("bottomMm") != horizontal, "only enabled directions serialized");
                var restored = ProcessRuleParameters.FromJson(code, json);
                Check(horizontal ? restored.LeftMm == 2.5m && restored.RightMm == 0 && restored.TopMm == null
                    : restored.TopMm == 2.5m && restored.BottomMm == 0 && restored.LeftMm == null, "directional json round trip");
                var rule = new ProcessRuleData { Category = "검증카테고리", ProcessName = "방향별 검증", ActionCode = code, ParamsJSON = json };
                rule.Validate();
                editor.SetRule(1, rule);
                string summary = Field<Control>(editor, "tbDescription").Text;
                Check(!summary.Contains(code) && (horizontal ? summary.Contains("좌 2.5mm / 우 0mm") && !summary.Contains("상 ")
                    : summary.Contains("상 2.5mm / 하 0mm") && !summary.Contains("좌 ")), "directional summary");
                if (horizontal) p.LeftMm = -1; else p.TopMm = -1;
                Reject(() => p.ToJson(code), "negative directional margin accepted");
                if (horizontal) p.LeftMm = null; else p.TopMm = null;
                Reject(() => p.ToJson(code), "missing directional margin accepted");
            }
            using (var editor = new UcProcessRuleEach())
            {
                var summaryBox = Field<Control>(editor, "tbDescription");
                Check((bool)summaryBox.GetType().GetProperty("ReadOnly").GetValue(summaryBox, null), "summary must be read-only");
                foreach (var action in new[] { "ADD_MARGIN", "ADD_CUT_LINE", "ADD_GUIDE_LINE" })
                {
                    var rule = new ProcessRuleData { Category = "검증카테고리", ProcessName = "요약 검증", ActionCode = action,
                        ParamsJSON = Margin().ToJson(action), EditDescription = "설명 원문" };
                    editor.SetRule(1, rule);
                    Check(summaryBox.Text.Contains("요약 검증") && summaryBox.Text.Contains("상 3.125mm")
                        && summaryBox.Text.Contains("하 0mm") && summaryBox.Text.Contains("설명: 설명 원문"), "margin command summary");
                    Check(rule.EditDescription == "설명 원문" && rule.ProcessName == "요약 검증", "summary mutated source fields");
                }
                var punchRule = new ProcessRuleData { Category = "검증카테고리", ProcessName = "타공", ActionCode = "ADD_PUNCH_MARK",
                    ParamsJSON = new ProcessRuleParameters { PunchMode = "FOUR_SIDES", PunchCount = 4, DiameterMm = 5.25m }.ToJson("ADD_PUNCH_MARK") };
                editor.SetRule(1, punchRule);
                Check(summaryBox.Text.Contains("사방타공") && summaryBox.Text.Contains("4개")
                    && summaryBox.Text.Contains("5.25mm") && !summaryBox.Text.Contains("간격"), "four sides summary");
                punchRule.ParamsJSON = new ProcessRuleParameters { PunchMode = "CUSTOM_SPACING", PunchCount = 6, DiameterMm = 5, SpacingMm = 300 }.ToJson("ADD_PUNCH_MARK");
                editor.SetRule(1, punchRule);
                Check(summaryBox.Text.Contains("요청 간격별 타공") && summaryBox.Text.Contains("간격 300mm"), "custom spacing summary refresh");
                punchRule.ParamsJSON = null;
                editor.SetRule(1, punchRule);
                Check(summaryBox.Text.Contains("미입력") && !summaryBox.Text.Contains("300mm"), "legacy null summary");
                punchRule.ParamsJSON = "{bad";
                editor.SetRule(1, punchRule);
                Check(summaryBox.Text.Contains("파라미터 확인 필요"), "invalid json must not break list rendering");
            }
            Console.WriteLine("Local regression checks passed: " + checks);
            if (Array.IndexOf(args, "--database") >= 0) DatabaseChecks();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void DatabaseChecks()
    {
        var categories = CommonCodeRepository.GetItems("규칙카테고리");
        Check(categories.Rows.Count > 0, "규칙카테고리 common code group is empty");
        using (var dialog = new DialogProcessRule())
        {
            string category = Convert.ToString(categories.Rows[0]["ItemName"]).Trim();
            typeof(DialogProcessRule).GetMethod("BindCategories", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(dialog, new object[] { categories, category });
            Check(Field<InputComboBox>(dialog, "cbCategory").Text == category, "actual common code category selection");
        }
        long createdRule = 0;
        using (var connection = DB.CreateConnection())
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    long item = Convert.ToInt64(Sql(connection, transaction, "SELECT SeqNo FROM PriceItems WHERE CustCode IS NULL AND IsActive = 1 ORDER BY SeqNo LIMIT 1"));
                    long otherItem = Convert.ToInt64(Sql(connection, transaction, "SELECT SeqNo FROM PriceItems WHERE CustCode IS NULL AND IsActive = 1 AND SeqNo <> " + item + " ORDER BY SeqNo LIMIT 1"));
                    var draft = new ProcessRuleData { Category = "검증카테고리", ProcessName = "__ROLLBACK_TEST__", WorkName = "independent work text", EditDescription = "initial", ActionCode = "ADD_MARGIN", ParamsJSON = Margin().ToJson("ADD_MARGIN") };
                    var saved = Save(connection, transaction, item, draft);
                    createdRule = saved.RuleID;
                    Check(saved.Category == draft.Category, "category insert and reload");
                    Check(saved.MappingSeqNo > 0 && saved.RuleStepID > 0 && saved.ParamsJSON == draft.ParamsJSON, "new rule, step and mapping");
                    var stale = saved;
                    var edit = (ProcessRuleData)Call("LoadRule", connection, transaction, saved.RuleID, true);
                    edit.MappingSeqNo = saved.MappingSeqNo;
                    edit.EditDescription = "updated";
                    edit.Category = "수정카테고리";
                    edit.ActionCode = "ADD_GUIDE_LINE";
                    saved = Save(connection, transaction, item, edit);
                    Check(saved.Category == "수정카테고리", "category update and reload");
                    Check(saved.ActionCode == "ADD_GUIDE_LINE" && saved.EditDescription == "updated" && saved.WorkName == "independent work text", "update action, json and preserve work text");
                    bool conflict = false;
                    try { Save(connection, transaction, item, stale); } catch (InvalidOperationException) { conflict = true; }
                    Check(conflict, "stale edit was accepted");
                    Sql(connection, transaction, "UPDATE ProcessRule SET Remark = 'master remark' WHERE RuleID = " + saved.RuleID);
                    Sql(connection, transaction, "UPDATE ProcessRuleStep SET Remark = 'step remark' WHERE RuleID = " + saved.RuleID);
                    Sql(connection, transaction, "INSERT INTO PriceItemProcessRule (PriceItemSeqNo, RuleID) VALUES (" + otherItem + "," + saved.RuleID + ")");
                    var shared = (ProcessRuleData)Call("LoadRule", connection, transaction, saved.RuleID, true);
                    shared.MappingSeqNo = saved.MappingSeqNo;
                    shared.ParamsJSON = new ProcessRuleParameters { PunchMode = "FOUR_SIDES", PunchCount = 4, DiameterMm = 5 }.ToJson("ADD_PUNCH_MARK");
                    shared.ActionCode = "ADD_PUNCH_MARK";
                    shared.Category = "복제카테고리";
                    var clone = Save(connection, transaction, item, shared);
                    Check(clone.Category == "복제카테고리"
                        && Convert.ToString(Sql(connection, transaction, "SELECT Category FROM ProcessRule WHERE RuleID = " + saved.RuleID)) == "수정카테고리", "shared clone category and original preservation");
                    Check(clone.RuleID != saved.RuleID && clone.ActionCode == "ADD_PUNCH_MARK", "shared rule must clone");
                    Check(Convert.ToString(Sql(connection, transaction, "SELECT ActionCode FROM ProcessRuleStep WHERE RuleID = " + saved.RuleID)) == "ADD_GUIDE_LINE", "shared original changed");
                    Check(Convert.ToString(Sql(connection, transaction, "SELECT Remark FROM ProcessRule WHERE RuleID = " + clone.RuleID)) == "master remark", "clone master remark lost");
                    Check(Convert.ToString(Sql(connection, transaction, "SELECT Remark FROM ProcessRuleStep WHERE RuleID = " + clone.RuleID)) == "step remark", "clone step remark lost");
                    var standalone = (ProcessRuleData)Call("SaveRuleCore", connection, transaction, draft, false);
                    Check(Convert.ToInt32(Sql(connection, transaction, "SELECT COUNT(*) FROM PriceItemProcessRule WHERE RuleID = " + standalone.RuleID)) == 0, "standalone rule unexpectedly linked");
                    Sql(connection, transaction, "INSERT INTO ProcessRuleStep (RuleID, StepSeq, ActionCode) VALUES (" + clone.RuleID + ", 2, 'ADD_CUT_LINE')");
                    bool multiple = false;
                    try { Call("LoadRule", connection, transaction, clone.RuleID, true); } catch (InvalidOperationException) { multiple = true; }
                    Check(multiple && Convert.ToInt32(Sql(connection, transaction, "SELECT COUNT(*) FROM ProcessRuleStep WHERE RuleID = " + clone.RuleID)) == 2, "multi-step rule not protected");
                    bool stepFailed = false;
                    try { Sql(connection, transaction, "INSERT INTO ProcessRuleStep (RuleID, StepSeq, ActionCode) VALUES (" + standalone.RuleID + ", 1, 'ADD_MARGIN')"); } catch (MySqlException) { stepFailed = true; }
                    Check(stepFailed, "expected step failure missing");
                }
                finally { transaction.Rollback(); }
            }
            Check(Convert.ToInt32(Sql(connection, null, "SELECT COUNT(*) FROM ProcessRule WHERE RuleID = " + createdRule)) == 0, "rollback left master row");
            Check(Convert.ToInt32(Sql(connection, null, "SELECT COUNT(*) FROM ProcessRuleStep WHERE RuleID = " + createdRule)) == 0, "rollback left step row");
            Check(Convert.ToInt32(Sql(connection, null, "SELECT COUNT(*) FROM PriceItemProcessRule WHERE RuleID = " + createdRule)) == 0, "rollback left mapping row");
        }
        Console.WriteLine("Database checks passed; all test writes rolled back. Total checks: " + checks);
    }
}



