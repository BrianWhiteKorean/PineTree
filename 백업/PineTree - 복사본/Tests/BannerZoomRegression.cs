using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
internal static class BannerZoomRegression
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr value, IntPtr data);
    private delegate bool WindowCallback(IntPtr window, IntPtr data);
    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint thread, WindowCallback callback, IntPtr data);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
    private static void SelectFixedSize(object editor)
    {
        bool messageSeen=false;
        using(var timer=new Timer { Interval=50 }) {
            timer.Tick+=delegate {
                EnumThreadWindows(GetCurrentThreadId(),delegate(IntPtr dialog,IntPtr data) {
                    var button=FindWindowEx(dialog,IntPtr.Zero,"Button",null);
                    if(button==IntPtr.Zero)return true;
                    messageSeen=FindWindowEx(dialog,IntPtr.Zero,"Static","폭을 지정하면 원본 비율에 맞춰 길이가 자동으로 입력됩니다.")!=IntPtr.Zero;
                    SendMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero);
                    return false;
                },IntPtr.Zero);
            };
            timer.Start();Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption13"));timer.Stop();
        }
        Check(messageSeen,"fixed-size selection displays requested message");
    }
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
            var path=Path.GetFullPath(args[0]);var app=Assembly.LoadFrom(path);var parentType=app.GetType("PineTree.UserControls.UcOrderDesigner",true);
            var parent=(UserControl)FormatterServices.GetUninitializedObject(parentType);typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(parent,new object[0]);
            using(parent) using(var host=new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Left=-4000, Top=-4000 }) {
                Call(parent,"InitializeComponent");Call(parent,"InitOrderItemTable");Call(parent,"InitOrderEditor");Call(parent,"InitPreviewEvent");
                var item=Activator.CreateInstance(app.GetType("PineTree.Dialogs.DialogPriceItemOrderItem",true));
                Set(item,"PriceItemSeqNo",101L);Set(item,"PriceItemName","Banner zoom");Set(item,"UnitName","회배");Set(item,"BillingBasis","면적");Set(item,"UnitPrice",4000m);
                Call(parent,"AddOrderItem",item,null);var table=(DataTable)Field(parent,"m_orderItems");table.Rows[0]["CategoryName"]="현수막";
                var grid=Field(parent,"FGrid1");Set(grid,"Row",(int)Get(Get(grid,"Rows"),"Fixed"));Call(parent,"UpdateSelectedOrderEditor");
                var editor=(UserControl)Field(parent,"m_orderEditor");
                host.ClientSize=parent.Size;host.Controls.Add(parent);host.Show();Application.DoEvents();
                var combo=Field(editor,"cbZoomType");var rate=Field(editor,"tbZoomRate");
                Check(object.ReferenceEquals(Get(combo,"SelectedOption"),Field(editor,"inputOption6")),"default original mode");
                Check(!(bool)Get(combo,"Enabled"),"zoom mode disabled before source drop");
                Check(!(bool)Get(Field(editor,"tbTargetWidth"),"Enabled") && !(bool)Get(Field(editor,"tbTargetHeight"),"Enabled"),"initial dimensions disabled");
                Check(!(bool)Get(rate,"Enabled") && Value(editor,"tbZoomRate")==1,"original mode disables rate and defaults to one");
                bool engineLoaded=false;foreach(var loaded in AppDomain.CurrentDomain.GetAssemblies())if(loaded.GetName().Name=="PineTree.PdfEngine")engineLoaded=true;
                Check(!engineLoaded,"initial zoom UI must not load PDF engine");
                // A real source inspection loads the engine; preload its standalone file for this synthetic metadata test.
                Assembly.LoadFrom(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../../../PineTree.PdfEngine/bin/Debug/PineTree.PdfEngine.dll")));
                Source(editor,1000.502,510.5);
                Check((bool)Get(combo,"Enabled"),"zoom mode enabled after source measurements loaded");
                Check(!(bool)Get(Field(editor,"tbTargetWidth"),"Enabled") && !(bool)Get(Field(editor,"tbTargetHeight"),"Enabled"),"original mode keeps dimensions disabled after source drop");
                Check(Value(editor,"tbTargetWidth")==1001 && Value(editor,"tbTargetHeight")==511,"original dimensions rounded half up");
                Check((decimal)table.Rows[0]["Qty"]==1 && (decimal)table.Rows[0]["AreaQty"]==.511511m,"grid receives rounded original area");
                Set(combo,"SelectedOption",Field(editor,"inputOption7"));
                Check((bool)Get(rate,"Enabled") && Value(editor,"tbZoomRate")==1,"scale mode enables rate with default one");
                Check(!(bool)Get(Field(editor,"tbTargetWidth"),"Enabled") && !(bool)Get(Field(editor,"tbTargetHeight"),"Enabled"),"ratio mode keeps dimensions disabled");
                var rateTextBox=(TextBox)Get(rate,"TextBox");
                rateTextBox.Focus();rateTextBox.SelectAll();
                foreach(char key in "10") SendMessage(rateTextBox.Handle,0x0102,(IntPtr)key,IntPtr.Zero);
                Check(rateTextBox.Text=="10" && Value(editor,"tbZoomRate")==10,"keyboard accepts two-digit rate 10");
                Check(Value(editor,"tbTargetWidth")==10005 && Value(editor,"tbTargetHeight")==5105,"keyboard rate updates rounded dimensions");
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
                SelectFixedSize(editor);
                Check(Value(editor,"tbTargetWidth")==31 && Value(editor,"tbTargetHeight")==46,"fixed-size selection preserves current dimensions");
                Check(!(bool)Get(rate,"Enabled") && (bool)Get(Field(editor,"tbTargetWidth"),"Enabled") && (bool)Get(Field(editor,"tbTargetHeight"),"Enabled"),"fixed-size enables dimensions and disables rate");
                Check((bool)Get(Field(editor,"tbTargetHeight"),"ReadOnly"),"fixed-size height is automatically calculated");
                Num(editor,"tbTargetWidth",800);
                Num(editor,"tbZoomRate",10);
                Check(Value(editor,"tbTargetWidth")==800 && Value(editor,"tbTargetHeight")==1190,"fixed-size height follows original ratio independently of stored rate");
                Check((decimal)table.Rows[0]["AreaQty"]==.952m,"fixed-size edits update order area");
                output=Call(editor,"ReadPdfGenerationSettings");
                Check((double)Get(output,"TargetWidthMm")==800 && (double)Get(output,"TargetHeightMm")==1190 && (string)Get(output,"FitMode")=="COVER","fixed-size output uses exact target bounds and cover placement");
                Set(Field(editor,"cbDeploy"),"SelectedOption",Field(editor,"inputOption4"));output=Call(editor,"ReadPdfGenerationSettings");
                Check((string)Get(output,"FitMode")=="CONTAIN","fixed size retains contain placement option");
                var fixedState=Capture(editor);Check((string)Get(fixedState,"ZoomType")=="FIXED_SIZE","fixed-size mode captured explicitly");
                var fixedJson=(string)fixedState.GetType().GetMethod("ToJson").Invoke(fixedState,null);
                var fixedDecoded=fixedState.GetType().GetMethod("FromJson").Invoke(null,new object[]{fixedJson});
                using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())) {
                    restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{fixedDecoded});
                    Check(Value(restored,"tbTargetWidth")==800 && Value(restored,"tbTargetHeight")==1190 && object.ReferenceEquals(Get(Field(restored,"cbZoomType"),"SelectedOption"),Field(restored,"inputOption13")),"fixed-size mode and independent dimensions restored");
                    Check(!(bool)Get(Field(restored,"tbZoomRate"),"Enabled"),"restored fixed-size rate disabled");
                    Check((bool)Get(Field(restored,"tbTargetWidth"),"Enabled") && (bool)Get(Field(restored,"tbTargetHeight"),"Enabled"),"restored fixed-size dimensions enabled");
                }
                Call(editor,"ResetSourceMeasurements");Source(editor,100,200);
                Check(Value(editor,"tbTargetWidth")==100 && Value(editor,"tbTargetHeight")==200,"replacement source seeds usable fixed-size dimensions");
                Set(combo,"SelectedOption",Field(editor,"inputOption7"));
                Check(Value(editor,"tbTargetWidth")==1000 && Value(editor,"tbTargetHeight")==2000,"switching back to ratio scales original dimensions");
                Check(!(bool)Get(Field(editor,"tbTargetHeight"),"ReadOnly"),"other modes retain normal height editing");
                Check(!(bool)Get(Field(editor,"tbTargetWidth"),"Enabled") && !(bool)Get(Field(editor,"tbTargetHeight"),"Enabled"),"leaving fixed-size disables dimensions again");
                Source(editor,800,59);SelectFixedSize(editor);
                var widthTextBox=(TextBox)Get(Field(editor,"tbTargetWidth"),"TextBox");
                widthTextBox.Focus();widthTextBox.SelectAll();
                foreach(char key in "4000") SendMessage(widthTextBox.Handle,0x0102,(IntPtr)key,IntPtr.Zero);
                Check(Value(editor,"tbTargetWidth")==4000 && Value(editor,"tbTargetHeight")==295,"keyboard width 4000 uses source 800 by 59 to calculate height 295");
                Num(editor,"tbTargetWidth",800);Num(editor,"tbTargetWidth",4000);
                Check(Value(editor,"tbTargetHeight")==295,"repeated width edits use source ratio without cumulative rounding");
                Num(editor,"tbTargetWidth",0);Check(Value(editor,"tbTargetHeight")==0,"zero width clears calculated height");
                Call(editor,"ResetSourceMeasurements");Call(editor,"UpdateDocumentState");
                Check(!(bool)Get(combo,"Enabled") && !(bool)Get(rate,"Enabled"),"cleared source disables both zoom inputs");
            }
            Console.WriteLine("PASS: zoom/original/rounding/grid/state/PDF checks: "+checks);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
