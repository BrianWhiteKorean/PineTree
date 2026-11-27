using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace PineTree.Classes
{
    public sealed class ProcessRuleData
    {
        public long MappingSeqNo { get; set; }
        public long RuleID { get; set; }
        public string ProcessName { get; set; }
        public string Category { get; set; }
        public string WorkName { get; set; }
        public string EditDescription { get; set; }
        public string ActionCode { get; set; }
        public string ParamsJSON { get; set; }
        public long RuleStepID { get; set; }
        public int RuleVersion { get; set; }
        public int LinkedItemCount { get; set; }
        public DateTime UpdatedAt { get; set; }
        internal ProcessRuleData SavedVersion { get; set; }

        public string BuildSummary()
        {
            string actionName;
            switch (ActionCode)
            {
                case "ADD_MARGIN": actionName = "여백 / 이미지 확장"; break;
                case "ADD_CUT_LINE": actionName = "재단선 생성"; break;
                case "ADD_GUIDE_LINE": actionName = "미싱 가이드선 생성"; break;
                case "ADD_PUNCH_MARK": actionName = "타공마크 생성"; break;
                case "ADD_WOOD_BAR_LR": actionName = "좌우각목"; break;
                case "ADD_WOOD_BAR_TB": actionName = "상하각목"; break;
                case "ADD_POLE_HEM_LR": actionName = "좌우봉미싱"; break;
                case "ADD_POLE_HEM_TB": actionName = "상하봉미싱"; break;
                default: throw new ArgumentException("지원하지 않는 PDF 편집명령입니다: " + ActionCode);
            }
            var p = ProcessRuleParameters.FromJson(ActionCode, ParamsJSON);
            var summary = new StringBuilder();
            summary.Append((ProcessName ?? string.Empty).Trim()).Append(" · ").Append(actionName).AppendLine();
            if (ActionCode == "ADD_PUNCH_MARK")
            {
                summary.Append(p.PunchMode == "FOUR_SIDES" ? "사방타공"
                    : p.PunchMode == "CUSTOM_SPACING" ? "요청 간격별 타공" : "타공 방식 미입력")
                    .Append(" · 개수 ").Append(Quantity(p.PunchCount, "개"))
                    .Append(" · 직경 ").Append(Quantity(p.DiameterMm, "mm"));
                if (p.PunchMode == "CUSTOM_SPACING")
                    summary.Append(" · 간격 ").Append(Quantity(p.SpacingMm, "mm"));
            }
            else
            {
                if (ProcessRuleParameters.UsesVerticalMargins(ActionCode))
                    summary.Append("상 ").Append(Quantity(p.TopMm, "mm"))
                        .Append(" / 하 ").Append(Quantity(p.BottomMm, "mm"));
                if (ProcessRuleParameters.UsesHorizontalMargins(ActionCode))
                {
                    if (ProcessRuleParameters.UsesVerticalMargins(ActionCode)) summary.Append(" / ");
                    summary.Append("좌 ").Append(Quantity(p.LeftMm, "mm"))
                        .Append(" / 우 ").Append(Quantity(p.RightMm, "mm"));
                }
            }
            return summary.ToString();
        }

        private static string Quantity(decimal? value, string unit)
        {
            return value.HasValue ? value.Value.ToString("0.############################", CultureInfo.CurrentCulture) + unit : "미입력";
        }

        public ProcessRuleData Copy()
        {
            ProcessRuleData copy = (ProcessRuleData)MemberwiseClone();
            copy.SavedVersion = null;
            return copy;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ProcessName))
                throw new ArgumentException("후가공 규칙명을 입력해주세요.");
            if (ProcessName.Trim().Length > 100)
                throw new ArgumentException("후가공 규칙명은 100자까지 입력할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(Category) || Category.Trim() == "[선택하세요]")
                throw new ArgumentException("카테고리를 선택해주세요.");
            if (Category.Trim().Length > 50)
                throw new ArgumentException("카테고리는 50자까지 입력할 수 있습니다.");
            if ((WorkName ?? string.Empty).Length > 200)
                throw new ArgumentException("PDF 편집명령은 200자까지 입력할 수 있습니다.");
            if ((EditDescription ?? string.Empty).Length > 1000)
                throw new ArgumentException("편집방법 / 기본값 설명은 1000자까지 입력할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(ActionCode))
                throw new ArgumentException("PDF 편집명령을 선택해주세요.");
            ProcessRuleParameters.FromJson(ActionCode, ParamsJSON).Validate(ActionCode);
        }
    }

    public sealed class ProcessRuleParameters
    {
        public string PunchMode { get; set; }
        public decimal? PunchCount { get; set; }
        public decimal? DiameterMm { get; set; }
        public decimal? SpacingMm { get; set; }
        public decimal? TopMm { get; set; }
        public decimal? BottomMm { get; set; }
        public decimal? LeftMm { get; set; }
        public decimal? RightMm { get; set; }

        public static bool UsesHorizontalMargins(string action)
        {
            return action == "ADD_MARGIN" || action == "ADD_CUT_LINE" || action == "ADD_GUIDE_LINE"
                || action == "ADD_WOOD_BAR_LR" || action == "ADD_POLE_HEM_LR";
        }

        public static bool UsesVerticalMargins(string action)
        {
            return action == "ADD_MARGIN" || action == "ADD_CUT_LINE" || action == "ADD_GUIDE_LINE"
                || action == "ADD_WOOD_BAR_TB" || action == "ADD_POLE_HEM_TB";
        }

        public void Validate(string action)
        {
            if (action == "ADD_PUNCH_MARK")
            {
                if (PunchMode != "FOUR_SIDES" && PunchMode != "CUSTOM_SPACING")
                    throw new ArgumentException("타공 방식을 선택해주세요.");
                if (!PunchCount.HasValue || PunchCount <= 0 || PunchCount > int.MaxValue || decimal.Truncate(PunchCount.Value) != PunchCount)
                    throw new ArgumentException("타공 개수는 양의 정수로 입력해주세요.");
                if (!DiameterMm.HasValue || DiameterMm <= 0)
                    throw new ArgumentException("타공 직경을 양수로 입력해주세요. 미정인 경우 확정 후 저장해주세요.");
                if (PunchMode == "CUSTOM_SPACING" && (!SpacingMm.HasValue || SpacingMm <= 0))
                    throw new ArgumentException("타공 간격은 양수로 입력해주세요.");
            }
            else if (UsesHorizontalMargins(action) || UsesVerticalMargins(action))
            {
                if (UsesVerticalMargins(action) && (!TopMm.HasValue || !BottomMm.HasValue || TopMm < 0 || BottomMm < 0))
                    throw new ArgumentException("상단 및 하단 여백을 0 이상의 숫자로 입력해주세요.");
                if (UsesHorizontalMargins(action) && (!LeftMm.HasValue || !RightMm.HasValue || LeftMm < 0 || RightMm < 0))
                    throw new ArgumentException("좌측 및 우측 여백을 0 이상의 숫자로 입력해주세요.");
            }
            else throw new ArgumentException("지원하지 않는 PDF 편집명령입니다.");
        }

        public string ToJson(string action)
        {
            Validate(action);
            var values = new Dictionary<string, object>();
            if (action == "ADD_PUNCH_MARK")
            {
                values["punchMode"] = PunchMode;
                values["punchCount"] = (int)PunchCount.Value;
                values["diameterMm"] = DiameterMm;
                values["spacingMm"] = PunchMode == "CUSTOM_SPACING" ? SpacingMm : null;
            }
            else
            {
                if (UsesVerticalMargins(action))
                {
                    values["topMm"] = TopMm;
                    values["bottomMm"] = BottomMm;
                }
                if (UsesHorizontalMargins(action))
                {
                    values["leftMm"] = LeftMm;
                    values["rightMm"] = RightMm;
                }
            }
            values["unit"] = "mm";
            return new JavaScriptSerializer().Serialize(values);
        }

        public static ProcessRuleParameters FromJson(string action, string json)
        {
            var result = new ProcessRuleParameters();
            if (string.IsNullOrWhiteSpace(json)) return result; // Legacy rules require user input before saving.
            try
            {
                var values = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (values == null) throw new ArgumentException("파라미터는 JSON 객체여야 합니다.");
                object unit;
                if (values.TryGetValue("unit", out unit) && !Equals(unit, "mm"))
                    throw new ArgumentException("mm 이외 단위의 규칙은 이 화면에서 편집할 수 없습니다.");
                if (action == "ADD_PUNCH_MARK")
                {
                    object mode;
                    result.PunchMode = values.TryGetValue("punchMode", out mode) ? Convert.ToString(mode) : null;
                    result.PunchCount = Number(values, "punchCount");
                    result.DiameterMm = Number(values, "diameterMm");
                    result.SpacingMm = Number(values, "spacingMm");
                }
                else
                {
                    result.TopMm = Number(values, "topMm");
                    result.BottomMm = Number(values, "bottomMm");
                    result.LeftMm = Number(values, "leftMm");
                    result.RightMm = Number(values, "rightMm");
                }
                return result;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException || ex is InvalidCastException)
            {
                throw new ArgumentException("기존 ParamsJSON을 읽을 수 없습니다. " + ex.Message, ex);
            }
        }

        private static decimal? Number(Dictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null) return null;
            if (value is string || value is bool || value is System.Collections.IEnumerable)
                throw new ArgumentException(key + " 값은 숫자여야 합니다.");
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
    }
}
