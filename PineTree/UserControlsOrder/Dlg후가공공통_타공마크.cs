using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using C1.Win.C1FlexGrid;
using PineTree.Classes;
using PineTree.PdfEngine;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg후가공공통_타공마크 : Form, IPostProcessDialog
    {
        private readonly OrderPostProcessSetting _draft;
        private readonly List<HoleMarkPlacement> _placements;
        private readonly decimal _width;
        private readonly decimal _height;
        private readonly decimal _imageWidth;
        private readonly decimal _imageHeight;
        private readonly decimal _sourceScaleDivisor;

        public OrderPostProcessSetting Setting { get; private set; }

        internal static OrderPostProcessSetting CreateEmptySetting()
        {
            var setting = OrderPostProcessSetting.Create("HOLE");
            setting.Parameters.HolePlacements = new List<HoleMarkPlacement>();
            return setting;
        }

        public Dlg후가공공통_타공마크() : this(CreateEmptySetting(), 0, 0) { }

        public Dlg후가공공통_타공마크(OrderPostProcessSetting setting, decimal width, decimal height)
            : this(setting, width, height, width, height) { }

        public Dlg후가공공통_타공마크(OrderPostProcessSetting setting, decimal width, decimal height, decimal imageWidth, decimal imageHeight)
            : this(setting, width, height, imageWidth, imageHeight, 1m) { }

        public Dlg후가공공통_타공마크(OrderPostProcessSetting setting, decimal width, decimal height, decimal imageWidth, decimal imageHeight, decimal sourceScaleDivisor)
        {
            InitializeComponent();
            _draft = setting.Copy();
            _placements = _draft.Parameters.GetHolePlacements().ToList();
            _width = width;
            _height = height;
            _imageWidth = imageWidth;
            _imageHeight = imageHeight;
            _sourceScaleDivisor = sourceScaleDivisor;

            var parameters = _draft.Parameters;
            var kinds = new[] { "DOT", "CROSS" };
            colorPickerHole.Color = ColorTranslator.FromHtml(parameters.HoleBackgroundColor ?? "#FFFFFF");
            colorPickerMarker.Color = ColorTranslator.FromHtml(parameters.HoleMarkerColor ?? "#000000");
            PostProcessDialogBinding.Select(cbHoleKind, kinds, parameters.HoleMarkKind);
            PostProcessDialogBinding.Select(cbDeployType, OrderPostProcessSetting.OptionsFor("HOLE"), parameters.TypeCode);
            if (_placements.FirstOrDefault()?.HasSideRuns() == true) cbDeployType.SelectedIndex = 2;
            InitializeGrid();

            btnAdd.Click += delegate { AddPlacement(); };
            btnRemove.Click += delegate { RemovePlacement(); };
            cbDeployType.SelectedIndexChanged += delegate { UpdateButtons(); };
            FGrid1.AfterRowColChange += delegate { UpdateButtons(); };
            FGrid1.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                var hit = FGrid1.HitTest(e.X, e.Y);
                if (hit.Type == HitTestTypeEnum.Cell) EditPlacement(hit.Row - FGrid1.Rows.Fixed);
            };
            RefreshGrid(0);

            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () =>
            {
                var value = _draft.Copy();
                var p = value.Parameters;
                p.HoleBackgroundColor = ColorHex(colorPickerHole.Color);
                p.HoleMarkerColor = ColorHex(colorPickerMarker.Color);
                p.HoleMarkKind = PostProcessDialogBinding.Selected(cbHoleKind, kinds);
                p.HolePlacements = new List<HoleMarkPlacement>(_placements);
                var first = _placements.FirstOrDefault();
                p.TypeCode = first?.TypeCode ?? "FOUR_CORNERS";
                p.HoleEdges = first?.Edges;
                if (first?.TypeCode == "CUSTOM_SPACING" && !first.HasRuns()) p.HoleSpacingMm = (decimal)first.SpacingMm;
                if (_width > 0 && _height > 0) value.GetMarkCountForImage(_width, _height, _imageWidth, _imageHeight);
                return value;
            }, value => Setting = value);
        }

        private static string ColorHex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private void InitializeGrid()
        {
            FGrid1.Rows.Fixed = 1;
            FGrid1.Cols.Fixed = 0;
            FGrid1.Cols.Count = 3;
            FGrid1.AllowEditing = false;
            FGrid1.AllowSorting = AllowSortingEnum.None;
            FGrid1.AllowDragging = AllowDraggingEnum.None;
            FGrid1.SelectionMode = SelectionModeEnum.Row;
            FGrid1.Styles.Normal.WordWrap = true;
            string[] captions = { "타공종류", "개수", "설정 내용(mm)" };
            int[] widths = { 115, 45, 345 };
            for (int col = 0; col < captions.Length; col++)
            {
                FGrid1.Cols[col].Caption = captions[col];
                FGrid1.Cols[col].Width = widths[col];
            }
        }

        private void RefreshGrid(int selectedIndex)
        {
            FGrid1.Rows.Count = FGrid1.Rows.Fixed + _placements.Count;
            for (int i = 0; i < _placements.Count; i++)
            {
                int row = FGrid1.Rows.Fixed + i;
                var placement = _placements[i];
                bool corners = placement.TypeCode == "FOUR_CORNERS";
                FGrid1[row, 0] = corners ? "사방타공" : placement.HasSideRuns() ? "요청 간격별 (좌·우)" : "요청 간격별 (상·하)";
                FGrid1[row, 1] = CountText(placement);
                FGrid1[row, 2] = PlacementText(placement);
                FGrid1.AutoSizeRow(row);
            }
            if (_placements.Count > 0)
                FGrid1.Row = FGrid1.Rows.Fixed + Math.Min(selectedIndex, _placements.Count - 1);
            UpdateButtons();
        }

        private string CountText(HoleMarkPlacement placement)
        {
            if (placement.TypeCode == "FOUR_CORNERS") return placement.Edges.GetCornerCount().ToString();
            if (!placement.HasRuns() && (_width <= 0 || _height <= 0)) return "-";
            try { return placement.GetMarkCount((double)_width, (double)_height, (double)_imageWidth, (double)_imageHeight).ToString(); }
            catch (ArgumentException) { return "확인 필요"; }
        }

        private static string PlacementText(HoleMarkPlacement placement)
        {
            if (placement.HasRuns())
                return string.Join(Environment.NewLine, new[] {
                    RunText("상단", placement.TopRun), RunText("하단", placement.BottomRun),
                    RunText("좌측", placement.LeftRun), RunText("우측", placement.RightRun)
                }.Where(line => line != null));
            var edges = placement.Edges;
            string text = $"상 {InsetText(edges.TopMm, edges.TopEnabled)} · 하 {InsetText(edges.BottomMm, edges.BottomEnabled)}"
                + $" · 좌 {InsetText(edges.LeftMm, edges.LeftEnabled)} · 우 {InsetText(edges.RightMm, edges.RightEnabled)}";
            return placement.TypeCode == "FOUR_CORNERS" ? text : text + $" · 최대간격 {placement.SpacingMm:0.###}";
        }

        private static string RunText(string name, HoleMarkRun run)
        {
            if (run == null) return null;
            string startSide = run.StartBottomMm.HasValue ? "하" : "상";
            string startHorizontalSide = run.StartRightMm.HasValue ? "우" : "좌";
            string start = $"({startHorizontalSide} {run.StartRightMm ?? run.StartLeftMm:0.###}, {startSide} {run.StartBottomMm ?? run.StartTopMm:0.###})";
            string endSide = run.EndRightMm.HasValue ? "우" : "좌";
            string endVerticalSide = run.EndBottomMm.HasValue ? "하" : "상";
            return run.Mode == "UNIFORM"
                ? $"{name} 균등 {run.Count}개 · {start} → ({endSide} {run.EndRightMm ?? run.EndLeftMm:0.###}, {endVerticalSide} {run.EndBottomMm ?? run.EndTopMm:0.###})"
                : $"{name} 고정 반대쪽까지 · {start}에서 기준 간격 {run.SpacingMm:0.###}mm";
        }

        private static string InsetText(double inset, bool enabled)
        {
            return enabled ? $"{inset:0.###}" : $"{inset:0.###} (해제)";
        }

        private void UpdateButtons()
        {
            btnAdd.Enabled = cbDeployType.SelectedIndex >= 0;
            btnAdd.ToolTipText = cbDeployType.SelectedIndex == 0 ? "사방타공 추가" : "요청 간격별 타공 추가";
            int index = FGrid1.Row - FGrid1.Rows.Fixed;
            btnRemove.Enabled = index >= 0 && index < _placements.Count;
        }

        private void AddPlacement()
        {
            HoleMarkPlacement placement;
            if (cbDeployType.SelectedIndex == 0)
            {
                using (var dialog = new Dlg타공_사방타공(_width, _height, _sourceScaleDivisor))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    placement = dialog.Placement;
                }
            }
            else if (cbDeployType.SelectedIndex == 1 || cbDeployType.SelectedIndex == 2)
                placement = ShowCustomPlacement(cbDeployType.SelectedIndex == 2);
            else return;
            if (placement == null) return;
            _placements.Add(placement);
            RefreshGrid(_placements.Count - 1);
        }

        private void RemovePlacement()
        {
            int index = FGrid1.Row - FGrid1.Rows.Fixed;
            if (index < 0 || index >= _placements.Count) return;
            _placements.RemoveAt(index);
            RefreshGrid(index);
        }

        private void EditPlacement(int index)
        {
            if (index < 0 || index >= _placements.Count) return;
            var placement = _placements[index];
            if (placement.TypeCode != "CUSTOM_SPACING") return;
            if (!placement.HasRuns())
            {
                MessageBox.Show(this, "기존 최대간격 설정은 상단·하단 좌표 입력 방식으로 직접 수정할 수 없습니다. 새 배치를 추가해주세요.",
                    "요청 간격별 타공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var updated = ShowCustomPlacement(placement.HasSideRuns(), placement);
            if (updated == null) return;
            _placements[index] = updated;
            RefreshGrid(index);
        }

        private HoleMarkPlacement ShowCustomPlacement(bool vertical, HoleMarkPlacement existing = null)
        {
            bool usesImage = existing?.ImageCoordinates ?? true;
            decimal width = usesImage ? _imageWidth : _width;
            decimal height = usesImage ? _imageHeight : _height;
            if (vertical)
            {
                using (var dialog = new Dlg타공_커스텀타공_좌우(width, height, existing, _sourceScaleDivisor))
                    return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Placement : null;
            }
            using (var dialog = new Dlg타공_커스텀타공_상단하단(width, height, existing, _sourceScaleDivisor))
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Placement : null;
        }
    }
}
