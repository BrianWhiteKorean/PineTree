using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class TopBottomWoodDefaultsRegression
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    private static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
    private static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
    private static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    private static void Num(object o,string n,decimal v){Set(Field(o,n),"Value",v);}
    private static decimal Value(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    [STAThread]private static int Main(string[] args)
    {
        try {
            Application.EnableVisualStyles();
            string path=Path.GetFullPath(args[0]);var app=Assembly.LoadFrom(path);
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
            var settingType=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
            foreach(string code in new[]{"WOOD_TB","WOOD"}) {
            string prefix=code=="WOOD"?"Wood":"WoodTopBottom";
            Func<object> create=()=>settingType.GetMethod("Create").Invoke(null,new object[]{code});
            using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder현수막",true)))
            using(var host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000,Width=1100,Height=800}) {
                host.Controls.Add(editor);host.Show();
                editor.GetType().GetField("_sourceWidthMm",F).SetValue(editor,800m);
                editor.GetType().GetField("_sourceHeightMm",F).SetValue(editor,59m);
                Call(editor,"UpdateDocumentState");
                foreach(decimal rate in new[]{1m,5.5m,10m}) {
                    Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,rate==1?"inputOption6":"inputOption7"));
                    Num(editor,"tbZoomRate",rate);
                    using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",create())) {
                        Check(Value(dialog,"tbExtraLine")==8*rate && Value(dialog,"tbDesignLine")==0,"scaled spacing default "+rate);
                        Check(Value(dialog,"tbExtraLineThickness")==.6m*rate && Value(dialog,"tbDesignLineThickness")==.6m*rate,"scaled thickness defaults "+rate);
                        Check(!(bool)Get(Field(dialog,"tbDesignLineThickness"),"Enabled"),"unchecked design thickness disabled");
                    }
                }
                // Select fixed-size without showing its user guidance message in this test.
                editor.GetType().GetField("_updatingInputs",F).SetValue(editor,true);
                Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption13"));
                editor.GetType().GetField("_updatingInputs",F).SetValue(editor,false);
                Num(editor,"tbTargetWidth",4000);
                object accepted;
                using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",create())) {
                    Check(Value(dialog,"tbExtraLine")==40 && Value(dialog,"tbExtraLineThickness")==3,"fixed-size uses actual width/source scale");
                    dialog.Shown+=delegate {
                        Set(Field(dialog,"chkDesignLine"),"Checked",true);
                        Check((bool)Get(Field(dialog,"tbDesignLineThickness"),"Enabled"),"design thickness follows checkbox");
                        Num(dialog,"tbExtraLine",52);Num(dialog,"tbDesignLine",2);
                        Num(dialog,"tbExtraLineThickness",3.3m);Num(dialog,"tbDesignLineThickness",1.2m);
                        Field(dialog,"btnOK").GetType().GetMethod("PerformClick").Invoke(Field(dialog,"btnOK"),null);
                    };
                    Check(dialog.ShowDialog(host)==DialogResult.OK,"settings accepted");accepted=Get(dialog,"Setting");
                }
                Call(editor,"CommitPostProcess",-1,accepted);
                var pdf=Call(editor,"ReadPdfGenerationSettings");
                Check((double)Get(pdf,prefix+"ExtraMm")==52 && (double)Get(pdf,prefix+"DesignOffsetMm")==2,"spacing reaches parent PDF settings");
                Check((double)Get(pdf,prefix+"ExtraLineThicknessMm")==3.3 && (double)Get(pdf,prefix+"DesignLineThicknessMm")==1.2,"separate thickness reaches PDF settings");
                string summary=(string)accepted.GetType().GetMethod("Summary").Invoke(accepted,new object[]{4000m,295m});
                Check(summary.Contains("3.3mm") && summary.Contains("1.2mm"),"parent summary includes both thicknesses");
                var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
                var json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
                var decoded=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
                using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())) {
                    restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{decoded});
                    var items=(System.Collections.IList)Field(restored,"_postProcesses");
                    using(var dialog=(Form)Call(restored,"CreatePostProcessDialog",items[0])) {
                        Check(Value(dialog,"tbExtraLine")==52 && Value(dialog,"tbDesignLine")==2,"custom spacing restored");
                        Check(Value(dialog,"tbExtraLineThickness")==3.3m && Value(dialog,"tbDesignLineThickness")==1.2m,"custom thickness restored rather than reset to defaults");
                    }
                }
                var invalid=create();var parameters=Get(invalid,"Parameters");Set(parameters,"ExtraLineThicknessMm",(decimal?)0m);
                bool rejected=false;try{settingType.GetMethod("Validate").Invoke(invalid,null);}catch(TargetInvocationException ex){rejected=ex.InnerException is ArgumentException;}
                Check(rejected,"zero thickness rejected");
            }
            }
            Console.WriteLine("PASS: both wood dialogs scale defaults/parent/state/PDF settings checks: "+checks);return 0;
        } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
