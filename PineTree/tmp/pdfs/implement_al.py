from pathlib import Path
p=Path('Classes/OrderPostProcessSetting.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('"CUT", "WOOD_TB" };','"CUT", "WOOD_TB", "AL_SCROLL" };',1).replace('"재단", "상하각목" };','"재단", "상하각목", "족자및쫄대" };',1)
s=s.replace('int index = Array.IndexOf(Names, name);','int index = Array.FindIndex(Names, item => string.Concat(item.Where(c => !char.IsWhiteSpace(c))) == string.Concat((name ?? string.Empty).Where(c => !char.IsWhiteSpace(c))));')
s=s.replace('case "CUT": return new[] { "NORMAL", "EXACT" };','case "CUT": return new[] { "NORMAL", "EXACT" };\n                case "AL_SCROLL": return new[] { "TOP_BOTTOM" };')
s=s.replace('if (ProcessCode == "CUT")\n            {','if (ProcessCode == "CUT" || ProcessCode == "AL_SCROLL")\n            {',1)
s=s.replace('            else if (ProcessCode == "HOLE")','            else if (ProcessCode == "HOLE")',1)
needle='            else if (ProcessCode == "HOLE")'
# AL margin validation follows common cut validation, not a separate else branch.
s=s.replace('                foreach (decimal? inset in new[]', '                if (ProcessCode == "AL_SCROLL" && (p.ScrollTopMm < 0 || p.ScrollBottomMm < 0 || p.ScrollTopMm > 100000 || p.ScrollBottomMm > 100000))\n                    throw new ArgumentException("족자및쫄대 상·하 여백은 0 이상 100,000mm 이하로 입력해주세요.");\n                foreach (decimal? inset in new[]',1)
s=s.replace('            if (ProcessCode == "CUT" && p.CutInsetTopMm.HasValue)', '            if (ProcessCode == "AL_SCROLL") return string.Format("상 여백 {0:0.###}·하 여백 {1:0.###}mm / 재단선 {2:0.###}mm / 별색 {3}", p.ScrollTopMm, p.ScrollBottomMm, p.LineWidthMm, p.SpotColorName);\n            if (ProcessCode == "CUT" && p.CutInsetTopMm.HasValue)',1)
s=s.replace('item.ProcessCode == "WOOD_TB") && !single.Add', 'item.ProcessCode == "WOOD_TB" || item.ProcessCode == "AL_SCROLL") && !single.Add')
s=s.replace('재단·타공·좌우각목·상하각목은 각각','재단·타공·좌우각목·상하각목·족자및쫄대는 각각')
s=s.replace('public decimal LineWidthMm { get; set; } = .18m;', 'public decimal ScrollTopMm { get; set; } = 5m;\n        public decimal ScrollBottomMm { get; set; } = 5m;\n        public decimal LineWidthMm { get; set; } = .18m;')
p.write_text(s,encoding='utf-8-sig')
p=Path('UserControlsOrder/Dlg페트_AL족자.cs')
p.write_text('''using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControlsOrder
{
    public partial class Dlg페트_AL족자 : Form, IPostProcessDialog
    {
        public OrderPostProcessSetting Setting { get; private set; }
        public Dlg페트_AL족자() : this(OrderPostProcessSetting.Create("AL_SCROLL")) { }
        public Dlg페트_AL족자(OrderPostProcessSetting setting)
        {
            InitializeComponent();
            var draft = setting.Copy();
            PostProcessDialogBinding.Number(tbLineWidth, draft.Parameters.LineWidthMm, 10);
            PostProcessDialogBinding.Select(cbSpot, new[] { "SPOT" }, draft.Parameters.ColorMode);
            tbColorName.Text = draft.Parameters.SpotColorName;
            PostProcessDialogBinding.Number(tbTop, draft.Parameters.ScrollTopMm);
            PostProcessDialogBinding.Number(tbBottom, draft.Parameters.ScrollBottomMm);
            tbLineWidth.ToolTipText = "단위: mm。追加した余白を含むページの端から2mm内側の再断線";
            tbLineWidth.ToolTipText = "단위: mm. 추가 여백을 포함한 페이지 가장자리에서 2mm 안쪽에 재단선을 표시합니다.";
            tbColorName.ToolTipText = "커팅용 별색 채널 이름. 고객사 샘플: thru";
            tbTop.ToolTipText = tbBottom.ToolTipText = "단위: mm. 확대된 원본 위·아래에 추가할 흰 여백. 이미지 크기는 유지됩니다.";
            PostProcessDialogBinding.Bind(this, btnOK, btnCancel, () => {
                var value = draft.Copy();
                value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);
                value.Parameters.ColorMode = PostProcessDialogBinding.Selected(cbSpot, new[] { "SPOT" });
                value.Parameters.SpotColorName = tbColorName.Text.Trim();
                value.Parameters.ScrollTopMm = PostProcessDialogBinding.Read(tbTop);
                value.Parameters.ScrollBottomMm = PostProcessDialogBinding.Read(tbBottom);
                return value;
            }, value => Setting = value);
        }
    }
}
'''.replace('            tbLineWidth.ToolTipText = "단위: mm。追加した余白を含むページの端から2mm内側の再断線";\n',''),encoding='utf-8-sig')
p=Path('UserControlsOrder/UcOrder페트.PostProcesses.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('case "CUT": return new Dlg페트_재단(setting);','case "CUT": return new Dlg페트_재단(setting);\n                case "AL_SCROLL": return new Dlg페트_AL족자(setting);')
s=s.replace('code == "WOOD_TB") && existing','code == "WOOD_TB" || code == "AL_SCROLL") && existing')
s=s.replace('            return result;\n        }\n        private decimal RopeGuideThicknessMm()', '            var scroll = _postProcesses.Find(item => item.ProcessCode == "AL_SCROLL");\n            if (scroll != null) result += scroll.Parameters.ScrollTopMm + scroll.Parameters.ScrollBottomMm;\n            return result;\n        }\n        private decimal RopeGuideThicknessMm()')
s=s.replace('        {\n            foreach(var item in _postProcesses) {\n                var p = item.Parameters;','''        {
            var scroll = _postProcesses.Find(item => item.ProcessCode == "AL_SCROLL");
            if (scroll != null) {
                if (_sourceHasCutline) throw new ArgumentException("족자및쫄대는 재단선이 없는 원본파일을 선택해주세요.");
                var p = scroll.Parameters;
                settings.MakeAlScroll = true;
                settings.ScrollTopMm = (double)p.ScrollTopMm; settings.ScrollBottomMm = (double)p.ScrollBottomMm;
                settings.MakeCutline = true; settings.LineWidthMm = (double)p.LineWidthMm; settings.SpotColorName = p.SpotColorName;
                settings.CutInsetTopMm = settings.CutInsetBottomMm = settings.CutInsetLeftMm = settings.CutInsetRightMm = 2;
            }
            // A separate CUT row overrides the scroll's default cut settings regardless of row order.
            foreach(var item in _postProcesses) {
                var p = item.Parameters;''',1)
p.write_text(s,encoding='utf-8-sig')
p=Path('../PineTree.PdfEngine/OrderDocumentService.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public bool MakeCutline { get; set; }','public bool MakeAlScroll { get; set; }\n        public double ScrollTopMm { get; set; } = 5;\n        public double ScrollBottomMm { get; set; } = 5;\n        public bool MakeCutline { get; set; }')
s=s.replace('double result = TargetHeightMm + (MakeWoodTopBottom ? 2 * WoodTopBottomExtraMm : 0);','double result = TargetHeightMm + (MakeWoodTopBottom ? 2 * WoodTopBottomExtraMm : 0) + (MakeAlScroll ? ScrollTopMm + ScrollBottomMm : 0);')
s=s.replace('EdgeLeftMm, EdgeRightMm, CutInsetTopMm,','EdgeLeftMm, EdgeRightMm, ScrollTopMm, ScrollBottomMm, CutInsetTopMm,')
s=s.replace('            if (MakeWood && (WoodExtraMm < 0', '            if (MakeAlScroll && (ScrollTopMm < 0 || ScrollBottomMm < 0))\n                throw new ArgumentException("족자및쫄대 상·하 여백은 0 이상이어야 합니다.");\n            if (MakeWood && (WoodExtraMm < 0',1)
p.write_text(s,encoding='utf-8-sig')
p=Path('../PineTree.PdfEngine/PdfWorker/pdf_worker.py');s=p.read_text(encoding='utf-8')
s=s.replace('"CutInsetTopMm": 0,', '"MakeAlScroll": False, "ScrollTopMm": 5, "ScrollBottomMm": 5,\n         "CutInsetTopMm": 0,',1)
s=s.replace('"CutInsetTopMm", "CutInsetBottomMm", "CutInsetLeftMm", "CutInsetRightMm",\n                "LineWidthMm"','"ScrollTopMm", "ScrollBottomMm", "CutInsetTopMm", "CutInsetBottomMm", "CutInsetLeftMm", "CutInsetRightMm",\n                "LineWidthMm"',1)
s=s.replace('for key in ("MakeCutline", "MakeHole",','for key in ("MakeAlScroll", "MakeCutline", "MakeHole",',1)
s=s.replace('    finished_height = s["TargetHeightMm"] +', '    if s["MakeAlScroll"] and min(s["ScrollTopMm"], s["ScrollBottomMm"]) < 0:\n        raise ValueError("족자및쫄대 상·하 여백은 0 이상이어야 합니다.")\n    finished_height = (s["ScrollTopMm"]+s["ScrollBottomMm"] if s["MakeAlScroll"] else 0) + s["TargetHeightMm"] +',1)
# Height also affects mark placement; locate independent mark function calculation.
s=s.replace('height = s["TargetHeightMm"] +', 'height = (s["ScrollTopMm"]+s["ScrollBottomMm"] if s["MakeAlScroll"] else 0) + s["TargetHeightMm"] +')
s=s.replace('    tw, th = art_width+rope_left+rope_right, art_height+rope_bottom+rope_top','    scroll_top = s["ScrollTopMm"]*MM if s["MakeAlScroll"] else 0\n    scroll_bottom = s["ScrollBottomMm"]*MM if s["MakeAlScroll"] else 0\n    tw, th = art_width+rope_left+rope_right, art_height+rope_bottom+rope_top+scroll_top+scroll_bottom')
s=s.replace('bottom+rope_bottom+wood_y','bottom+rope_bottom+wood_y+scroll_bottom',1)
s=s.replace('if not wood_y and not s["RopeSewPasses"]:', 'if not wood_y and not s["RopeSewPasses"] and not s["MakeAlScroll"]:')
s=s.replace('if not wood and not wood_y and not s["RopeSewPasses"]:', 'if not wood and not wood_y and not s["RopeSewPasses"] and not s["MakeAlScroll"]:')
s=s.replace('+ (f" / 좌우각목 여분 각', '+ (f" / 족자및쫄대 상 {s[\'ScrollTopMm\']:g}·하 {s[\'ScrollBottomMm\']:g}mm" if s["MakeAlScroll"] else "")\n            + (f" / 좌우각목 여분 각',1)
p.write_text(s,encoding='utf-8')
