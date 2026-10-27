using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
internal static class BannerOrderValuesRegression
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Finish(object editor,string code) { return editor.GetType().Assembly.GetType("PineTree.Classes.OrderPostProcessSetting").GetMethod("Create").Invoke(null,new object[]{code}); }
    private static int checks;
    private static object Field(object o, string n) { return o.GetType().GetField(n, Flags).GetValue(o); }
    private static object Call(object o, string n, params object[] args) { return o.GetType().GetMethod(n, Flags).Invoke(o, args); }
    private static object Get(object o, string n) { return o.GetType().GetProperty(n).GetValue(o, null); }
    private static void Set(object o, string n, object value) { o.GetType().GetProperty(n).SetValue(o, value, null); }
    private static void Number(object o, string n, decimal value) { Set(Field(o, n), "Value", value); }
    private static decimal Value(object o, string n) { return Convert.ToDecimal(Get(Field(o,n), "Value")); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static async Task Run(Form parent, Assembly assembly, string folder)
    {
        Call(parent, "InitOrderItemTable"); Call(parent, "InitOrderEditor"); Call(parent, "InitPreviewEvent");
        var table=(DataTable)Field(parent,"m_orderItems");
        for (int i=0;i<2;i++) {
            var item=Activator.CreateInstance(assembly.GetType("PineTree.Dialogs.DialogPriceItemOrderItem",true));
            Set(item,"PriceItemSeqNo",(long)(101+i));Set(item,"PriceItemName","Banner"+i);
            Set(item,"UnitName",i==0?"회배":"장");Set(item,"BillingBasis",i==0?"면적":"수량");Set(item,"UnitPrice",i==0?4000m:1000m);
            Call(parent,"AddOrderItem",item,null);table.Rows[i]["CategoryName"]="현수막";
        }
        var grid=Field(parent,"FGrid1");int fixedRows=(int)Get(Get(grid,"Rows"),"Fixed");
        Set(grid,"Row",fixedRows);Call(parent,"UpdateSelectedOrderEditor");
        var first=(UserControl)Field(parent,"m_orderEditor");
        foreach(string name in new[]{"tbTargetWidth","tbTargetHeight","inputNumericBox3","inputNumericBox4"}) Check(Value(first,name)==0,"initial zero: "+name);
        Check(((System.Collections.IList)Field(first,"_postProcesses")).Count==0,"initial finishing list empty");
        await (Task)Call(first,"LoadSourceFileAsync",Path.Combine(folder,"source.png"));
        Console.WriteLine("Drop values: " + Value(first,"tbTargetWidth") + " / " + Value(first,"tbTargetHeight") + " / " + Value(first,"inputNumericBox4") + " / " + Get(Field(first,"tbFileInfo"),"Text"));
        Check(Value(first,"tbTargetWidth")==51m && Value(first,"tbTargetHeight")==102m && Value(first,"inputNumericBox4")==1,"DPI dimensions and quantity from drop");
        Check((decimal)table.Rows[0]["Qty"]==1 && (decimal)table.Rows[0]["TotalAmount"]==23,"drop populates area billing and VAT");
        string sourceSize = ((decimal)Field(first,"_sourceWidthMm")).ToString("0.###") + " × " + ((decimal)Field(first,"_sourceHeightMm")).ToString("0.###");
        Number(first,"tbTargetWidth",1000);Number(first,"tbTargetHeight",2000);Number(first,"inputNumericBox4",3);
        Check((decimal)table.Rows[0]["Qty"]==3 && (decimal)table.Rows[0]["AreaQty"]==6 && (decimal)table.Rows[0]["TotalAmount"]==26400,"edited area quantity and VAT reach row");
        Check(((string)Field(first,"_pdfSummary")).Contains(sourceSize),"summary retains dropped file dimensions");
        Set(grid,"Row",fixedRows+1);Call(parent,"UpdateSelectedOrderEditor");
        var second=(UserControl)Field(parent,"m_orderEditor");
        Check(!object.ReferenceEquals(first,second) && !first.IsDisposed && Value(second,"inputNumericBox4")==0,"each item has independent editor");
        await (Task)Call(second,"LoadSourceFileAsync",Path.Combine(folder,"source.pdf"));Number(second,"inputNumericBox4",2);
        Check((decimal)table.Rows[1]["Qty"]==2 && (decimal)table.Rows[1]["TotalAmount"]==2200 && (decimal)table.Rows[0]["TotalAmount"]==26400,"piece billing does not overwrite other row");
        Set(grid,"Row",fixedRows);Call(parent,"UpdateSelectedOrderEditor");
        Check(object.ReferenceEquals(first,Field(parent,"m_orderEditor")) && Value(first,"inputNumericBox4")==3,"selection restores original editor");
        Number(first,"inputNumericBox4",4);
        Check((decimal)table.Rows[0]["TotalAmount"]==35200 && (decimal)table.Rows[1]["TotalAmount"]==2200,"quantity updates only its bound row");
        await (Task)Call(first,"LoadSourceFileAsync",Path.Combine(folder,"cutline-metadata.pdf"));
        var detected=((System.Collections.IList)Field(first,"_postProcesses"))[0];Check((string)Get(Get(detected,"Parameters"),"SpotColorName")=="CutContour" && (decimal)Get(Get(detected,"Parameters"),"LineWidthMm")==.23m,"explicit source cutline populates list");
        var settings=Call(first,"ReadPdfGenerationSettings");Check(!(bool)Get(settings,"MakeCutline"),"existing cutline is preserved without duplication");
        var generation=(Task)Call(first,"GeneratePdfToPathAsync",Path.Combine(folder,"existing-cutline-output.pdf"),settings);await generation;
        Check(File.Exists(Path.Combine(folder,"existing-cutline-output.pdf")),"existing cutline output succeeds");
        await (Task)Call(first,"LoadSourceFileAsync",Path.Combine(folder,"source.png"));
        Check(((System.Collections.IList)Field(first,"_postProcesses")).Count==0,"non-cutline source clears source cutline row");
        Set(grid,"Row",fixedRows);Call(parent,"btnRemove_Click",parent,EventArgs.Empty);
        Check(first.IsDisposed && !second.IsDisposed && table.Rows.Count==1,"removed row releases only its editor");
        using(var bitmap=new Bitmap(parent.Width,parent.Height)){parent.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"banner-values.png"));}
        Console.WriteLine("PASS: order item values checks: "+checks);
    }
    [STAThread] private static int Main(string[] args)
    {
        int exit=0;
        try {
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=assembly.GetType("PineTree.Dialogs.DialogOrder",true);
            var parent=(Form)FormatterServices.GetUninitializedObject(type);typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(parent,new object[0]);
            Call(parent,"InitializeComponent");parent.StartPosition=FormStartPosition.Manual;parent.Location=new Point(-4000,-4000);parent.ShowInTaskbar=false;
            using(parent){parent.Shown+=async delegate{try{await Run(parent,assembly,Path.GetFullPath(args[1]));}catch(Exception e){Console.Error.WriteLine(e);exit=1;}finally{parent.Close();}};Application.Run(parent);}
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
        return exit;
    }
}
