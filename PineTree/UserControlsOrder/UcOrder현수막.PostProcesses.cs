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
    public partial class UcOrder현수막
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
                    FGrid1[row,"Summary"] = item.Summary(FinishedWidthMm(),FinishedHeightMm()); FGrid1[row,"Delete"] = "삭제";
                    FGrid1.Rows[row].UserData = item;
                }
                if (_postProcesses.Count > 0) FGrid1.Row = Math.Max(FGrid1.Rows.Fixed, Math.Min(selected,FGrid1.Rows.Count-1));
                inputGroupHeader2.Text = "후가공 목록 " + _postProcesses.Count + "건";
            }
            finally { FGrid1.Redraw = true; }
        }
        private Form CreatePostProcessDialog(OrderPostProcessSetting setting)
        {
            switch(setting.ProcessCode) {
                case "HOLE": return new Dialog타공마크(setting,FinishedWidthMm(),FinishedHeightMm());
                case "WOOD": return new Dialog좌우각목(setting);
                case "WOOD_TB": return new Dialog상하각목(setting);
                case "POLE_SEW": return new Dialog봉미싱(setting);
                case "HEM_SEW": return new Dialog사방시접미싱(setting);
                case "BAND_SEW": return new Dialog밴드미싱(setting);
                case "ROPE_SEW": return new Dialog로프미싱(setting);
                case "CUT": return new Dialog재단선(setting);
                default: throw new ArgumentException("지원하지 않는 후가공입니다.");
            }
        }
        private void AddPostProcess()
        {
            if(_generating || cbWork.SelectedIndex < 0) return;
            string code = OrderPostProcessSetting.CodeForName(cbWork.SelectedOption.Text);
            int existing = _postProcesses.FindIndex(x => x.ProcessCode == code);
            if((code == "CUT" || code == "HOLE" || code == "WOOD" || code == "WOOD_TB") && existing >= 0) { EditPostProcess(existing); return; }
            using(var dialog = CreatePostProcessDialog(OrderPostProcessSetting.Create(code))) {
                if(dialog.ShowDialog(this) != DialogResult.OK) return;
                CommitPostProcess(-1, ((IPostProcessDialog)dialog).Setting);
            }
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
            _postProcesses.Clear(); _postProcesses.AddRange(next);
            UpdateDocumentState();
        }
        private void RemovePostProcess(int index)
        {
            if(_generating || index < 0 || index >= _postProcesses.Count) return;
            _postProcesses.RemoveAt(index); UpdateDocumentState();
        }
        private decimal FinishedWidthMm()
        {
            var wood = _postProcesses.Find(item => item.ProcessCode == "WOOD");
            return WidthMm + (wood == null ? 0 : 2 * (wood.Parameters.ExtraLineMm ?? wood.Parameters.DimensionMm));
        }
        private decimal FinishedHeightMm()
        {
            var wood = _postProcesses.Find(item => item.ProcessCode == "WOOD_TB");
            return HeightMm + (wood == null ? 0 : 2 * (wood.Parameters.ExtraLineMm ?? wood.Parameters.DimensionMm));
        }
        private void ApplyPostProcessesToPdf(PdfGenerationSettings settings)
        {
            foreach(var item in _postProcesses) {
                var p = item.Parameters;
                if(item.ProcessCode == "CUT") {
                    settings.MakeCutline = !_sourceHasCutline;
                    settings.LineWidthMm = (double)p.LineWidthMm; settings.SpotColorName = p.SpotColorName;
                }
                else if(item.ProcessCode == "HOLE") {
                    item.GetMarkCount(FinishedWidthMm(),FinishedHeightMm());
                    settings.MakeHole = true; settings.HoleType = p.TypeCode;
                    settings.MarkLeftRightMm = (double)p.MarkLeftRightMm; settings.MarkTopBottomMm = (double)p.MarkTopBottomMm;
                    settings.HoleSpacingMm = (double)p.HoleSpacingMm;
                }
                else if(item.ProcessCode == "WOOD") {
                    settings.MakeWood = true;
                    settings.WoodExtraMm = (double)(p.ExtraLineMm ?? p.DimensionMm);
                    settings.MakeWoodDesignLine = p.MakeDesignLine;
                    settings.WoodDesignOffsetMm = (double)p.DesignLineMm;
                }
                else if(item.ProcessCode == "WOOD_TB") {
                    settings.MakeWoodTopBottom = true;
                    settings.WoodTopBottomExtraMm = (double)(p.ExtraLineMm ?? p.DimensionMm);
                    settings.MakeWoodTopBottomDesignLine = p.MakeDesignLine;
                    settings.WoodTopBottomDesignOffsetMm = (double)p.DesignLineMm;
                }
                // Sewing PDF rules are pending customer confirmation.
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
                    state.MakeHole = true; state.HoleType = p.TypeCode; state.HoleSpacingMm = p.HoleSpacingMm;
                    state.MarkLeftRightMm = p.MarkLeftRightMm; state.MarkTopBottomMm = p.MarkTopBottomMm;
                }
            }
        }
    }
}
