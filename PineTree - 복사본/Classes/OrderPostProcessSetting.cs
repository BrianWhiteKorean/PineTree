using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace PineTree.Classes
{
    // Per-order settings, separate from the reusable ProcessRules templates.
    public sealed class OrderPostProcessSetting
    {
        public int SeqNo { get; set; }
        public string ProcessCode { get; set; }
        public PostProcessParameters Parameters { get; set; } = new PostProcessParameters();
        [ScriptIgnore] public string ProcessName { get { return NameFor(ProcessCode); } }
        public OrderPostProcessSetting Copy() { return new JavaScriptSerializer().Deserialize<OrderPostProcessSetting>(new JavaScriptSerializer().Serialize(this)); }
        public static readonly string[] Codes = { "HOLE", "WOOD", "POLE_SEW", "HEM_SEW", "BAND_SEW", "ROPE_SEW", "CUT", "WOOD_TB", "AL_SCROLL" };
        private static readonly string[] Names = { "타공", "좌우각목", "봉미싱", "사방시접미싱", "밴드미싱", "사방줄미싱", "재단", "상하각목", "족자및쫄대" };
        public static string NameFor(string code) { int i = Array.IndexOf(Codes, code); return i < 0 ? code : Names[i]; }
        public static string CodeForName(string name)
        {
            int index = Array.FindIndex(Names, item => string.Concat(item.Where(c => !char.IsWhiteSpace(c))) == string.Concat((name ?? string.Empty).Where(c => !char.IsWhiteSpace(c))));
            if(index < 0) throw new ArgumentException("지원하지 않는 후가공입니다: " + name);
            return Codes[index];
        }
        public static OrderPostProcessSetting Create(string code)
        {
            var item = new OrderPostProcessSetting { ProcessCode = code };
            item.Parameters.TypeCode = OptionsFor(code)[0];
            return item;
        }
        public static string[] OptionsFor(string code)
        {
            switch (code)
            {
                case "HOLE": return new[] { "FOUR_CORNERS", "CUSTOM_SPACING" };
                case "WOOD": return new[] { "LEFT_RIGHT", "TOP_BOTTOM" };
                case "WOOD_TB": return new[] { "TOP_BOTTOM" };
                case "POLE_SEW": return new[] { "LEFT_RIGHT", "TOP_BOTTOM", "TOP", "BOTTOM", "ROUND" };
                case "HEM_SEW": case "BAND_SEW": return new[] { "SINGLE", "DOUBLE" };
                case "ROPE_SEW": return new[] { "ROPE_5", "ROPE_8" };
                case "CUT": return new[] { "NORMAL", "EXACT" };
                case "AL_SCROLL": return new[] { "TOP_BOTTOM" };
                default: throw new ArgumentException("지원하지 않는 후가공입니다: " + code);
            }
        }
        public void Validate()
        {
            if (Parameters == null || !OptionsFor(ProcessCode).Contains(Parameters.TypeCode)) throw new ArgumentException("후가공 종류를 선택해주세요.");
            var p = Parameters;
            if (ProcessCode == "CUT" || ProcessCode == "AL_SCROLL")
            {
                if (p.LineWidthMm <= 0 || p.LineWidthMm > 10 || p.ColorMode != "SPOT" || string.IsNullOrWhiteSpace(p.SpotColorName)
                    || p.SpotColorName.Length > 100 || p.SpotColorName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                    throw new ArgumentException("재단선은 0 초과 10mm 이하의 선두께와 별색 이름(100자 이내)을 입력해주세요.");
                if (ProcessCode == "AL_SCROLL" && (p.ScrollTopMm < 0 || p.ScrollBottomMm < 0 || p.ScrollTopMm > 100000 || p.ScrollBottomMm > 100000))
                    throw new ArgumentException("족자및쫄대 상·하 여백은 0 이상 100,000mm 이하로 입력해주세요.");
                foreach (decimal? inset in new[] { p.CutInsetTopMm, p.CutInsetBottomMm, p.CutInsetLeftMm, p.CutInsetRightMm })
                    if (inset.HasValue && (inset.Value < 0 || inset.Value > 100000))
                        throw new ArgumentException("재단 여백은 0 이상 100,000mm 이하로 입력해주세요.");
            }
            else if (ProcessCode == "HOLE")
            {
                if (p.HoleMarkKind != "DOT" && p.HoleMarkKind != "CROSS") throw new ArgumentException("타공 마크종류를 선택해주세요.");
                if (p.MarkLeftRightMm < 2.5m || p.MarkTopBottomMm < 2.5m || (p.TypeCode == "CUSTOM_SPACING" && p.HoleSpacingMm <= 0))
                    throw new ArgumentException("타공 안쪽 거리는 2.5mm 이상, 최대 간격은 양수로 입력해주세요.");
            }
            else if (ProcessCode == "WOOD" || ProcessCode == "WOOD_TB")
            {
                decimal extra = p.ExtraLineMm ?? p.DimensionMm;
                if (extra < 0 || extra > 100000 || p.DesignLineMm < 0 || (p.MakeDesignLine && p.DesignLineMm > extra))
                    throw new ArgumentException("각목 여분선은 0 이상으로 입력하고, 디자인선 이격은 추가 여백 이내로 입력해주세요.");
                if ((p.ExtraLineThicknessMm.HasValue && (p.ExtraLineThicknessMm <= 0 || p.ExtraLineThicknessMm > 100000))
                    || (p.DesignLineThicknessMm.HasValue && (p.DesignLineThicknessMm <= 0 || p.DesignLineThicknessMm > 100000)))
                    throw new ArgumentException("여분선과 디자인선의 두께는 0 초과 100,000mm 이하로 입력해주세요.");
            }
            else if (ProcessCode == "ROPE_SEW")
            {
                foreach (decimal value in new[] { p.RopeMargin(p.RopeTopMm), p.RopeMargin(p.RopeBottomMm), p.RopeMargin(p.RopeLeftMm), p.RopeMargin(p.RopeRightMm) })
                    if (value <= 0 || value > 100000) throw new ArgumentException("사방줄미싱의 상·하·좌·우 간격은 모두 0 초과 100,000mm 이하로 입력해주세요.");
            }
            else if (p.DimensionMm <= 0 || p.DimensionMm > 100000) throw new ArgumentException("후가공 치수(mm)는 0 초과 100,000 이하로 입력해주세요.");
        }
        public int GetMarkCount(decimal width, decimal height)
        {
            if (ProcessCode != "HOLE") return 0;
            Validate(); var p = Parameters;
            if (width <= 2 * p.MarkLeftRightMm || height <= 2 * p.MarkTopBottomMm)
                throw new ArgumentException("타공 안쪽 거리는 완성 가로·세로의 절반보다 작아야 합니다.");
            if (p.TypeCode == "FOUR_CORNERS") return 4;
            decimal count = 2 * (Math.Ceiling((width - 2*p.MarkLeftRightMm) / p.HoleSpacingMm) + Math.Ceiling((height - 2*p.MarkTopBottomMm) / p.HoleSpacingMm));
            if (count > 2000) throw new ArgumentException("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.");
            return (int)count;
        }
        public string Summary(decimal width, decimal height)
        {
            var p = Parameters;
            if (ProcessCode == "AL_SCROLL") return string.Format("상 여백 {0:0.###}·하 여백 {1:0.###}mm / 재단선 {2:0.###}mm / 별색 {3}", p.ScrollTopMm, p.ScrollBottomMm, p.LineWidthMm, p.SpotColorName);
            if (ProcessCode == "CUT" && p.CutInsetTopMm.HasValue) return string.Format("사방 재단 / {0:0.###}mm / 여백 상 {1:0.###}·하 {2:0.###}·좌 {3:0.###}·우 {4:0.###}mm{5}",
                p.LineWidthMm, p.CutInsetTopMm ?? 0m, p.CutInsetBottomMm ?? 0m, p.CutInsetLeftMm ?? 0m, p.CutInsetRightMm ?? 0m, p.FromSource ? " (원본 포함)" : "");
            if (ProcessCode == "CUT") return string.Format("{0} / 사방 외곽선 / {1:0.###}mm / 별색 {2}{3}", p.TypeCode == "EXACT" ? "정재단" : "일반 재단", p.LineWidthMm, p.SpotColorName, p.FromSource ? " (원본 포함)" : "");
            if (ProcessCode == "HOLE")
            {
                string count; try { count = width > 0 && height > 0 ? GetMarkCount(width, height) + "개" : "개수: 치수 입력 후 계산"; }
                catch (ArgumentException) { count = "치수 확인 필요"; }
                return string.Format("{0} / {1} / 재단선 기준 좌우 {2:0.###}·상하 {3:0.###}mm", p.TypeCode == "FOUR_CORNERS" ? "사방타공" : "최대 " + p.HoleSpacingMm.ToString("0.###") + "mm 간격", count, p.MarkLeftRightMm, p.MarkTopBottomMm) + " / " + (p.HoleMarkKind == "CROSS" ? "십자마크" : "점");
            }
            if (ProcessCode == "WOOD" || ProcessCode == "WOOD_TB") return string.Format("{0} 여분 각 {1:0.###}mm{3} / 디자인선 {2}{4}",
                ProcessCode == "WOOD_TB" ? "상하" : "좌우", p.ExtraLineMm ?? p.DimensionMm, p.MakeDesignLine ? p.DesignLineMm.ToString("0.###") + "mm" : "없음",
                p.ExtraLineThicknessMm.HasValue ? " (두께 " + p.ExtraLineThicknessMm.Value.ToString("0.###") + "mm)" : "",
                p.MakeDesignLine && p.DesignLineThicknessMm.HasValue ? " (두께 " + p.DesignLineThicknessMm.Value.ToString("0.###") + "mm)" : "");
            string[] labels;
            if (ProcessCode == "ROPE_SEW") return string.Format("{0} / 상 {1:0.###}·하 {2:0.###}·좌 {3:0.###}·우 {4:0.###}mm",
                p.TypeCode == "ROPE_8" ? "8mm 로프미싱" : "5mm 로프미싱", p.RopeMargin(p.RopeTopMm), p.RopeMargin(p.RopeBottomMm), p.RopeMargin(p.RopeLeftMm), p.RopeMargin(p.RopeRightMm));
            switch(ProcessCode) {
                case "WOOD": labels = new[] { "좌우각목", "상하각목" }; break;
                case "POLE_SEW": labels = new[] { "좌·우 봉미싱", "상·하 봉미싱", "상단만 봉미싱", "하단만 봉미싱", "원형 족자봉" }; break;
                case "ROPE_SEW": labels = new[] { "5mm 로프미싱", "8mm 로프미싱" }; break;
                default: labels = new[] { "1번 말아 미싱", "2번 말아 미싱" }; break;
            }
            int index = Array.IndexOf(OptionsFor(ProcessCode), p.TypeCode);
            return string.Format("{0} / {1:0.###}mm{2} (PDF 반영 대기)", index < 0 ? p.TypeCode : labels[index], p.DimensionMm, ProcessCode == "POLE_SEW" && p.MakeSlit ? " / 절개선" : "");
        }
        public static List<OrderPostProcessSetting> CopyList(IEnumerable<OrderPostProcessSetting> items)
        {
            var result = items == null ? new List<OrderPostProcessSetting>() : items.Select(x => x.Copy()).ToList();
            for(int i=0;i<result.Count;i++) {
                // Previously stored WOOD/TOP_BOTTOM settings now use the dedicated dialog.
                if(result[i].ProcessCode == "WOOD" && result[i].Parameters.TypeCode == "TOP_BOTTOM") result[i].ProcessCode = "WOOD_TB";
                result[i].SeqNo = i+1;
            }
            return result;
        }
        public static void ValidateList(IEnumerable<OrderPostProcessSetting> items)
        {
            var single = new HashSet<string>();
            foreach(var item in items) {
                if(item == null) throw new ArgumentException("후가공 설정이 비어 있습니다.");
                item.Validate();
                if ((item.ProcessCode == "CUT" || item.ProcessCode == "HOLE" || item.ProcessCode == "WOOD" || item.ProcessCode == "WOOD_TB" || item.ProcessCode == "AL_SCROLL") && !single.Add(item.ProcessCode))
                    throw new ArgumentException("재단·타공·좌우각목·상하각목·족자및쫄대는 각각 한 건씩 등록해주세요. 기존 행을 더블클릭하면 수정할 수 있습니다.");
            }
        }
    }
    public sealed class PostProcessParameters
    {
        public string TypeCode { get; set; }
        public decimal DimensionMm { get; set; }
        public decimal? RopeTopMm { get; set; }
        public decimal? RopeBottomMm { get; set; }
        public decimal? RopeLeftMm { get; set; }
        public decimal? RopeRightMm { get; set; }
        public decimal RopeMargin(decimal? value) { return value ?? (DimensionMm > 0 ? DimensionMm : 30m); }
        // Null reads the old WOOD DimensionMm value; zero is an explicit no-margin setting.
        public decimal? ExtraLineMm { get; set; }
        public bool MakeDesignLine { get; set; }
        public decimal DesignLineMm { get; set; }
        // Missing values preserve the original PDF guide thickness for existing orders.
        public decimal? ExtraLineThicknessMm { get; set; }
        public decimal? DesignLineThicknessMm { get; set; }
        public bool MakeSlit { get; set; }
        public decimal ScrollTopMm { get; set; } = 5m;
        public decimal ScrollBottomMm { get; set; } = 5m;
        public decimal LineWidthMm { get; set; } = .18m;
        // PET: inset from the existing output page; null preserves legacy banner geometry.
        public decimal? CutInsetTopMm { get; set; }
        public decimal? CutInsetBottomMm { get; set; }
        public decimal? CutInsetLeftMm { get; set; }
        public decimal? CutInsetRightMm { get; set; }
        public string ColorMode { get; set; } = "SPOT";
        public string SpotColorName { get; set; } = "thru";
        public bool FromSource { get; set; }
        public string HoleMarkKind { get; set; } = "DOT";
        public decimal MarkLeftRightMm { get; set; } = 20;
        public decimal MarkTopBottomMm { get; set; } = 20;
        public decimal HoleSpacingMm { get; set; } = 500;
    }
}
