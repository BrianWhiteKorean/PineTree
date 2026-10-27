using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PineTree.Classes
{
    // UI state stays independent of the large PDF runtime assembly.
    public sealed class BannerOrderEditorState
    {
        public int Version { get; set; } = 1;
        public decimal WidthMm { get; set; }
        public decimal HeightMm { get; set; }
        public decimal Quantity { get; set; }
        // Null keeps the manually sized values of orders saved before zoom controls existed.
        public string ZoomType { get; set; }
        public decimal ZoomRate { get; set; } = 1;
        public string FitMode { get; set; } = "CONTAIN";
        public decimal EdgeTopMm { get; set; } = 2;
        public decimal EdgeBottomMm { get; set; } = 2;
        public decimal EdgeLeftMm { get; set; } = 2;
        public decimal EdgeRightMm { get; set; } = 2;
        // Null means a legacy document. An empty list explicitly means no finishing work.
        public List<OrderPostProcessSetting> PostProcesses { get; set; }
        public List<OrderPostProcessSetting> GetPostProcesses()
        {
            if (PostProcesses != null) return OrderPostProcessSetting.CopyList(PostProcesses);
            var items = new List<OrderPostProcessSetting>();
            if (MakeCutline) {
                var item = OrderPostProcessSetting.Create("CUT");
                item.Parameters.LineWidthMm = LineWidthMm; item.Parameters.SpotColorName = string.IsNullOrWhiteSpace(SpotColorName) ? "thru" : SpotColorName;
                item.Parameters.FromSource = SourceHasCutline; items.Add(item);
            }
            if (MakeHole) {
                var item = OrderPostProcessSetting.Create("HOLE");
                item.Parameters.TypeCode = HoleType; item.Parameters.HoleSpacingMm = HoleSpacingMm;
                item.Parameters.MarkLeftRightMm = MarkLeftRightMm; item.Parameters.MarkTopBottomMm = MarkTopBottomMm; items.Add(item);
            }
            return OrderPostProcessSetting.CopyList(items);
        }
        public bool MakeCutline { get; set; }
        public decimal LineWidthMm { get; set; } = .18m;
        public string ColorMode { get; set; }
        public string SpotColorName { get; set; }
        public bool MakeHole { get; set; }
        public string HoleType { get; set; } = "FOUR_CORNERS";
        public decimal MarkLeftRightMm { get; set; } = 20;
        public decimal MarkTopBottomMm { get; set; } = 20;
        public decimal HoleSpacingMm { get; set; } = 500;
        public string SourceFilePath { get; set; }
        public string SourceFileInfo { get; set; }
        public decimal SourceWidthMm { get; set; }
        public decimal SourceHeightMm { get; set; }
        public bool SourceHasCutline { get; set; }

        public string ToJson() { return new JavaScriptSerializer().Serialize(this); }
        public static BannerOrderEditorState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var state = new JavaScriptSerializer().Deserialize<BannerOrderEditorState>(json);
            if (state == null || state.Version != 1) throw new InvalidOperationException("지원하지 않는 현수막 편집정보 형식입니다.");
            OrderPostProcessSetting.ValidateList(state.GetPostProcesses());
            return state;
        }
    }
}
