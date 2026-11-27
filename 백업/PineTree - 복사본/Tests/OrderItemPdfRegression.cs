using PineTree.PdfEngine;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

internal static class OrderItemPdfRegression
{
    private static int checks;
    private static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name).SetValue(target, value, null); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
    private static void Pump(Func<bool> ready)
    {
        var watch=Stopwatch.StartNew();
        while (!ready()) { Application.DoEvents(); Thread.Sleep(20); if (watch.Elapsed.TotalSeconds>90) throw new TimeoutException(); }
    }
    private static void Number(object form, string name, decimal value) { Set(Field(form,name),"Value",value); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            using(var form=new DialogOrderItem())
            {
                var s=(PdfGenerationSettings)Call(form,"ReadPdfGenerationSettings");
                Check(s.TargetWidthMm==600 && s.TargetHeightMm==1800 && s.EdgeTopMm==2 && s.LineWidthMm==.18,"visible defaults");
                Check(!s.MakeCutline && !s.MakeHole,"designer unchecked flags are respected");
                foreach(var name in new[]{"tbTargetWidth","tbEdgeTop","tbLineWidth","tbMarkLeftRight","tbHoleSpacing"})
                    Set(Field(form,name),"ValueIsNull",true);
                s=(PdfGenerationSettings)Call(form,"ReadPdfGenerationSettings");s.Validate();
                Check(s.TargetWidthMm==600 && s.EdgeTopMm==2 && s.LineWidthMm==.18 && s.HoleSpacingMm==500,"empty inputs use defaults");
                Number(form,"tbTargetWidth",600);Number(form,"tbTargetHeight",1800);
                Number(form,"tbEdgeTop",2);Number(form,"tbLineWidth",.18m);Number(form,"tbMarkLeftRight",20);Number(form,"tbHoleSpacing",500);
                Set(Field(form,"chkMakeCutline"),"Checked",true);Set(Field(form,"chkMakeHole"),"Checked",true);
                Set(Field(form,"cbHoleType"),"SelectedOption",Field(form,"inputOption5"));
                s=(PdfGenerationSettings)Call(form,"ReadPdfGenerationSettings");
                Check(s.GetMarkCount()==12 && Convert.ToInt32(Get(Field(form,"tbHoleCount"),"Value"))==12,"spacing selection and calculated count");
                Set(Field(form,"cbHoleType"),"SelectedOption",Field(form,"inputOption4"));
                Set(Field(form,"cbDeploy"),"SelectedOption",Field(form,"inputOption2"));
                Check(((PdfGenerationSettings)Call(form,"ReadPdfGenerationSettings")).FitMode=="COVER","combo option identity mapping");
                Set(Field(form,"cbDeploy"),"SelectedOption",Field(form,"inputOption1"));
                Set(Field(form,"tbColorName"),"Text","  ");
                Check(((PdfGenerationSettings)Call(form,"ReadPdfGenerationSettings")).SpotColorName=="thru","blank spot name default");
                Set(Field(form,"tbColorName"),"Text","thru");
                Set(Field(form,"tbSourceFile"),"Text",Path.GetFullPath(args[0]));
                Pump(()=>Field(form,"_sourceDetails")!=null || Convert.ToString(Get(Field(form,"tbFileType"),"Text")).Contains("확인 필요"));
                Check(Field(form,"_sourceDetails")!=null,"async source inspection");
                s=(PdfGenerationSettings)Call(form,"ReadPdfGenerationSettings");
                var task=(Task<OrderDocumentResult>)Call(form,"GeneratePdfToPathAsync",Path.GetFullPath(args[1]),s);
                Check(!(bool)Get(Field(form,"tbTargetWidth"),"Enabled") && !(bool)Get(Field(form,"btnOK"),"Enabled"),"generation freezes inputs");
                Pump(()=>task.IsCompleted);
                var result=task.GetAwaiter().GetResult();
                Check(Math.Abs(result.OutputWidthMm-604)<.0001 && Math.Abs(result.OutputHeightMm-1804)<.0001 && result.MarkCount==4,"UI settings reach worker");
                Check(Convert.ToString(Get(Field(form,"inputTextBox2"),"Text"))==Path.GetFullPath(args[1]),"output path binding");
                Check((bool)Get(Field(form,"btnPreview2"),"Enabled"),"output preview enabled");
                form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);form.ShowInTaskbar=false;
                Set(Field(form,"c1DockingTab1"),"SelectedIndex",1);form.Show();Application.DoEvents();
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.ChangeExtension(args[1],".order-window.png"));}
                Number(form,"tbTargetWidth",700);
                Check(string.IsNullOrEmpty(Convert.ToString(Get(Field(form,"inputTextBox2"),"Text"))) && !(bool)Get(Field(form,"btnPreview2"),"Enabled"),"changed settings invalidate stale output path");
                Check(File.Exists(args[1]),"invalidation preserves generated file");
            }
            Console.WriteLine("PASS: order-item PDF integration checks: "+checks);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
