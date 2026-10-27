using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class PostProcessRegression
{
    private const BindingFlags F = BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static Assembly app;
    private static Type settingType;
    private static object Field(object o,string name) { return o.GetType().GetField(name,F).GetValue(o); }
    private static object Get(object o,string name) { return o.GetType().GetProperty(name).GetValue(o,null); }
    private static void Set(object o,string name,object value) { o.GetType().GetProperty(name).SetValue(o,value,null); }
    private static object Call(object o,string name,params object[] args) { return o.GetType().GetMethod(name,F).Invoke(o,args); }
    private static void Check(bool value,string name) { if(!value)throw new Exception(name); checks++; }
    private static object Create(string code) { return settingType.GetMethod("Create").Invoke(null,new object[]{code}); }
    private static string Json(object state) { return (string)state.GetType().GetMethod("ToJson").Invoke(state,null); }
    private static object Capture(object editor) { return editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null); }
    private static void Restore(object editor,object state) { editor.GetType().GetMethod("RestoreEditorState").Invoke(editor,new[]{state}); }
    private static void Number(object owner,string field,decimal value) { Set(Field(owner,field),"Value",value); }
    private static void Click(object dialog,string name) { var button=Field(dialog,name);button.GetType().GetMethod("PerformClick").Invoke(button,null); }
    private static object Cell(object grid,int row,string column) { return grid.GetType().GetProperty("Item",new[]{typeof(int),typeof(string)}).GetValue(grid,new object[]{row,column}); }
    private static object Params(object setting) { return Get(setting,"Parameters"); }
    private static async Task Run(Form host,UserControl editor,string source,string folder)
    {
        var list=(IList)Field(editor,"_postProcesses");var grid=(Control)Field(editor,"FGrid1");
        Check((int)Get(Get(grid,"Rows"),"Count")==1 && list.Count==0,"starts with no finishing rows");
        Number(editor,"tbTargetWidth",600);Number(editor,"tbTargetHeight",1800);Number(editor,"inputNumericBox4",2);
        string[] codes={"HOLE","WOOD","POLE_SEW","HEM_SEW","BAND_SEW","ROPE_SEW","CUT"};
        foreach(string code in codes) {
            var initial=Create(code);
            using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",initial)) {
                if(code=="CUT") { Number(dialog,"tbLineWidth",.27m);Set(Field(dialog,"tbColorName"),"Text","RegressionCut");Set(Field(dialog,"cbCutType"),"SelectedIndex",1); }
                else if(code=="WOOD") {
                    Check(!(bool)Get(Field(dialog,"tbDesignLine"),"Enabled"),"wood design input disabled until checked");
                    Number(dialog,"tbExtraLine",35);Set(Field(dialog,"chkDesignLine"),"Checked",true);Number(dialog,"tbDesignLine",10);
                    Check((bool)Get(Field(dialog,"tbDesignLine"),"Enabled"),"wood design input follows checkbox");
                }
                else if(code!="HOLE") {
                    string number=(code=="WOOD"||code=="BAND_SEW")?"tbLineWidth":"tbLength";
                    Number(dialog,number,35);Set(Field(dialog,"cbType"),"SelectedIndex",1);
                    if(code=="POLE_SEW")Set(Field(dialog,"chkCutLine"),"Checked",true);
                }
                dialog.Shown+=delegate { Click(dialog,"btnOK"); };
                Check(dialog.ShowDialog(host)==DialogResult.OK,code+" confirms");
                var selected=Get(dialog,"Setting");Check(selected!=null && (string)Get(selected,"ProcessCode")==code,code+" returns typed settings");
                if(code!="CUT"&&code!="HOLE")Check((decimal)Get(Params(initial),"DimensionMm")==0,code+" edits a copy");
                Call(editor,"CommitPostProcess",-1,selected);
            }
        }
        Check(list.Count==7 && (int)Get(Get(grid,"Rows"),"Count")==8,"all seven types appear");
        Check(((string)Get(Field(editor,"inputGroupHeader2"),"Text")).Contains("7건"),"count header");
        Check(Cell(grid,7,"Summary").ToString().Contains("0.27") && Cell(grid,3,"Summary").ToString().Contains("절개선"),"grid summarizes entered settings");
        Check(Cell(grid,3,"Summary").ToString().Contains("PDF 반영 대기"),"pending PDF processing is visible");
        using(var fullBitmap=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(fullBitmap,new Rectangle(Point.Empty,fullBitmap.Size));fullBitmap.Save(Path.Combine(folder,"finishing-all-ui.png"));}
        using(var blankDialog=(Form)Call(editor,"CreatePostProcessDialog",Create("WOOD"))) {
            blankDialog.Shown+=delegate{Set(Field(blankDialog,"tbExtraLine"),"Text",string.Empty);Click(blankDialog,"btnOK");};
            Check(blankDialog.ShowDialog(host)==DialogResult.OK && (decimal)Get(Params(Get(blankDialog,"Setting")),"ExtraLineMm")==0,"blank wood extra returns zero");
        }
        string before=Json(Capture(editor));
        using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",list[1])) {
            Number(dialog,"tbExtraLine",99);dialog.Shown+=delegate{Click(dialog,"btnCancel");};
            Check(dialog.ShowDialog(host)==DialogResult.Cancel && Get(dialog,"Setting")==null,"cancel returns no edit");
        }
        Check(Json(Capture(editor))==before,"cancel preserves saved row");
        var state=Capture(editor);var stateType=state.GetType();
        var decoded=stateType.GetMethod("FromJson").Invoke(null,new object[]{Json(state)});
        using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())) {
            Restore(restored,decoded);Check(Json(Capture(restored))==before,"no-file finishing state round trip");
            Call(restored,"RemovePostProcess",1);Check(list.Count==7,"different order items have independent settings");
        }
        object settings=Call(editor,"ReadPdfGenerationSettings");
        Check((bool)Get(settings,"MakeCutline") && (bool)Get(settings,"MakeHole") && (double)Get(settings,"LineWidthMm")==.27,"list drives PDF cut and holes");
        Check((bool)Get(settings,"MakeWood") && (double)Get(settings,"WoodExtraMm")==35 && (bool)Get(settings,"MakeWoodDesignLine") && (double)Get(settings,"WoodDesignOffsetMm")==10,"wood dialog parameters reach PDF engine");
        Check((string)Get(settings,"SpotColorName")=="RegressionCut" && (string)Get(settings,"HoleType")=="FOUR_CORNERS","PDF parameters match dialogs");
        // Exercise actual generation via the editor, independent of its optional source thumbnail.
        editor.GetType().GetField("_sourcePath",F).SetValue(editor,source);
        editor.GetType().GetField("_sourceDetails",F).SetValue(editor,new object());
        var work=(Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"finishing.pdf"),settings);await work;
        Check(File.Exists(Path.Combine(folder,"finishing.pdf")),"real editor PDF generation");
        Check((int)Get(Get(work,"Result"),"MarkCount")==4,"PDF includes four actual hole marks");
        Check(Math.Abs((double)Get(Get(work,"Result"),"OutputWidthMm")-674)<.0001,"original bleed and output dimensions preserved");
        // Pending work must not alter the engine settings or the generated vector PDF.
        for(int i=list.Count-1;i>=0;i--) if((string)Get(list[i],"ProcessCode")!="HOLE" && (string)Get(list[i],"ProcessCode")!="CUT" && (string)Get(list[i],"ProcessCode")!="WOOD")Call(editor,"RemovePostProcess",i);
        var onlyPdfSettings=Call(editor,"ReadPdfGenerationSettings");
        Check((bool)Get(onlyPdfSettings,"MakeCutline") && (bool)Get(onlyPdfSettings,"MakeHole"),"removing pending work retains active PDF operations");
        await (Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"finishing-only.pdf"),onlyPdfSettings);
        using(var bitmap=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"finishing-ui.png"));}
        // Custom-spacing geometry remains independent of the optional designer input field.
        Set(Params(list[0]),"TypeCode","CUSTOM_SPACING");Set(Params(list[0]),"HoleSpacingMm",500m);
        object spaced=Call(editor,"ReadPdfGenerationSettings");
        Check((string)Get(spaced,"HoleType")=="CUSTOM_SPACING" && (double)Get(spaced,"HoleSpacingMm")==500,"custom spacing maps to PDF engine");
        await (Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"finishing-spaced.pdf"),spaced);
        // Restore complete list, then delete the actual second row through its button hit target.
        editor.GetType().GetField("_sourcePath",F).SetValue(editor,null);
        editor.GetType().GetField("_sourceDetails",F).SetValue(editor,null);
        Restore(editor,decoded);
        var rectangle=(Rectangle)grid.GetType().GetMethod("GetCellRect",new[]{typeof(int),typeof(int)}).Invoke(grid,new object[]{2,3});
        grid.GetType().GetMethod("OnMouseUp",F).Invoke(grid,new object[]{new MouseEventArgs(MouseButtons.Left,1,rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2,0)});
        Check(list.Count==6 && (string)Get(list[1],"ProcessCode")=="POLE_SEW" && (int)Cell(grid,2,"SeqNo")==2,"row delete button removes only clicked row and renumbers");
        Check(((string)Get(Field(editor,"inputGroupHeader2"),"Text")).Contains("6건"),"delete updates count");
        // Old JSON must still become finishing rows, but an explicitly empty list stays empty.
        object legacy=Activator.CreateInstance(stateType);Set(legacy,"MakeHole",true);Set(legacy,"MakeCutline",true);Set(legacy,"SpotColorName","OldCut");
        using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())) {
            Restore(restored,legacy);Check(((IList)Field(restored,"_postProcesses")).Count==2,"legacy cut and hole migrate");
            Call(restored,"RemovePostProcess",1);Call(restored,"RemovePostProcess",0);
            var empty=Capture(restored);Set(empty,"MakeHole",true);
            Restore(restored,empty);Check(((IList)Field(restored,"_postProcesses")).Count==0,"empty list never revives old flags");
        }
        await RunCustomerSample(host,editor.GetType(),folder);
        await RunCustomerWood(host,editor.GetType(),folder);
        await RunTopBottom(host,editor.GetType(),folder);
        Console.WriteLine("PASS: finishing dialogs/grid/roundtrip/PDF/customer sample: "+checks);
    }
    private static async Task RunCustomerSample(Form host,Type editorType,string folder)
    {
        string sample=Path.GetFullPath("../../1.요구분석/샘플파일/페트_재단선+사방타공마크_604X1804_1EA_별색.ai");
        using(var editor=(UserControl)Activator.CreateInstance(editorType)) {
            host.Controls.Add(editor);editor.Dock=DockStyle.Fill;editor.BringToFront();
            await (Task)Call(editor,"LoadSourceFileAsync",sample);
            Check(Field(editor,"_sourceDetails")!=null,"customer AI inspection");
            Check((decimal)Get(editor,"WidthMm")==60 && (decimal)Get(editor,"HeightMm")==180,"customer AI original is 1:10");
            Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption7"));Number(editor,"tbZoomRate",10);
            Check((decimal)Get(editor,"WidthMm")==600 && (decimal)Get(editor,"HeightMm")==1800,"customer scale gives final trim size");
            var cut=Create("CUT");var hole=Create("HOLE");
            Call(editor,"CommitPostProcess",-1,cut);Call(editor,"CommitPostProcess",-1,hole);
            using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",hole)) {
                Check(Field(dialog,"_spacingInput")!=null,"designer spacing field connected");
                Set(Field(dialog,"cbHoleType"),"SelectedIndex",1);
                Number(dialog,"tbHoleSpacing",500);
                Check(Convert.ToInt32(Get(Field(dialog,"tbHoleCount"),"Value"))==12,"spacing input calculates hole count");
                Set(Field(dialog,"cbHoleType"),"SelectedIndex",0);
                Check(Convert.ToInt32(Get(Field(dialog,"tbHoleCount"),"Value"))==4,"four-corner mode restores four marks");
            }
            var settings=Call(editor,"ReadPdfGenerationSettings");
            var generation=(Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"customer-pet.pdf"),settings);await generation;
            var result=Get(generation,"Result");
            Check(Math.Abs((double)Get(result,"OutputWidthMm")-604)<.0001 && Math.Abs((double)Get(result,"OutputHeightMm")-1804)<.0001,"customer output page is 604 by 1804");
            Check((int)Get(result,"MarkCount")==4,"customer four corner marks generated");
            string summary=(string)Field(editor,"_pdfSummary");
            Check(summary.Contains("604")&&summary.Contains("1804")&&summary.Contains("완성/재단"),"summary distinguishes trim and output sizes");
            var state=Capture(editor);
            using(var restored=(UserControl)Activator.CreateInstance(editorType)) {
                Restore(restored,state);
                Check((decimal)Get(restored,"WidthMm")==600 && ((IList)Field(restored,"_postProcesses")).Count==2,"customer size and finishing restore");
            }
            host.Controls.Remove(editor);
        }
    }
    private static async Task RunCustomerWood(Form host,Type editorType,string folder)
    {
        string sample=Path.GetFullPath("../../1.요구분석/1026/현수막_좌우각목_8000x600_1EA.ai");
        using(var editor=(UserControl)Activator.CreateInstance(editorType)) {
            host.Controls.Add(editor);editor.Dock=DockStyle.Fill;editor.BringToFront();
            await (Task)Call(editor,"LoadSourceFileAsync",sample);
            Check(Field(editor,"_sourceDetails")!=null,"customer wood AI inspection");
            Check((decimal)Get(editor,"WidthMm")==800 && (decimal)Get(editor,"HeightMm")==59,"customer wood source dimensions");
            foreach(string name in new[]{"tbEdgeLeft","tbEdgeRight","tbEdgeTop","tbEdgeBottom"}) Number(editor,name,0);
            using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",Create("WOOD"))) {
                Number(dialog,"tbExtraLine",8.03m);Set(Field(dialog,"chkDesignLine"),"Checked",true);Number(dialog,"tbDesignLine",0);
                dialog.Shown+=delegate{Click(dialog,"btnOK");};
                Check(dialog.ShowDialog(host)==DialogResult.OK,"customer wood dialog confirms");
                Call(editor,"CommitPostProcess",-1,Get(dialog,"Setting"));
            }
            var settings=Call(editor,"ReadPdfGenerationSettings");
            var generation=(Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"customer-wood.pdf"),settings);await generation;
            var result=Get(generation,"Result");
            Check(Math.Abs((double)Get(result,"OutputWidthMm")-816.06)<.0001 && Math.Abs((double)Get(result,"OutputHeightMm")-59)<.0001,"customer wood output size matches reference");
            var state=Capture(editor);
            using(var restored=(UserControl)Activator.CreateInstance(editorType)) {
                Restore(restored,state);var work=((IList)Field(restored,"_postProcesses"))[0];
                Check((decimal)Get(Params(work),"ExtraLineMm")==8.03m && (bool)Get(Params(work),"MakeDesignLine"),"wood margin and design checkbox round trip");
            }
            var shifted=((IList)Field(editor,"_postProcesses"))[0];Set(Params(shifted),"DesignLineMm",3m);Call(editor,"CommitPostProcess",0,shifted);
            settings=Call(editor,"ReadPdfGenerationSettings");
            var moved=(Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"customer-wood-offset.pdf"),settings);await moved;
            Check(Math.Abs((double)Get(Get(moved,"Result"),"OutputWidthMm")-816.06)<.0001,"design offset changes no output width");
            host.Controls.Remove(editor);
        }
    }
    private static object MakeSettingList(object setting)
    {
        var list=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(settingType));list.Add(setting);return list;
    }
    private static async Task RunTopBottom(Form host,Type editorType,string folder)
    {
        using(var editor=(UserControl)Activator.CreateInstance(editorType)) {
            host.Controls.Add(editor);editor.Dock=DockStyle.Fill;editor.BringToFront();
            object topOption=null;
            foreach(var option in (IEnumerable)Get(Field(editor,"cbWork"),"Items")) {
                string text=(string)Get(option,"Text");
                string code=(string)settingType.GetMethod("CodeForName").Invoke(null,new object[]{text});
                Check((string)Get(Create(code),"ProcessName")==text,"combo mapping: "+text);
                if(text=="상하각목")topOption=option;
            }
            Check(topOption!=null,"designer top-bottom option present");
            Set(Field(editor,"cbWork"),"SelectedOption",topOption);
            using(var timer=new System.Windows.Forms.Timer{Interval=50}) {
                timer.Tick+=delegate {
                    foreach(Form form in Application.OpenForms) {
                        if(form.GetType().Name!="Dialog상하각목")continue;
                        timer.Stop();Number(form,"tbExtraLine",80);
                        Check(!(bool)Get(Field(form,"tbDesignLine"),"Enabled"),"top design field starts disabled");
                        Set(Field(form,"chkDesignLine"),"Checked",true);Number(form,"tbDesignLine",20);
                        Check((bool)Get(Field(form,"tbDesignLine"),"Enabled"),"top design field toggles on");
                        Click(form,"btnOK");break;
                    }
                };
                timer.Start();Call(editor,"AddPostProcess");timer.Stop();
            }
            var items=(IList)Field(editor,"_postProcesses");
            Check(items.Count==1 && (string)Get(items[0],"ProcessCode")=="WOOD_TB" && (string)Get(Params(items[0]),"TypeCode")=="TOP_BOTTOM","actual combo Add returns top-bottom settings");
            using(var empty=(Form)Call(editor,"CreatePostProcessDialog",Create("WOOD_TB"))) {
                empty.Shown+=delegate{Set(Field(empty,"tbExtraLine"),"Text",string.Empty);Click(empty,"btnOK");};
                Check(empty.ShowDialog(host)==DialogResult.OK && (decimal)Get(Params(Get(empty,"Setting")),"ExtraLineMm")==0,"top empty margin returns zero");
            }
            string sample=Path.GetFullPath("../../1.요구분석/1026/현수막_좌우각목_8000x600_1EA.ai");
            await (Task)Call(editor,"LoadSourceFileAsync",sample);
            Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption7"));Number(editor,"tbZoomRate",10);
            foreach(string name in new[]{"tbEdgeLeft","tbEdgeRight","tbEdgeTop","tbEdgeBottom"})Number(editor,name,0);
            var settings=Call(editor,"ReadPdfGenerationSettings");
            Check((bool)Get(settings,"MakeWoodTopBottom") && (double)Get(settings,"WoodTopBottomExtraMm")==80 && (double)Get(settings,"WoodTopBottomDesignOffsetMm")==20,"top settings reach PDF with absolute mm");
            var task=(Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"top-bottom.pdf"),settings);await task;
            var result=Get(task,"Result");
            Check(Math.Abs((double)Get(result,"OutputWidthMm")-8000)<.0001 && Math.Abs((double)Get(result,"OutputHeightMm")-750)<.0001,"top margin applies after ten-times zoom");
            var wood=Create("WOOD");Set(Params(wood),"ExtraLineMm",15m);Set(Params(wood),"MakeDesignLine",true);Set(Params(wood),"DesignLineMm",2m);Call(editor,"CommitPostProcess",-1,wood);
            settings=Call(editor,"ReadPdfGenerationSettings");
            task=(Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"wood-both-axes.pdf"),settings);await task;
            result=Get(task,"Result");
            Check(Math.Abs((double)Get(result,"OutputWidthMm")-8030)<.0001 && Math.Abs((double)Get(result,"OutputHeightMm")-750)<.0001,"both wood axes combine independently");
            var state=Capture(editor);
            using(var restored=(UserControl)Activator.CreateInstance(editorType)) {
                Restore(restored,state);var list=(IList)Field(restored,"_postProcesses");
                Check(list.Count==2 && (string)Get(list[0],"ProcessCode")=="WOOD_TB" && (decimal)Get(Params(list[0]),"ExtraLineMm")==80,"top and side wood restore independently");
                Check((decimal)Call(restored,"FinishedWidthMm")==8030 && (decimal)Call(restored,"FinishedHeightMm")==750,"restored dimensions include both axes");
            }
            // Persisted pre-dialog WOOD/TOP_BOTTOM entries map to the new dedicated type.
            var legacy=Create("WOOD");Set(Params(legacy),"TypeCode","TOP_BOTTOM");Set(Params(legacy),"DimensionMm",12m);
            var copy=settingType.GetMethod("CopyList").Invoke(null,new object[]{MakeSettingList(legacy)});
            Check((string)Get(((IList)copy)[0],"ProcessCode")=="WOOD_TB","legacy top-bottom maps to dedicated code");
            host.Controls.Remove(editor);
        }
    }
    [STAThread]private static int Main(string[] args)
    {
        int result=0;
        try {
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            string dll=Path.GetFullPath(args[0]),source=Path.GetFullPath(args[1]),folder=Path.GetFullPath(args[2]);Directory.CreateDirectory(folder);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dll),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
            app=Assembly.LoadFrom(dll);settingType=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
            using(var host=new Form{Width=1200,Height=900,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000})
            using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder현수막",true))) {
                host.Controls.Add(editor);editor.Dock=DockStyle.Fill;
                host.Shown+=async delegate { try{await Run(host,editor,source,folder);}catch(Exception e){Console.Error.WriteLine(e);result=1;}finally{host.Close();} };
                Application.Run(host);
            }
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}return result;
    }
}
