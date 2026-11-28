using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PineTree.PdfEngine
{
    public sealed class PdfHemSewSettings
    {
        public string TypeCode { get; set; } = "SINGLE";
        public double MarginMm { get { return TypeCode == "DOUBLE" ? 30 : 15; } }
        public double LineThicknessMm { get; set; } = .06;
        public void Validate()
        {
            if (TypeCode != "SINGLE" && TypeCode != "DOUBLE") throw new ArgumentException("사방시접미싱 종류를 선택해주세요.");
            if (double.IsNaN(LineThicknessMm) || double.IsInfinity(LineThicknessMm) || LineThicknessMm <= 0 || LineThicknessMm > 100000)
                throw new ArgumentException("사방시접미싱 선두께는 양수여야 합니다.");
        }
    }
    public sealed class PdfRopeSewSettings
    {
        public string TypeCode { get; set; } = "ROPE_5";
        public double TopMm { get; set; } = 15;
        public double BottomMm { get; set; } = 15;
        public double LeftMm { get; set; } = 15;
        public double RightMm { get; set; } = 15;
        public double LineThicknessMm { get; set; } = .17 * 25.4 / 72;
        public void Validate()
        {
            if (TypeCode != "ROPE_5" && TypeCode != "ROPE_8") throw new ArgumentException("사방줄미싱 종류를 선택해주세요.");
            foreach (double value in new[] { TopMm, BottomMm, LeftMm, RightMm, LineThicknessMm })
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 100000)
                    throw new ArgumentException("사방줄미싱 간격과 선두께는 양수여야 합니다.");
        }
    }
    public sealed class HoleMarkEdges
    {
        public bool LeftEnabled { get; set; } = true;
        public bool RightEnabled { get; set; } = true;
        public bool TopEnabled { get; set; } = true;
        public bool BottomEnabled { get; set; } = true;
        public double LeftMm { get; set; }
        public double RightMm { get; set; }
        public double TopMm { get; set; }
        public double BottomMm { get; set; }

        public static HoleMarkEdges FromSymmetric(double horizontalMm, double verticalMm)
        {
            return new HoleMarkEdges { LeftMm = horizontalMm, RightMm = horizontalMm, TopMm = verticalMm, BottomMm = verticalMm };
        }

        public bool HasEnabledEdge() { return LeftEnabled || RightEnabled || TopEnabled || BottomEnabled; }

        public void ValidateInsets()
        {
            if (!HasEnabledEdge()) return;
            foreach (double inset in new[] { LeftMm, RightMm, TopMm, BottomMm })
                if (double.IsNaN(inset) || double.IsInfinity(inset) || inset < (double)PdfGenerationSettings.MinimumHoleInsetMm)
                    throw new ArgumentException(FormattableString.Invariant($"타공 안쪽 거리는 {PdfGenerationSettings.MinimumHoleInsetMm}mm 이상으로 입력해주세요."));
        }

        private int HorizontalEdgeCount() { return (TopEnabled ? 1 : 0) + (BottomEnabled ? 1 : 0); }
        private int VerticalEdgeCount() { return (LeftEnabled ? 1 : 0) + (RightEnabled ? 1 : 0); }

        public int GetCornerCount()
        {
            int horizontal = HorizontalEdgeCount();
            return 2 * horizontal + (2 - horizontal) * VerticalEdgeCount();
        }

        public int GetMarkCount(double width, double height, string type, double spacing)
        {
            if (!HasEnabledEdge()) return 0;
            ValidateInsets();
            double horizontalSpan = width - LeftMm - RightMm;
            double verticalSpan = height - TopMm - BottomMm;
            if (horizontalSpan <= 0 || verticalSpan <= 0)
                throw new ArgumentException("타공 안쪽 거리의 좌우·상하 합은 각각 완성 가로·세로보다 작아야 합니다.");
            if (type == "FOUR_CORNERS") return GetCornerCount();
            if (double.IsNaN(spacing) || double.IsInfinity(spacing) || spacing <= 0)
                throw new ArgumentException("타공 최대 간격은 양수여야 합니다.");
            int horizontal = HorizontalEdgeCount();
            int vertical = VerticalEdgeCount();
            double count = GetCornerCount();
            if (horizontal > 0) count += horizontal * (Math.Max(1, Math.Ceiling(horizontalSpan / spacing)) - 1);
            if (vertical > 0) count += vertical * (Math.Max(1, Math.Ceiling(verticalSpan / spacing)) - 1);
            if (count > 2000)
                throw new ArgumentException("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.");
            return (int)count;
        }
    }

    public sealed class HoleMarkRun
    {
        public string Mode { get; set; }
        // Only uniform runs use a manually entered point count.
        public int Count { get; set; }
        public double StartLeftMm { get; set; }
        public double? StartRightMm { get; set; }
        public double StartTopMm { get; set; }
        public double? StartBottomMm { get; set; }
        public double EndLeftMm { get; set; }
        public double? EndRightMm { get; set; }
        public double EndTopMm { get; set; }
        public double? EndBottomMm { get; set; }
        public double SpacingMm { get; set; }

        private const double RoundingToleranceMm = 1e-9;

        public int GetMarkCount(double width = 0, double height = 0, bool vertical = false)
        {
            double startX = StartRightMm ?? StartLeftMm;
            double startY = StartBottomMm ?? StartTopMm;
            ValidatePosition(startX, startY, width, height);
            if (Mode == "UNIFORM")
            {
                if (Count < 1 || Count > 2000) throw new ArgumentException("타공 개수는 1~2,000개로 입력해주세요.");
                double endY = EndBottomMm ?? EndTopMm;
                double endX = EndRightMm ?? EndLeftMm;
                ValidatePosition(endX, endY, width, height);
                bool oppositeX = StartRightMm.HasValue != EndRightMm.HasValue;
                bool oppositeY = StartBottomMm.HasValue != EndBottomMm.HasValue;
                if ((oppositeX && width <= 0) || (oppositeY && height <= 0)) return Count;
                double differenceX = oppositeX ? startX + endX - width : startX - endX;
                double differenceY = oppositeY ? startY + endY - height : startY - endY;
                bool samePoint = Math.Abs(differenceX) <= RoundingToleranceMm && Math.Abs(differenceY) <= RoundingToleranceMm;
                if (samePoint && Count != 1)
                    throw new ArgumentException("균등간격의 시작점과 끝점이 같으면 1개를 입력해주세요.");
                return Count;
            }
            if (Mode == "FIXED")
            {
                if (double.IsNaN(SpacingMm) || double.IsInfinity(SpacingMm) || SpacingMm <= 0)
                    throw new ArgumentException("고정간격은 0mm보다 커야 합니다.");
                double limit = vertical ? height : width;
                if (limit <= 0) return 0;
                double start = vertical
                    ? (StartBottomMm.HasValue ? height - startY : startY)
                    : (StartRightMm.HasValue ? width - startX : startX);
                double span = Math.Max(0, limit - 2 * start);
                double intervals = Math.Floor(span / SpacingMm + RoundingToleranceMm);
                if (intervals >= 2000)
                    throw new ArgumentException("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.");
                return (int)intervals + 1;
            }
            throw new ArgumentException("타공 간격 방식을 선택해주세요.");
        }

        private static void ValidatePosition(double left, double top, double width, double height)
        {
            if (!(left >= 0) || !(top >= 0) || double.IsInfinity(left) || double.IsInfinity(top)
                || (width > 0 && left > width + RoundingToleranceMm) || (height > 0 && top > height + RoundingToleranceMm))
                throw new ArgumentException("타공 위치가 이미지 가로·세로 범위를 벗어났습니다.");
        }
    }

    public sealed class HoleMarkPlacement
    {
        public string TypeCode { get; set; }
        public HoleMarkEdges Edges { get; set; }
        public double SpacingMm { get; set; }
        public HoleMarkRun TopRun { get; set; }
        public HoleMarkRun BottomRun { get; set; }
        public HoleMarkRun LeftRun { get; set; }
        public HoleMarkRun RightRun { get; set; }
        public bool ImageCoordinates { get; set; }

        public bool HasSideRuns() { return LeftRun != null || RightRun != null; }
        public bool HasRuns() { return TopRun != null || BottomRun != null || HasSideRuns(); }

        public void Validate()
        {
            if (HasRuns())
            {
                if (TypeCode != "CUSTOM_SPACING") throw new ArgumentException("타공 배치를 선택해주세요.");
                TopRun?.GetMarkCount();
                BottomRun?.GetMarkCount();
                LeftRun?.GetMarkCount(vertical: true);
                RightRun?.GetMarkCount(vertical: true);
                return;
            }
            if (Edges == null || (TypeCode != "FOUR_CORNERS" && TypeCode != "CUSTOM_SPACING"))
                throw new ArgumentException("타공 배치를 선택해주세요.");
            Edges.ValidateInsets();
            if (Edges.HasEnabledEdge() && TypeCode == "CUSTOM_SPACING" &&
                (double.IsNaN(SpacingMm) || double.IsInfinity(SpacingMm) || SpacingMm <= 0))
                throw new ArgumentException("타공 최대 간격은 양수여야 합니다.");
        }

        public int GetMarkCount(double width, double height, double? imageWidth = null, double? imageHeight = null)
        {
            Validate();
            if (!HasRuns()) return Edges.GetMarkCount(width, height, TypeCode, SpacingMm);
            double runWidth = ImageCoordinates ? imageWidth ?? width : width;
            double runHeight = ImageCoordinates ? imageHeight ?? height : height;
            int count = (TopRun?.GetMarkCount(runWidth, runHeight) ?? 0) + (BottomRun?.GetMarkCount(runWidth, runHeight) ?? 0)
                + (LeftRun?.GetMarkCount(runWidth, runHeight, vertical: true) ?? 0) + (RightRun?.GetMarkCount(runWidth, runHeight, vertical: true) ?? 0);
            if (count > 2000) throw new ArgumentException("전체 타공마크는 최대 2,000개까지 생성할 수 있습니다.");
            return count;
        }

        public static int GetTotalMarkCount(IEnumerable<HoleMarkPlacement> placements, double width, double height,
            double? imageWidth = null, double? imageHeight = null)
        {
            int count = 0;
            foreach (var placement in placements)
            {
                if (placement == null) throw new ArgumentException("타공 배치가 비어 있습니다.");
                count += placement.GetMarkCount(width, height, imageWidth, imageHeight);
                if (count > 2000) throw new ArgumentException("전체 타공마크는 최대 2,000개까지 생성할 수 있습니다.");
            }
            return count;
        }
    }

    // Direct UI settings. These are independent of the reusable finishing rules.
    public sealed class PdfGenerationSettings
    {
        public const decimal MinimumHoleInsetMm = 1.5m;

        public bool GenerateAllPages { get; set; }
        public int SourcePageIndex { get; set; }
        public bool RequireSourceCutline { get; set; }
        public string SourceSizingMode { get; set; } = "FIXED_SIZE";
        public double SourceScale { get; set; } = 1;
        // Target sizes and finishing inputs are in final print millimetres.
        public double OutputRipScale { get; set; } = 1;
        public bool RoundSourceDimensionsBeforeScale { get; set; }
        public double TargetWidthMm { get; set; } = 600;
        public double TargetHeightMm { get; set; } = 1800;
        public string FitMode { get; set; } = "CONTAIN";
        // Add white canvas margins around the finished area without scaling the artwork.
        public bool ExpandImage { get; set; }
        public int SourceRotationDegreesClockwise { get; set; }
        public bool SourceFlipHorizontal { get; set; }
        public double EdgeTopMm { get; set; } = 0;
        public double EdgeBottomMm { get; set; } = 0;
        public double EdgeLeftMm { get; set; } = 0;
        public double EdgeRightMm { get; set; } = 0;
        public bool MakeAlScroll { get; set; }
        public double ScrollTopMm { get; set; } = 5;
        public double ScrollBottomMm { get; set; } = 5;
        public bool MakeCutline { get; set; }
        public bool CutInsetsFromPage { get; set; }
        public bool CutlineBySide { get; set; }
        public double CutInsetTopMm { get; set; }
        public double CutInsetBottomMm { get; set; }
        public double CutInsetLeftMm { get; set; }
        public double CutInsetRightMm { get; set; }
        public double LineWidthMm { get; set; } = 0.18;
        public string CutlineColorMode { get; set; } = "SPOT";
        public string SpotColorName { get; set; } = "thru";
        public bool MakeHole { get; set; }
        public string HoleType { get; set; } = "FOUR_CORNERS";
        public string HoleBackgroundColor { get; set; } = "#FFFFFF";
        public string HoleMarkerColor { get; set; } = "#000000";
        public string HoleMarkKind { get; set; } = "CROSS";
        public double MarkLeftRightMm { get; set; } = 20;
        public double MarkTopBottomMm { get; set; } = 20;
        public HoleMarkEdges HoleEdges { get; set; }
        public List<HoleMarkPlacement> HolePlacements { get; set; }
        public double HoleSpacingMm { get; set; } = 500;

        public bool MakeWood { get; set; }
        public double WoodExtraMm { get; set; }
        public bool MakeWoodDesignLine { get; set; }
        public double WoodDesignOffsetMm { get; set; }
        public double WoodExtraLineThicknessMm { get; set; } = .17 * 25.4 / 72;
        public double WoodDesignLineThicknessMm { get; set; } = .17 * 25.4 / 72;

        public bool MakeWoodTopBottom { get; set; }
        public double WoodTopBottomExtraMm { get; set; }
        public bool MakeWoodTopBottomDesignLine { get; set; }
        public double WoodTopBottomDesignOffsetMm { get; set; }
        public double WoodTopBottomExtraLineThicknessMm { get; set; } = .17 * 25.4 / 72;
        public double WoodTopBottomDesignLineThicknessMm { get; set; } = .17 * 25.4 / 72;
        public List<PdfHemSewSettings> HemSewPasses { get; set; } = new List<PdfHemSewSettings>();
        public List<PdfRopeSewSettings> RopeSewPasses { get; set; } = new List<PdfRopeSewSettings>();

        public double FinishedWidthMm()
        {
            double result = TargetWidthMm + (MakeWood ? 2 * WoodExtraMm : 0);
            foreach (var rope in RopeSewPasses) result += rope.LeftMm + rope.RightMm;
            foreach (var hem in HemSewPasses) result += 2 * hem.MarginMm;
            return result;
        }
        public double FinishedHeightMm()
        {
            double result = TargetHeightMm + (MakeWoodTopBottom ? 2 * WoodTopBottomExtraMm : 0) + (MakeAlScroll ? ScrollTopMm + ScrollBottomMm : 0);
            foreach (var rope in RopeSewPasses) result += rope.TopMm + rope.BottomMm;
            foreach (var hem in HemSewPasses) result += 2 * hem.MarginMm;
            return result;
        }

        public int GetMarkCount()
        {
            if (!MakeHole) return 0;
            if (HolePlacements != null)
                return HoleMarkPlacement.GetTotalMarkCount(HolePlacements, FinishedWidthMm(), FinishedHeightMm(), TargetWidthMm, TargetHeightMm);
            var edges = HoleEdges ?? HoleMarkEdges.FromSymmetric(MarkLeftRightMm, MarkTopBottomMm);
            return edges.GetMarkCount(FinishedWidthMm(), FinishedHeightMm(), HoleType, HoleSpacingMm);
        }

        public void Validate()
        {
            if (double.IsNaN(OutputRipScale) || double.IsInfinity(OutputRipScale) || OutputRipScale < 1)
                throw new ArgumentException("RIP 출력 배율은 1 이상이어야 합니다.");
            if (SourceRotationDegreesClockwise < 0 || SourceRotationDegreesClockwise > 270 || SourceRotationDegreesClockwise % 90 != 0)
                throw new ArgumentException("원본 회전은 0, 90, 180, 270도여야 합니다.");
            if (HemSewPasses == null) throw new ArgumentException("사방시접미싱 목록이 비어 있습니다.");
            foreach (var hem in HemSewPasses) {
                if (hem == null) throw new ArgumentException("사방시접미싱 설정이 비어 있습니다.");
                hem.Validate();
            }
            if (RopeSewPasses == null) throw new ArgumentException("사방줄미싱 목록이 올바르지 않습니다.");
            if (SourcePageIndex < 0) throw new ArgumentException("대지 번호가 올바르지 않습니다.");
            foreach (var rope in RopeSewPasses)
            {
                if (rope == null) throw new ArgumentException("사방줄미싱 설정이 비어 있습니다.");
                rope.Validate();
            }
            foreach (double value in new[] { TargetWidthMm, TargetHeightMm, EdgeTopMm, EdgeBottomMm,
                EdgeLeftMm, EdgeRightMm, ScrollTopMm, ScrollBottomMm, CutInsetTopMm, CutInsetBottomMm, CutInsetLeftMm, CutInsetRightMm, LineWidthMm, MarkLeftRightMm, MarkTopBottomMm, HoleSpacingMm, WoodExtraMm, WoodDesignOffsetMm, WoodTopBottomExtraMm, WoodTopBottomDesignOffsetMm,
                WoodTopBottomExtraLineThicknessMm, WoodTopBottomDesignLineThicknessMm, WoodExtraLineThicknessMm, WoodDesignLineThicknessMm })
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("유효한 숫자를 입력해주세요.");
            if (TargetWidthMm <= 0 || TargetHeightMm <= 0 || FinishedWidthMm() + EdgeLeftMm + EdgeRightMm > 14400
                || FinishedHeightMm() + EdgeTopMm + EdgeBottomMm > 14400)
                throw new ArgumentException("완성 가로·세로는 양수이며 가장자리를 포함한 출력 크기는 14,400mm 이하여야 합니다.");
            if (EdgeTopMm < 0 || EdgeBottomMm < 0 || EdgeLeftMm < 0 || EdgeRightMm < 0)
                throw new ArgumentException("가장자리 여유는 0 이상이어야 합니다.");
            if (MakeAlScroll && (ScrollTopMm < 0 || ScrollBottomMm < 0))
                throw new ArgumentException("족자및쫄대 상·하 여백은 0 이상이어야 합니다.");
            if (MakeWood && (WoodExtraMm < 0 || WoodDesignOffsetMm < 0 || (MakeWoodDesignLine && WoodDesignOffsetMm > WoodExtraMm)))
                throw new ArgumentException("좌우각목 여백은 0 이상, 디자인선 이격은 추가 여백 이내로 입력해주세요.");
            if (MakeWoodTopBottom && (WoodTopBottomExtraMm < 0 || WoodTopBottomDesignOffsetMm < 0
                || (MakeWoodTopBottomDesignLine && WoodTopBottomDesignOffsetMm > WoodTopBottomExtraMm)))
                throw new ArgumentException("상하각목 여백은 0 이상, 디자인선 이격은 추가 여백 이내로 입력해주세요.");
            if (FitMode != "CONTAIN" && FitMode != "COVER") throw new ArgumentException("배치방식을 선택해주세요.");
            if (MakeWood && (WoodExtraLineThicknessMm <= 0 || WoodExtraLineThicknessMm > 100000
                || WoodDesignLineThicknessMm <= 0 || WoodDesignLineThicknessMm > 100000))
                throw new ArgumentException("좌우각목 선두께는 0 초과 100,000mm 이하로 입력해주세요.");
            if (MakeWoodTopBottom && (WoodTopBottomExtraLineThicknessMm <= 0 || WoodTopBottomExtraLineThicknessMm > 100000
                || WoodTopBottomDesignLineThicknessMm <= 0 || WoodTopBottomDesignLineThicknessMm > 100000))
                throw new ArgumentException("상하각목 선두께는 0 초과 100,000mm 이하로 입력해주세요.");
            if (MakeCutline && (Math.Abs(CutInsetLeftMm) + Math.Abs(CutInsetRightMm) >= FinishedWidthMm() + (CutInsetsFromPage ? EdgeLeftMm + EdgeRightMm : 0)
                || Math.Abs(CutInsetTopMm) + Math.Abs(CutInsetBottomMm) >= FinishedHeightMm() + (CutInsetsFromPage ? EdgeTopMm + EdgeBottomMm : 0)))
                throw new ArgumentException("재단 여백을 적용한 가로·세로 길이는 0보다 커야 합니다.");
            if (MakeCutline && (LineWidthMm <= 0 || LineWidthMm > 10))
                throw new ArgumentException("재단선 두께는 0 초과 10mm 이하로 입력해주세요.");
            if (MakeCutline && CutlineColorMode != "SPOT" && CutlineColorMode != "BLACK")
                throw new ArgumentException("재단선 색상방식을 선택해주세요.");
            if (MakeCutline && CutlineColorMode == "SPOT" && (string.IsNullOrWhiteSpace(SpotColorName)
                || SpotColorName.Length > 100 || SpotColorName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0))
                throw new ArgumentException("별색 이름은 100자 이내로 입력해주세요.");
            if (MakeHole)
            {
                foreach (string color in new[] { HoleBackgroundColor, HoleMarkerColor })
                    if (color == null || !System.Text.RegularExpressions.Regex.IsMatch(color, @"\A#[0-9a-fA-F]{6}\z"))
                        throw new ArgumentException("타공 색상은 #RRGGBB 형식이어야 합니다.");
                if (HoleMarkKind != "DOT" && HoleMarkKind != "CROSS") throw new ArgumentException("타공 마크종류를 선택해주세요.");
                if (HolePlacements == null && HoleType != "FOUR_CORNERS" && HoleType != "CUSTOM_SPACING")
                    throw new ArgumentException("타공 배치를 선택해주세요.");
                GetMarkCount();
            }
        }
    }

    public sealed class OrderDocumentResult
    {
        public bool Success { get; set; }
        public bool IsCanceled { get; set; }
        public string Error { get; set; }
        public string FileType { get; set; }
        public string FileInfo { get; set; }
        public int PageCount { get; set; }
        public double SourceWidthMm { get; set; }
        public double SourceHeightMm { get; set; }
        public string SourceDimensionNote { get; set; }
        public bool HasCutline { get; set; }
        public string CutlineColorName { get; set; }
        public double? CutlineWidthMm { get; set; }
        public string RegionDescription { get; set; }
        public string Recipe { get; set; }
        public string OutputPath { get; set; }
        public double Scale { get; set; }
        public double OutputWidthMm { get; set; }
        public double OutputHeightMm { get; set; }
        public double OutputRipScale { get; set; } = 1;
        public int MarkCount { get; set; }
        [ScriptIgnore]
        public Image PreviewImage { get; set; }
    }

    public static class OrderDocumentService
    {
        // Keep compressed previews, not shared GDI images. Each caller owns its bitmap.
        private sealed class CachedSource
        {
            internal string Key, Metadata;
            internal byte[] Preview;
        }
        private static readonly object SourceCacheGate = new object();
        private static readonly LinkedList<CachedSource> SourceCache = new LinkedList<CachedSource>();
        private const int SourceCacheLimit = 32 * 1024 * 1024;
        private static int sourceCacheBytes;

        private static string SourceKey(string source, int page)
        {
            var file = new FileInfo(source);
            return file.FullName.ToUpperInvariant() + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks
                + "|" + file.CreationTimeUtc.Ticks + "|" + page;
        }

        private static OrderDocumentResult ReadSourceCache(string key)
        {
            lock (SourceCacheGate)
            {
                for (var node = SourceCache.First; node != null; node = node.Next)
                {
                    if (node.Value.Key != key) continue;
                    var cached = node.Value;
                    SourceCache.Remove(node); SourceCache.AddLast(cached);
                    var result = new JavaScriptSerializer().Deserialize<OrderDocumentResult>(cached.Metadata);
                    if (cached.Preview != null)
                        using (var stream = new MemoryStream(cached.Preview))
                        using (var image = Image.FromStream(stream)) result.PreviewImage = new Bitmap(image);
                    return result;
                }
            }
            return null;
        }

        private static void WriteSourceCache(string key, OrderDocumentResult result, byte[] preview = null)
        {
            string metadata = new JavaScriptSerializer().Serialize(result);
            int size = metadata.Length * 2 + (preview == null ? 0 : preview.Length);
            if (size > SourceCacheLimit) return;
            lock (SourceCacheGate)
            {
                for (var node = SourceCache.First; node != null; node = node.Next)
                    if (node.Value.Key == key)
                    {
                        sourceCacheBytes -= node.Value.Metadata.Length * 2 + (node.Value.Preview == null ? 0 : node.Value.Preview.Length);
                        SourceCache.Remove(node); break;
                    }
                SourceCache.AddLast(new CachedSource { Key = key, Metadata = metadata, Preview = preview });
                sourceCacheBytes += size;
                while (sourceCacheBytes > SourceCacheLimit || SourceCache.Count > 64)
                {
                    var oldest = SourceCache.First.Value;
                    sourceCacheBytes -= oldest.Metadata.Length * 2 + (oldest.Preview == null ? 0 : oldest.Preview.Length);
                    SourceCache.RemoveFirst();
                }
            }
        }

        public static Task PrepareRuntimeAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                var timing = PdfTiming.Start(Guid.NewGuid().ToString("N"), "prepare");
                string status = "failed";
                try
                {
                    using (timing?.Measure("runtime_prepare")) PdfRuntime.EnsureAvailable(token, timing);
                    status = "success";
                }
                catch (OperationCanceledException)
                {
                    status = "canceled";
                    throw;
                }
                finally { timing?.Write(status); }
            }, token);
        }

        public const string InputFilter = "출력 원본 (PDF/AI/EPS/PNG/JPG)|*.pdf;*.ai;*.eps;*.png;*.jpg;*.jpeg|PDF 문서|*.pdf|Illustrator 문서|*.ai|EPS 문서|*.eps|PNG 이미지|*.png|JPEG 이미지|*.jpg;*.jpeg";

        public static Task<OrderDocumentResult> InspectAsync(string source, CancellationToken token)
        {
            return RunAsync("inspect", source, null, 0, 1600, false, token);
        }

        public static Task<OrderDocumentResult> PreviewAsync(string source, int page, int maxPixels, bool fullCanvas, CancellationToken token)
        {
            return RunAsync("preview", source, null, page, maxPixels, fullCanvas, token);
        }

        // View changes and closing are normal lifecycle events, not exceptional failures.
        public static Task<OrderDocumentResult> PreviewPageAsync(string source, int page, int maxPixels, bool fullCanvas, CancellationToken token)
        {
            return Task.Run(() => RunWorker("preview", source, null, page, maxPixels, fullCanvas, token, suppressCancellation: true));
        }

        public static Task<OrderDocumentResult> ExtractPageAsync(string source, string output, int page, CancellationToken token)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("원본파일과 저장 경로는 달라야 합니다.");
            if (!string.Equals(Path.GetExtension(output), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("PDF 형식으로 저장해주세요.");
            return RunAsync("extract", source, output, page, 1600, false, token);
        }

        // Rectangle uses fractions of the displayed page, with its origin at the top left.
        // Normal viewport cancellation returns IsCanceled, without throwing in the debugger.
        public static Task<OrderDocumentResult> PreviewRegionAsync(string source, int page, int maxPixels,
            bool fullCanvas, RectangleF region, CancellationToken token)
        {
            return Task.Run(() => RunWorker("preview", source, null, page, maxPixels, fullCanvas, token,
                viewportRegion: new[] { (double)region.X, region.Y, region.Width, region.Height }));
        }

        public static Task<OrderDocumentResult> GenerateSampleAsync(string source, string output, CancellationToken token)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("원본파일과 출력파일의 저장 경로는 달라야 합니다.");
            if (!string.Equals(Path.GetExtension(output), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력파일은 PDF 형식으로 저장해주세요.");
            return RunAsync("generate", source, output, 0, 1600, false, token);
        }

        public static Task<OrderDocumentResult> GenerateAsync(string source, string output, PdfGenerationSettings settings, CancellationToken token)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            settings.Validate();
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("원본파일과 출력파일의 저장 경로는 달라야 합니다.");
            if (!string.Equals(Path.GetExtension(output), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력파일은 PDF 형식으로 저장해주세요.");
            // Freeze settings before the background operation starts.
            string snapshot = new JavaScriptSerializer().Serialize(settings);
            int page = settings.SourcePageIndex;
            return Task.Run(() => RunWorker("generate", source, output, page, 1600, false, token, snapshot), token);
        }

        private static Task<OrderDocumentResult> RunAsync(string operation, string source, string output,
            int page, int maxPixels, bool fullCanvas, CancellationToken token)
        {
            return Task.Run(() => RunWorker(operation, source, output, page, maxPixels, fullCanvas, token), token);
        }

        private static OrderDocumentResult RunWorker(string operation, string source, string output,
            int page, int maxPixels, bool fullCanvas, CancellationToken token, string settingsJson = null, double[] viewportRegion = null, bool suppressCancellation = false)
        {
            string requestId = Guid.NewGuid().ToString("N");
            var timing = PdfTiming.Start(requestId, operation);
            string status = "failed";
            try
            {
                var result = ExecuteWorker(operation, source, output, page, maxPixels, fullCanvas, token,
                    settingsJson, viewportRegion, suppressCancellation, requestId, timing);
                status = result.IsCanceled ? "canceled" : "success";
                return result;
            }
            catch (OperationCanceledException)
            {
                status = "canceled";
                throw;
            }
            finally { timing?.Write(status); }
        }

        private static OrderDocumentResult ExecuteWorker(string operation, string source, string output, int page,
            int maxPixels, bool fullCanvas, CancellationToken token, string settingsJson, double[] viewportRegion,
            bool suppressCancellation, string requestId, PdfTiming timing)
        {
            bool quietCancellation = viewportRegion != null || suppressCancellation;
            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException("작업요청파일을 선택해주세요.", source);
            string sourceKey = null, cacheKey = null;
            if (operation == "inspect" || (operation == "preview" && viewportRegion == null))
            {
                sourceKey = SourceKey(source, page);
                cacheKey = sourceKey + "|" + operation + "|" + maxPixels + "|" + fullCanvas;
                var cached = ReadSourceCache(cacheKey);
                if (cached != null)
                {
                    timing?.Record("source_cache_hit", 0);
                    if (token.IsCancellationRequested)
                    {
                        if (cached.PreviewImage != null) cached.PreviewImage.Dispose();
                        if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                    }
                    return cached;
                }
            }
            // Shared runtime preparation should finish once even if this particular viewport closes.
            PdfRuntime runtime;
            using (timing?.Measure("runtime_prepare"))
                runtime = PdfRuntime.EnsureAvailable(quietCancellation ? CancellationToken.None : token, timing);
            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
            string python = ConfigurationManager.AppSettings["PdfPythonPath"];
            if (string.IsNullOrWhiteSpace(python)) python = runtime.PythonPath;
            if (!File.Exists(python)) throw new FileNotFoundException("PDF Python 실행 파일이 없습니다.", python);
            string ghostscript = ConfigurationManager.AppSettings["PdfGhostscriptDllPath"];
            if (string.IsNullOrWhiteSpace(ghostscript)) ghostscript = runtime.GhostscriptPath;
            else if (!Path.IsPathRooted(ghostscript))
                ghostscript = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ghostscript));
            string work = Path.Combine(Path.GetTempPath(), "PineTreePdf", requestId);
            Directory.CreateDirectory(work);
            string preview = Path.Combine(work, "preview.png");
            string generated = Path.Combine(work, "generated.pdf");
            string staging = null;
            try
            {
                var request = new Dictionary<string, object>
                {
                    { "RequestId", requestId }, { "Operation", operation }, { "SourcePath", Path.GetFullPath(source) }, { "PageIndex", page },
                    { "WorkDirectory", work },
                    { "MaxPixels", maxPixels }, { "FullCanvas", fullCanvas },
                    { "OutputPath", operation == "generate" || operation == "extract" ? generated : preview },
                    { "GhostscriptDll", ghostscript }
                };
                var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                if (viewportRegion != null) request["ViewportRegion"] = viewportRegion;
                if (settingsJson != null) request["Settings"] = json.DeserializeObject(settingsJson);
                string requestFile = Path.Combine(work, "request.json");
                string responseFile = Path.Combine(work, "response.json");
                File.WriteAllText(requestFile, json.Serialize(request), new UTF8Encoding(false));
                var start = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = "-B " + QuoteArgument(runtime.WorkerPath) + " " + QuoteArgument(requestFile) + " " + QuoteArgument(responseFile),
                    WorkingDirectory = runtime.DirectoryPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8
                };
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                start.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
                start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
                start.EnvironmentVariables["PINETREE_PDF_WORKER_TIMING"] = timing == null ? "0" : "1";
                using (timing?.Measure("python_process"))
                using (var process = StartWorker(start, timing))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        while (!process.WaitForExit(100))
                        {
                            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                            if (watch.Elapsed > TimeSpan.FromMinutes(3))
                                throw new TimeoutException("파일 처리 시간이 3분을 초과했습니다. 파일의 크기와 상태를 확인해주세요.");
                        }
                        if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                        Task.WaitAll(stdout, stderr);
                        timing?.ReadWorker(stdout.Result);
                        if (!File.Exists(responseFile))
                            throw new InvalidOperationException("PDF 처리 엔진이 응답하지 않았습니다. 런타임 설치 상태를 확인해주세요.\r\n" +
                                (stderr.Result.Length > 1200 ? stderr.Result.Substring(0, 1200) : stderr.Result));
                    }
                    finally
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                }
                var result = json.Deserialize<OrderDocumentResult>(File.ReadAllText(responseFile, Encoding.UTF8));
                if (result == null || !result.Success)
                    throw new InvalidOperationException(result == null ? "파일 처리 결과가 없습니다." : result.Error);
                if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                if (operation == "preview")
                {
                    using (var stream = new MemoryStream(File.ReadAllBytes(preview)))
                    using (var image = Image.FromStream(stream))
                        result.PreviewImage = new Bitmap(image);
                }
                if (cacheKey != null && !token.IsCancellationRequested && sourceKey == SourceKey(source, page))
                {
                    WriteSourceCache(cacheKey, result, operation == "preview" ? File.ReadAllBytes(preview) : null);
                    if (operation == "preview") WriteSourceCache(sourceKey + "|inspect|1600|False", result);
                }
                if (operation == "generate" || operation == "extract")
                using (timing?.Measure("final_pdf_save"))
                {
                    string final = Path.GetFullPath(output);
                    string directory = Path.GetDirectoryName(final);
                    Directory.CreateDirectory(directory);
                    staging = Path.Combine(directory, ".pinetree-" + Guid.NewGuid().ToString("N") + ".tmp");
                    using (var input = File.OpenRead(generated))
                    using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[1024 * 1024];
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                            file.Write(buffer, 0, count);
                        }
                        file.Flush(true);
                    }
                    if (ShouldStop(token, quietCancellation)) return new OrderDocumentResult { IsCanceled = true };
                    if (File.Exists(final)) File.Replace(staging, final, null);
                    else File.Move(staging, final);
                    staging = null;
                    result.OutputPath = final;
                }
                return result;
            }
            finally
            {
                if (staging != null && File.Exists(staging)) File.Delete(staging);
                try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static Process StartWorker(ProcessStartInfo start, PdfTiming timing)
        {
            using (timing?.Measure("python_start")) return Process.Start(start);
        }

        private static bool ShouldStop(CancellationToken token, bool quietCancellation)
        {
            if (!token.IsCancellationRequested) return false;
            if (quietCancellation) return true;
            token.ThrowIfCancellationRequested();
            return false;
        }

        private static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2).Append('"');
            return result.ToString();
        }
    }
}
