from pathlib import Path
def edit(path,fn):
 p=Path(path);s=p.read_text(encoding='utf-8-sig');p.write_text(fn(s),encoding='utf-8-sig' if p.suffix=='.cs' else 'utf-8')
sides=['Top','Bottom','Left','Right']
p=Path('UserControlsOrder/Dlg페트_재단.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('tbLineWidth.Enabled = !draft.Parameters.FromSource;', '\n'.join('            '+line for line in [
 'PostProcessDialogBinding.Number(tb'+side+', draft.Parameters.CutInset'+side+'Mm ?? (draft.Parameters.FromSource ? 0m : 2m));' for side in sides]).lstrip()+'\n            tbLineWidth.Enabled = tbTop.Enabled = tbBottom.Enabled = tbLeft.Enabled = tbRight.Enabled = !draft.Parameters.FromSource;\n            foreach (var input in new[] { tbTop, tbBottom, tbLeft, tbRight })\n                input.ToolTipText = "단위: mm. 출력 페이지 가장자리에서 안쪽으로 재단선까지의 거리. 이미지와 페이지 크기는 유지됩니다.";')
s=s.replace('value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);','value.Parameters.LineWidthMm = PostProcessDialogBinding.Read(tbLineWidth);\n'+ '\n'.join('                value.Parameters.CutInset'+side+'Mm = '+ 'PostProcessDialogBinding.Read(tb'+side+');' for side in sides))
s=s.replace('완성 영역 네 변을 연결하는 사각 외곽선','입력한 여백 안쪽의 사각 재단선')
p.write_text(s,encoding='utf-8-sig')
p=Path('Classes/OrderPostProcessSetting.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public decimal LineWidthMm { get; set; } = .18m;', 'public decimal LineWidthMm { get; set; } = .18m;\n        // PET: inset from the existing output page; null preserves legacy banner geometry.\n'+ '\n'.join('        public decimal? CutInset'+side+'Mm { get; set; }' for side in sides))
s=s.replace('var p = Parameters;\n            if (ProcessCode == "CUT")', 'var p = Parameters;\n            if (ProcessCode == "CUT")',1)
needle='throw new ArgumentException("재단선은 0 초과 10mm 이하의 선두께와 별색 이름(100자 이내)을 입력해주세요.");'
s=s.replace(needle,needle+'\n                foreach (decimal? inset in new[] { p.CutInsetTopMm, p.CutInsetBottomMm, p.CutInsetLeftMm, p.CutInsetRightMm })\n                    if (inset.HasValue && (inset.Value < 0 || inset.Value > 100000))\n                        throw new ArgumentException("재단 여백은 0 이상 100,000mm 이하로 입력해주세요.");')
needle='            if (ProcessCode == "CUT") return string.Format('
s=s.replace(needle,'            if (ProcessCode == "CUT" && p.CutInsetTopMm.HasValue) return string.Format("사방 재단 / {0:0.###}mm / 여백 상 {1:0.###}·하 {2:0.###}·좌 {3:0.###}·우 {4:0.###}mm{5}",\n                p.LineWidthMm, p.CutInsetTopMm ?? 0m, p.CutInsetBottomMm ?? 0m, p.CutInsetLeftMm ?? 0m, p.CutInsetRightMm ?? 0m, p.FromSource ? " (원본 포함)" : "");\n'+needle)
p.write_text(s,encoding='utf-8-sig')
p=Path('UserControlsOrder/UcOrder페트.PostProcesses.cs');s=p.read_text(encoding='utf-8-sig').replace('case "CUT": return new Dlg현수막_재단선(setting);','case "CUT": return new Dlg페트_재단(setting);')
needle='settings.LineWidthMm = (double)p.LineWidthMm; settings.SpotColorName = p.SpotColorName;'
s=s.replace(needle,needle+'\n'+ '\n'.join('                    settings.CutInset'+side+'Mm = (double)(p.CutInset'+side+'Mm ?? (p.FromSource ? 0m : 2m));' for side in sides))
p.write_text(s,encoding='utf-8-sig')
p=Path('../PineTree.PdfEngine/OrderDocumentService.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public bool MakeCutline { get; set; }','public bool MakeCutline { get; set; }\n'+ '\n'.join('        public double CutInset'+side+'Mm { get; set; }' for side in sides))
s=s.replace('EdgeLeftMm, EdgeRightMm, LineWidthMm,', 'EdgeLeftMm, EdgeRightMm, CutInsetTopMm, CutInsetBottomMm, CutInsetLeftMm, CutInsetRightMm, LineWidthMm,')
needle='            if (MakeCutline && (LineWidthMm'
s=s.replace(needle,'            if (MakeCutline && (CutInsetTopMm < 0 || CutInsetBottomMm < 0 || CutInsetLeftMm < 0 || CutInsetRightMm < 0\n                || CutInsetLeftMm + CutInsetRightMm >= FinishedWidthMm() || CutInsetTopMm + CutInsetBottomMm >= FinishedHeightMm()))\n                throw new ArgumentException("재단 여백은 0 이상이며 좌우·상하 여백의 합은 출력 폭·길이보다 작아야 합니다.");\n'+needle)
p.write_text(s,encoding='utf-8-sig')
p=Path('../PineTree.PdfEngine/PdfWorker/pdf_worker.py');s=p.read_text(encoding='utf-8')
s=s.replace('"MakeCutline": True, "LineWidthMm": .18,','"CutInsetTopMm": 0, "CutInsetBottomMm": 0, "CutInsetLeftMm": 0, "CutInsetRightMm": 0,\n         "MakeCutline": True, "LineWidthMm": .18,')
s=s.replace('"LineWidthMm", "MarkLeftRightMm",','"CutInsetTopMm", "CutInsetBottomMm", "CutInsetLeftMm", "CutInsetRightMm",\n                "LineWidthMm", "MarkLeftRightMm",')
needle='    if s["MakeCutline"] and (not 0 < s["LineWidthMm"]'
s=s.replace(needle,'    if s["MakeCutline"] and (min(s[k] for k in ("CutInsetTopMm", "CutInsetBottomMm", "CutInsetLeftMm", "CutInsetRightMm")) < 0\n            or s["CutInsetLeftMm"]+s["CutInsetRightMm"] >= finished_width\n            or s["CutInsetTopMm"]+s["CutInsetBottomMm"] >= finished_height):\n        raise ValueError("재단 여백은 0 이상이며 좌우·상하 합은 출력 폭·길이보다 작아야 합니다.")\n'+needle)
s=s.replace('    if s["MakeCutline"]:\n        instructions.append', '    if s["MakeCutline"]:\n        cut_left, cut_bottom = left+s["CutInsetLeftMm"]*MM, bottom+s["CutInsetBottomMm"]*MM\n        cut_width = tw-(s["CutInsetLeftMm"]+s["CutInsetRightMm"])*MM\n        cut_height = th-(s["CutInsetTopMm"]+s["CutInsetBottomMm"])*MM\n        page.trimbox = RectangleObject([cut_left, cut_bottom, cut_left+cut_width, cut_bottom+cut_height])\n        instructions.append')
s=s.replace('{left:.8f} {bottom:.8f} {tw:.8f} {th:.8f} re S Q")','{cut_left:.8f} {cut_bottom:.8f} {cut_width:.8f} {cut_height:.8f} re S Q")')
p.write_text(s,encoding='utf-8')
