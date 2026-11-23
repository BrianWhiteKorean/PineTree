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
        // FIXED_SIZE uses an entered width and a height calculated from the original aspect ratio.
        public int SourceRotationDegreesClockwise { get; set; }
        public bool SourceFlipHorizontal { get; set; }
        public string ZoomType { get; set; }
        public decimal ZoomRate { get; set; } = 1;
        internal int GetZoomSelectionIndex()
        {
            if (ZoomType == "ORIGINAL") return 0;
            if (ZoomType == "SCALE" && ZoomRate == 10m) return 1;
            // Retain saved dimensions for legacy custom sizes until a supported mode is selected.
            return WidthMm > 0 && HeightMm > 0 ? -1 : 0;
        }
        public string FitMode { get; set; } = "CONTAIN";
        public decimal EdgeTopMm { get; set; } = 0;
        public decimal EdgeBottomMm { get; set; } = 0;
        public decimal EdgeLeftMm { get; set; } = 0;
        public decimal EdgeRightMm { get; set; } = 0;
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
                item.Parameters.HoleMarkKind = HoleMarkKind;
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
        public string HoleMarkKind { get; set; } = "DOT";
        public decimal MarkLeftRightMm { get; set; } = 20;
        public decimal MarkTopBottomMm { get; set; } = 20;
        public decimal HoleSpacingMm { get; set; } = 500;
        public string SourceFilePath { get; set; }
        public OrderServerFile SourceServerFile { get; set; }
        public OrderServerFile OutputServerFile { get; set; }
        public string SourceFileInfo { get; set; }
        public decimal SourceWidthMm { get; set; }
        public decimal SourceHeightMm { get; set; }
        public bool SourceHasCutline { get; set; }
        public int WorkingArtboardIndex { get; set; }
        public List<BannerArtboardEditorState> Artboards { get; set; }

        private static void ValidateSourceTransform(BannerOrderEditorState state)
        {
            if (state.SourceRotationDegreesClockwise < 0 || state.SourceRotationDegreesClockwise > 270 || state.SourceRotationDegreesClockwise % 90 != 0)
                throw new InvalidOperationException("원본 회전은 0, 90, 180, 270도여야 합니다.");
        }

        public string ToJson() { return new JavaScriptSerializer().Serialize(this); }
        public static BannerOrderEditorState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var state = new JavaScriptSerializer().Deserialize<BannerOrderEditorState>(json);
            if (state == null || state.Version != 1) throw new InvalidOperationException("지원하지 않는 현수막 편집정보 형식입니다.");
            ValidateSourceTransform(state);
            OrderPostProcessSetting.ValidateList(state.GetPostProcesses());
            if (state.SourceServerFile != null) state.SourceServerFile.Validate();
            if (state.OutputServerFile != null) state.OutputServerFile.Validate();
            if (state.WorkingArtboardIndex < 0) throw new InvalidOperationException("대지 번호가 올바르지 않습니다.");
            if (state.Artboards != null)
            {
                var indices = new HashSet<int>();
                foreach (var page in state.Artboards)
                {
                    if (page == null || page.PageIndex < 0 || !indices.Add(page.PageIndex) || page.Settings == null || page.Settings.Artboards != null)
                        throw new InvalidOperationException("대지별 편집정보가 올바르지 않습니다.");
                    ValidateSourceTransform(page.Settings);
                    OrderPostProcessSetting.ValidateList(page.Settings.GetPostProcesses());
                    if (page.Settings.OutputServerFile != null) page.Settings.OutputServerFile.Validate();
                }
            }
            return state;
        }
    }

    public sealed class BannerArtboardEditorState
    {
        public int PageIndex { get; set; }
        public BannerOrderEditorState Settings { get; set; }
    }
}
