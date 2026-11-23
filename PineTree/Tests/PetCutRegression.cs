using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Collections;
internal static class PetCutRegression
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
    static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks++;}
    [STAThread] static int Main(string[] args)
    {
        try {
            var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var settingType=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
            var setting=settingType.GetMethod("Create").Invoke(null,new object[]{"CUT"});
            using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder.UcOrder수성_그룹B",true))) {
                using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",setting)) {
                    Check(dialog.GetType().Name=="Dlg후가공공통_재단선","PET factory opens shared cut dialog");
                    foreach(var name in new[]{"tbTop","tbBottom","tbLeft","tbRight"})
                        Check(Convert.ToDecimal(Get(Field(dialog,name),"Value"))==0m,"default 0mm: "+name);
                    Set(Field(dialog,"tbTop"),"Value",3m);Set(Field(dialog,"tbBottom"),"Value",5m);
                    Set(Field(dialog,"tbLeft"),"Value",7m);Set(Field(dialog,"tbRight"),"Value",11m);
                    Set(Field(dialog,"tbLineWidth"),"Value",.25m);
                    Check((bool)Get(Field(dialog,"tbColorName"),"Enabled"),"default spot name is editable");
                    Set(Field(dialog,"cbSpot"),"SelectedIndex",1);
                    Check(!(bool)Get(Field(dialog,"tbColorName"),"Enabled"),"Black disables the spot name");
                    dialog.StartPosition=FormStartPosition.Manual;dialog.Left=-4000;dialog.Top=-4000;dialog.ShowInTaskbar=false;dialog.Show();Application.DoEvents();
                    ((IButtonControl)Field(dialog,"btnOK")).PerformClick();
                    setting=Get(dialog,"Setting");Check(setting!=null,"OK returns setting");
                }
                Call(editor,"CommitPostProcess",-1,setting);
                var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
                var json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
                var restored=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
                using(var other=(UserControl)Activator.CreateInstance(editor.GetType())) {
                    other.GetType().GetMethod("RestoreEditorState").Invoke(other,new object[]{restored});
                    var list=(IList)Field(other,"_postProcesses");Check(list.Count==1,"JSON restores one cut");
                    using(var dialog=(Form)Call(other,"CreatePostProcessDialog",list[0])) {
                        Check((int)Get(Field(dialog,"cbSpot"),"SelectedIndex")==1,"Black selection survives reload");
                        foreach(var pair in new[]{new object[]{"tbTop",3m},new object[]{"tbBottom",5m},new object[]{"tbLeft",7m},new object[]{"tbRight",11m},new object[]{"tbLineWidth",.25m}})
                            Check(Convert.ToDecimal(Get(Field(dialog,(string)pair[0]),"Value"))==(decimal)pair[1],"dialog restores "+pair[0]);
                        ((IButtonControl)Field(dialog,"btnCancel")).PerformClick();
                        Check(Get(dialog,"Setting")==null,"cancel returns no changes");
                    }
                    Set(Field(other,"tbTargetWidth"),"Value",724m);Set(Field(other,"tbTargetHeight"),"Value",2154m);
                    var pdf=Call(other,"ReadPdfGenerationSettings");
                    Check((bool)Get(pdf,"MakeCutline"),"cut enabled");
                    Check((string)Get(pdf,"CutlineColorMode")=="BLACK","PET sends Black to the PDF engine");
                    Check((bool)Get(pdf,"CutInsetsFromPage"),"Group B measures explicit insets from the page edge");
                    foreach(var pair in new[]{new object[]{"CutInsetTopMm",3d},new object[]{"CutInsetBottomMm",5d},new object[]{"CutInsetLeftMm",7d},new object[]{"CutInsetRightMm",11d},new object[]{"LineWidthMm",.25d}})
                        Check(Convert.ToDouble(Get(pdf,(string)pair[0]))==(double)pair[1],"PDF receives "+pair[0]);
                    Check((double)Get(pdf,"EdgeTopMm")==0 && (double)Get(pdf,"TargetWidthMm")==724,"no page expansion or target resize");
                    pdf.GetType().GetMethod("Validate").Invoke(pdf,null);
                    Set(pdf,"CutInsetLeftMm",724d);
                    bool invalid=false;try{pdf.GetType().GetMethod("Validate").Invoke(pdf,null);}catch(TargetInvocationException ex){invalid=ex.InnerException is ArgumentException;}
                    Check(invalid,"C# rejects excessive inset");
                    var grid=Field(other,"FGrid1");var indexer=grid.GetType().GetProperty("Item",new[]{typeof(int),typeof(string)});
                    var summary=(string)indexer.GetValue(grid,new object[]{1,"Summary"});Check(summary.Contains("상 3")&&summary.Contains("우 11"),"grid includes insets");
                }
            }
            Console.WriteLine("PASS "+checks+" PET cutting UI/state/settings checks");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
