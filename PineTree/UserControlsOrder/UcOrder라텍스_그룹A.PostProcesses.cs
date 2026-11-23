using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using C1.Win.C1FlexGrid;
using PineTree.Classes;
using PineTree.PdfEngine;

namespace PineTree.UserControlsOrder
{
    public partial class UcOrder라텍스_그룹A
    {
        private readonly List<OrderPostProcessSetting> _postProcesses = new List<OrderPostProcessSetting>();
        private void InitializePostProcesses()
        {
            FGrid1.Rows.Fixed = 1; FGrid1.Rows.Count = 1;
            FGrid1.Cols.Fixed = 0; FGrid1.Cols.Count = 4;
            string[] names = { "SeqNo", "ProcessName", "Summary", "Delete" };
            string[] captions = { "순번", "후가공명", "설정 내용", "삭제" };
            int[] widths = { 42, 102, 275, 60 };
            for(int i=0;i<4;i++) { FGrid1.Cols[i].Name = names[i]; FGrid1.Cols[i].Caption = captions[i]; FGrid1.Cols[i].Width = widths[i]; }
            FGrid1.AllowEditing = false; FGrid1.AllowSorting = AllowSortingEnum.None;
            FGrid1.AllowDragging = AllowDraggingEnum.None; FGrid1.SelectionMode = SelectionModeEnum.Row;
            FGrid1.Rows.DefaultSize = 28;
            FGrid1.DrawMode = DrawModeEnum.OwnerDraw;
            FGrid1.OwnerDrawCell += delegate(object sender, OwnerDrawCellEventArgs e) {
                if(e.Row < FGrid1.Rows.Fixed || e.Col != FGrid1.Cols["Delete"].Index) return;
                e.DrawCell(DrawCellFlags.Background | DrawCellFlags.Border);
                var bounds = Rectangle.Inflate(e.Bounds, -3, -3);
                if(bounds.Width > 0 && bounds.Height > 0)
                    ButtonRenderer.DrawButton(e.Graphics, bounds, "삭제", FGrid1.Font, false, FGrid1.Enabled ? PushButtonState.Normal : PushButtonState.Disabled);
                e.Handled = true;
            };
            FGrid1.MouseUp += delegate(object sender, MouseEventArgs e) {
                if(e.Button != MouseButtons.Left || e.Clicks != 1 || _generating) return;
                var hit = FGrid1.HitTest(e.X,e.Y);
                if(hit.Type == HitTestTypeEnum.Cell && hit.Row >= FGrid1.Rows.Fixed && hit.Column == FGrid1.Cols["Delete"].Index)
                    RemovePostProcess(hit.Row - FGrid1.Rows.Fixed);
            };
            FGrid1.MouseDoubleClick += delegate(object sender, MouseEventArgs e) {
                var hit = FGrid1.HitTest(e.X,e.Y);
                if(e.Button == MouseButtons.Left && hit.Type == HitTestTypeEnum.Cell && hit.Column != FGrid1.Cols["Delete"].Index)
                    EditPostProcess(hit.Row - FGrid1.Rows.Fixed);
            };
            FGrid1.KeyDown += delegate(object sender, KeyEventArgs e) {
                int index = FGrid1.Row - FGrid1.Rows.Fixed;
                if(e.KeyCode == Keys.Delete) { RemovePostProcess(index); e.Handled = e.SuppressKeyPress = true; }
                else if(e.KeyCode == Keys.Enter) { EditPostProcess(index); e.Handled = e.SuppressKeyPress = true; }
            };
            FGrid1.SizeChanged += delegate {
                FGrid1.Cols["Summary"].Width = Math.Max(160, FGrid1.ClientSize.Width - 42 - 102 - 60 - SystemInformation.VerticalScrollBarWidth - 2);
            };
            cbWork.SelectedIndex = 0;
            btnAdd.Click += delegate { AddPostProcess(); };
            RefreshPostProcessGrid();
        }
        private void RefreshPostProcessGrid()
        {
            if(FGrid1.Cols.Count != 4) return;
            int selected = FGrid1.Row;
            FGrid1.Redraw = false;
            try {
                FGrid1.Rows.Count = FGrid1.Rows.Fixed + _postProcesses.Count;
                for(int i=0;i<_postProcesses.Count;i++) {
                    var item = _postProcesses[i]; item.SeqNo = i+1; int row = i + FGrid1.Rows.Fixed;
                    FGrid1[row, "SeqNo"] = item.SeqNo; FGrid1[row,"ProcessName"] = item.ProcessName;
                    FGrid1[row,"Summary"] = item.SummaryForImage(FinishedWidthMm(), FinishedHeightMm(), WidthMm, HeightMm); FGrid1[row,"Delete"] = "삭제";
                    FGrid1.Rows[row].UserData = item;
                }
                if (_postProcesses.Count > 0) FGrid1.Row = Math.Max(FGrid1.Rows.Fixed, Math.Min(selected,FGrid1.Rows.Count-1));
                inputGroupHeader2.Text = "후가공 목록 " + _postProcesses.Count + "건";
            }
            finally { FGrid1.Redraw = true; }
        }
        private Form CreatePostProcessDialog(OrderPostProcessSetting setting)
        {
            decimal sourceScaleDivisor = ReadZoomRate();
            switch(setting.ProcessCode) {
                case "HOLE": return new Dlg후가공공통_타공마크(setting, FinishedWidthMm(), FinishedHeightMm(), WidthMm, HeightMm, sourceScaleDivisor);
                case "WOOD": return new Dlg현수막_좌우각목(setting, sourceScaleDivisor);
                case "WOOD_TB": return new Dlg현수막_상하각목(setting, sourceScaleDivisor);
                case "POLE_SEW": return new Dlg현수막_봉미싱(setting, sourceScaleDivisor);
                case "HEM_SEW": return new Dlg현수막_사방시접미싱(setting, sourceScaleDivisor);
                case "BAND_SEW": return new Dlg현수막_밴드미싱(setting, sourceScaleDivisor);
                case "ROPE_SEW": return new Dlg현수막_사방줄미싱(setting, sourceScaleDivisor);
                case "CUT": return new Dlg후가공공통_재단선(setting, 0m, sourceScaleDivisor);
                default: throw new ArgumentException("지원하지 않는 후가공입니다.");
            }
        }
        private void AddPostProcess()
        {
            if(_generating || cbWork.SelectedIndex < 0) return;
            string code = OrderPostProcessSetting.CodeForName(cbWork.SelectedOption.Text);
            int existing = _postProcesses.FindIndex(x => x.ProcessCode == code);
            if((code == "CUT" || code == "HOLE" || code == "WOOD" || code == "WOOD_TB") && existing >= 0) { EditPostProcess(existing); return; }
            var setting = code == "HOLE" ? Dlg후가공공통_타공마크.CreateEmptySetting() : OrderPostProcessSetting.Create(code);
            using(var dialog = CreatePostProcessDialog(setting)) {
                if(dialog.ShowDialog(this) != DialogResult.OK) return;
                CommitPostProcess(-1, ((IPostProcessDialog)dialog).Setting);
            }
        }
        private decimal PostProcessImageScale()
        {
            if (cbZoomType.SelectedIndex < 0 && _sourceWidthMm > 0 && WidthMm > 0)
                return WidthMm / _sourceWidthMm;
            return ReadZoomRate();
        }
        private void EditPostProcess(int index)
        {
            if(_generating || index < 0 || index >= _postProcesses.Count) return;
            using(var dialog = CreatePostProcessDialog(_postProcesses[index])) {
                if(dialog.ShowDialog(this) != DialogResult.OK) return;
                CommitPostProcess(index, ((IPostProcessDialog)dialog).Setting);
            }
        }
        private void CommitPostProcess(int index, OrderPostProcessSetting setting)
        {
            var next = OrderPostProcessSetting.CopyList(_postProcesses);
            if(index < 0) next.Add(setting.Copy()); else next[index] = setting.Copy();
            OrderPostProcessSetting.ValidateList(next);
            _outputServerFile = null;
            _postProcesses.Clear(); _postProcesses.AddRange(next);
            UpdateDocumentState();
        }
        private void RemovePostProcess(int index)
        {
            if(_generating || index < 0 || index >= _postProcesses.Count) return;
            _outputServerFile = null;
            _postProcesses.RemoveAt(index); UpdateDocumentState();
        }
        private decimal FinishedWidthMm()
        {
            var wood = _postProcesses.Find(item => item.ProcessCode == "WOOD");
            decimal result = WidthMm + (wood == null ? 0 : 2 * (wood.Parameters.ExtraLineMm ?? wood.Parameters.DimensionMm));
            foreach (var item in _postProcesses.FindAll(x => x.ProcessCode == "ROPE_SEW"))
                result += item.Parameters.RopeMargin(item.Parameters.RopeLeftMm) + item.Parameters.RopeMargin(item.Parameters.RopeRightMm);
            foreach (var item in _postProcesses.FindAll(x => x.ProcessCode == "HEM_SEW")) result += 2 * item.Parameters.HemMarginMm();
            return result;
        }
        private decimal FinishedHeightMm()
        {
            var wood = _postProcesses.Find(item => item.ProcessCode == "WOOD_TB");
            decimal result = HeightMm + (wood == null ? 0 : 2 * (wood.Parameters.ExtraLineMm ?? wood.Parameters.DimensionMm));
            foreach (var item in _postProcesses.FindAll(x => x.ProcessCode == "ROPE_SEW"))
                result += item.Parameters.RopeMargin(item.Parameters.RopeTopMm) + item.Parameters.RopeMargin(item.Parameters.RopeBottomMm);
            foreach (var item in _postProcesses.FindAll(x => x.ProcessCode == "HEM_SEW")) result += 2 * item.Parameters.HemMarginMm();
            return result;
        }
        private decimal RopeGuideThicknessMm() { return .17m * 25.4m / 72m; }
        private void ApplyPostProcessesToPdf(PdfGenerationSettings settings)
        {
            foreach(var item in _postProcesses) {
                var p = item.Parameters;
                if(item.ProcessCode == "CUT") {
                    settings.MakeCutline = !_sourceHasCutline;
                    settings.CutlineColorMode = p.ColorMode;
                    settings.LineWidthMm = (double)p.LineWidthMm; settings.SpotColorName = p.SpotColorName;
                    settings.CutInsetsFromPage = p.CutInsetTopMm.HasValue || p.CutInsetBottomMm.HasValue || p.CutInsetLeftMm.HasValue || p.CutInsetRightMm.HasValue;
                    settings.CutInsetTopMm = (double)(p.CutInsetTopMm ?? 0m);
                    settings.CutInsetBottomMm = (double)(p.CutInsetBottomMm ?? 0m);
                    settings.CutInsetLeftMm = (double)(p.CutInsetLeftMm ?? 0m);
                    settings.CutInsetRightMm = (double)(p.CutInsetRightMm ?? 0m);
                }
                else if(item.ProcessCode == "HOLE") {
                    item.GetMarkCountForImage(FinishedWidthMm(), FinishedHeightMm(), WidthMm, HeightMm);
                    settings.MakeHole = true; settings.HoleType = p.TypeCode; settings.HoleMarkKind = p.HoleMarkKind;
                    settings.MarkLeftRightMm = (double)p.MarkLeftRightMm; settings.MarkTopBottomMm = (double)p.MarkTopBottomMm;
                    settings.HoleEdges = p.HoleEdges;
                    settings.HolePlacements = p.HolePlacements;
                    settings.HoleSpacingMm = (double)p.HoleSpacingMm;
                    settings.HoleBackgroundColor = p.HoleBackgroundColor ?? "#FFFFFF";
                    settings.HoleMarkerColor = p.HoleMarkerColor ?? "#000000";
                }
                else if(item.ProcessCode == "WOOD") {
                    settings.MakeWood = true;
                    settings.WoodExtraMm = (double)(p.ExtraLineMm ?? p.DimensionMm);
                    settings.MakeWoodDesignLine = p.MakeDesignLine;
                    settings.WoodDesignOffsetMm = (double)p.DesignLineMm;
                    if (p.ExtraLineThicknessMm.HasValue) settings.WoodExtraLineThicknessMm = (double)p.ExtraLineThicknessMm.Value;
                    if (p.DesignLineThicknessMm.HasValue) settings.WoodDesignLineThicknessMm = (double)p.DesignLineThicknessMm.Value;
                }
                else if(item.ProcessCode == "WOOD_TB") {
                    settings.MakeWoodTopBottom = true;
                    settings.WoodTopBottomExtraMm = (double)(p.ExtraLineMm ?? p.DimensionMm);
                    settings.MakeWoodTopBottomDesignLine = p.MakeDesignLine;
                    settings.WoodTopBottomDesignOffsetMm = (double)p.DesignLineMm;
                    if (p.ExtraLineThicknessMm.HasValue) settings.WoodTopBottomExtraLineThicknessMm = (double)p.ExtraLineThicknessMm.Value;
                    if (p.DesignLineThicknessMm.HasValue) settings.WoodTopBottomDesignLineThicknessMm = (double)p.DesignLineThicknessMm.Value;
                }
                else if(item.ProcessCode == "ROPE_SEW") {
                    settings.RopeSewPasses.Add(new PdfRopeSewSettings {
                        TypeCode = p.TypeCode, TopMm = (double)p.RopeMargin(p.RopeTopMm), BottomMm = (double)p.RopeMargin(p.RopeBottomMm),
                        LeftMm = (double)p.RopeMargin(p.RopeLeftMm), RightMm = (double)p.RopeMargin(p.RopeRightMm),
                        LineThicknessMm = (double)RopeGuideThicknessMm()
                    });
                }
                else if(item.ProcessCode == "HEM_SEW") {
                    settings.HemSewPasses.Add(new PdfHemSewSettings {
                        TypeCode = p.TypeCode, LineThicknessMm = (double)(.06m * PostProcessImageScale())
                    });
                }
                // Other sewing PDF rules are pending customer confirmation.
            }
        }
        private void ExportLegacyFinishingSettings(BannerOrderEditorState state)
        {
            foreach(var item in _postProcesses) {
                var p = item.Parameters;
                if(item.ProcessCode == "CUT") {
                    state.MakeCutline = true; state.LineWidthMm = p.LineWidthMm;
                    state.ColorMode = p.ColorMode; state.SpotColorName = p.SpotColorName;
                }
                else if(item.ProcessCode == "HOLE") {
                    state.MakeHole = true; state.HoleType = p.TypeCode; state.HoleMarkKind = p.HoleMarkKind; state.HoleSpacingMm = p.HoleSpacingMm;
                    state.MarkLeftRightMm = p.MarkLeftRightMm; state.MarkTopBottomMm = p.MarkTopBottomMm;
                }
            }
        }
    }
}
