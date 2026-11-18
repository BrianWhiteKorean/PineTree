using System;
using System.Data;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
class DialogOrderDesignPadRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS: "+s);}
 static decimal Num(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
 [STAThread] static int Main(string[] args){try{
 Application.EnableVisualStyles();
 string fixture=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"source.jpg");fixture=Path.GetFullPath(fixture);
 using(var image=new System.Drawing.Bitmap(300,600)){using(var g=System.Drawing.Graphics.FromImage(image)){g.Clear(System.Drawing.Color.Green);}image.Save(fixture,System.Drawing.Imaging.ImageFormat.Jpeg);}
 var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=asm.GetType("PineTree.Dialogs.DialogOrderDesignPad",true);
 var form=(Form)FormatterServices.GetUninitializedObject(type);typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(form,new object[0]);
 using(form){Call(form,"InitializeComponent");Call(form,"InitializeOrderPricing");Call(form,"InitOrderItemTable");Call(form,"InitOrderEditor");Call(form,"InitPreviewEvent");
 Check(form.StartPosition==FormStartPosition.CenterParent && !form.ShowInTaskbar,"dialog window settings and designer resources");
 var mode=type.GetField("m_DialogMode",F);mode.SetValue(form,Enum.Parse(mode.FieldType,"Edit"));
 var table=((DataTable)Field(form,"m_orderItems")).Clone();table.Columns.Add("ItemDescription");
 for(int i=0;i<2;i++){var r=table.NewRow();r["OrderItemId"]=i+1;r["PriceItemSeqNo"]=1;r["PriceCategory"]="출력";r["CategoryName"]="현수막";r["ItemName"]="테스트";r["CustomName"]="품목"+i;r["WidthMm"]=1000+i*500;r["HeightMm"]=500;r["Qty"]=2+i;r["UnitPrice"]=3000;r["SupplyAmount"]=6000+i*3000;r["VatAmount"]=600+i*300;r["TotalAmount"]=6600+i*3300;
 r["RequestFilePath"]=fixture;
 r["EditorStateJson"]="{\"WidthMm\":"+(1000+i*500)+",\"HeightMm\":500,\"Quantity\":"+(2+i)+",\"SourceRotationDegreesClockwise\":90,\"EdgeBottomMm\":7}";r["EditorStateJson"]=((string)r["EditorStateJson"]).Replace("{", "{\"SourceFilePath\":\""+fixture.Replace("\\","\\\\")+"\",");table.Rows.Add(r);}
 Call(form,"ApplyLoadedOrderItems",table);var editor=Field(form,"m_orderEditor");Check(editor!=null,"first saved material editor restored");
 Check((decimal)Get(editor,"Quantity")==2 && (decimal)Get(editor,"WidthMm")==1000,"stored dimensions and quantity");
 var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);Check((decimal)Get(state,"EdgeBottomMm")==7,"saved finishing settings restored");
 form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-4000,-4000);form.Show();
 Application.DoEvents();
 Check(((PictureBox)Field(form,"pictureBox1")).Image==null,"legacy order without a saved PNG does not analyze its local source automatically");
 Check((decimal)Get(editor,"Quantity")==2 && (decimal)Get(editor,"WidthMm")==1000,"cacheless restore preserves saved dimensions and quantity");
 Check(Num(form,"tbQty")==2 && Num(form,"tbUnitPrice")==3000 && Num(form,"tbTotalAmount")==16500,"footer displays stored quantity, price, order total");
 var grid=Field(form,"FGrid1");grid.GetType().GetProperty("Row").SetValue(grid,2,null);Call(form,"UpdateSelectedOrderEditor");var second=Field(form,"m_orderEditor");
 Check(!ReferenceEquals(editor,second) && (decimal)Get(second,"Quantity")==3,"same-material rows restore independent editors");
 grid.GetType().GetProperty("Row").SetValue(grid,1,null);Call(form,"UpdateSelectedOrderEditor");Check(ReferenceEquals(editor,Field(form,"m_orderEditor")),"returning to a row preserves its editor");
 var saves=(IList)Call(form,"CreateOrderItemSaveDataList");Check(saves.Count==2 && (decimal)Get(saves[0],"Qty")==2 && (decimal)Get(saves[1],"Qty")==3,"restored items retain save data");
 Call(form,"btnCancel_Click",null,EventArgs.Empty);Check(form.DialogResult==DialogResult.Cancel,"cancel closes modal dialog");
 }
 return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
