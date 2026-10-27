using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
internal static class BannerZoomRegression
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    private static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
    private static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
    private static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
    private static void Num(object o,string n,decimal v){Set(Field(o,n),"Value",v);}
    private static decimal Value(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
    private static void Check(bool v,string message){if(!v)throw new Exception(message);checks++;}
    private static object Capture(object o){return o.GetType().GetMethod("CaptureEditorState").Invoke(o,null);}
    private static void Source(object editor,double w,double h){var type=editor.GetType().GetNestedType("SourcePreviewData",BindingFlags.NonPublic);var source=Activator.CreateInstance(type,true);type.GetField("SourceWidthMm").SetValue(source,w);type.GetField("SourceHeightMm").SetValue(source,h);Call(editor,"ApplySourceMeasurements",source);}
    [STAThread]private static int Main(string[] args)
    {
        try{
            Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var path=Path.GetFullPath(args[0]);var app=Assembly.LoadFrom(path);var parentType=app.GetType("PineTree.Dialogs.DialogOrder",true);
            var parent=(Form)FormatterServices.GetUninitializedObject(parentType);typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(parent,new object[0]);
            using(parent){
                Call(parent,"InitializeComponent");Call(parent,"InitOrderItemTable");Call(parent,"InitOrderEditor");Call(parent,"InitPreviewEvent");
                var item=Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem",true));
                Set(item,"PriceItemSeqNo",101L);Set(item,"PriceItemName","Banner zoom");Set(item,"UnitName","회배");Set(item,"BillingBasis","면적");Set(item,"UnitPrice",4000m);
                Call(parent,"AddOrderItem",item,null);var table=(DataTable)Field(parent,"m_orderItems");table.Rows[0]["CategoryName"]="현수막";
                var grid=Field(parent,"FGrid1");Set(grid,"Row",(int)Get(Get(grid,"Rows"),"Fixed"));Call(parent,"UpdateSelectedOrderEditor");
                var editor=(UserControl)Field(parent,"m_orderEditor");
                parent.StartPosition=FormStartPosition.Manual;parent.Left=-4000;parent.Top=-4000;parent.ShowInTaskbar=false;parent.Show();Application.DoEvents();
                var combo=Field(editor,"cbZoomType");var rate=Field(editor,"tbZoomRate");
                Check(object.ReferenceEquals(Get(combo,"SelectedOption"),Field(editor,"inputOption6")),"default original mode");
                Check(!(bool)Get(rate,"Enabled") && Value(editor,"tbZoomRate")==1,"original mode disables rate and defaults to one");
                bool engineLoaded=false;foreach(var loaded in AppDomain.CurrentDomain.GetAssemblies())if(loaded.GetName().Name=="PineTree.PdfEngine")engineLoaded=true;
                Check(!engineLoaded,"initial zoom UI must not load PDF engine");
                // A real source inspection loads the engine; preload its standalone file for this synthetic metadata test.
                Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
                Source(editor,1000.502,510.5);
                Check(Value(editor,"tbTargetWidth")==1001 && Value(editor,"tbTargetHeight")==511,"original dimensions rounded half up");
                Check((decimal)table.Rows[0]["Qty"]==1 && (decimal)table.Rows[0]["AreaQty"]==.511511m,"grid receives rounded original area");
                Set(combo,"SelectedOption",Field(editor,"inputOption7"));
                Check((bool)Get(rate,"Enabled") && Value(editor,"tbZoomRate")==1,"scale mode enables rate with default one");
                Num(editor,"tbZoomRate",2);
                Check(Value(editor,"tbTargetWidth")==2001 && Value(editor,"tbTargetHeight")==1021,"two times uses unrounded source");
                Num(editor,"tbZoomRate",1.5m);
                Check(Value(editor,"tbTargetWidth")==1501 && Value(editor,"tbTargetHeight")==766,"fractional scale avoids compounded rounding");
                Check((decimal)table.Rows[0]["AreaQty"]==1.149766m && (decimal)table.Rows[0]["TotalAmount"]==5059,"scaled area and VAT update grid");
                var state=Capture(editor);Check((string)Get(state,"ZoomType")=="SCALE" && (decimal)Get(state,"ZoomRate")==1.5m,"zoom controls captured");
                var json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);var decoded=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
                using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())){
                    restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{decoded});
                    Check(Value(restored,"tbTargetWidth")==1501 && Value(restored,"tbTargetHeight")==766 && Value(restored,"tbZoomRate")==1.5m,"saved scaling restored");
                    Check((bool)Get(Field(restored,"tbZoomRate"),"Enabled") && object.ReferenceEquals(Get(Field(restored,"cbZoomType"),"SelectedOption"),Field(restored,"inputOption7")),"restored mode enables rate");
                }
                Set(combo,"SelectedOption",Field(editor,"inputOption6"));
                Check(!(bool)Get(rate,"Enabled") && Value(editor,"tbTargetWidth")==1001 && Value(editor,"tbTargetHeight")==511,"original ignores previously entered scale");
                Set(combo,"SelectedOption",Field(editor,"inputOption7"));Num(editor,"tbZoomRate",3);Num(editor,"tbZoomRate",1.5m);
                Check(Value(editor,"tbTargetWidth")==1501 && Value(editor,"tbTargetHeight")==766,"repeated rate edits always use original source");
                Num(editor,"tbTargetWidth",2000.5m);Check(Value(editor,"tbTargetWidth")==2001,"direct dimension entry also rounded half up");
                var manual=Capture(editor);Check(Get(manual,"ZoomType")==null,"manual override retains independent sizing");
                using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())){restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{manual});Check(Value(restored,"tbTargetWidth")==2001,"manual legacy size not overwritten on reload");}
                Source(editor,20.5,30.5);Check(Value(editor,"tbTargetWidth")==31 && Value(editor,"tbTargetHeight")==46,"new source uses current scale");
                var output=Call(editor,"ReadPdfGenerationSettings");Check((double)Get(output,"TargetWidthMm")==31 && (double)Get(output,"TargetHeightMm")==46,"generated PDF receives rounded scaled size");
                Set(Field(editor,"cbDeploy"),"SelectedOption",Field(editor,"inputOption5"));output=Call(editor,"ReadPdfGenerationSettings");Check((string)Get(output,"FitMode")=="COVER","placement mode independent from zoom");
            }
            Console.WriteLine("PASS: zoom/original/rounding/grid/state/PDF checks: "+checks);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
