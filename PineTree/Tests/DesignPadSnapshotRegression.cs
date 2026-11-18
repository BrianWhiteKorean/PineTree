using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Collections;
class DesignPadSnapshotRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static Assembly app; static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static decimal Num(object o,string n){return Convert.ToDecimal(Get(Field(o,n),"Value"));}
 static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;Console.WriteLine("PASS: "+s);}
 static Control Empty(string typeName,Type baseType){var type=app.GetType(typeName,true);var c=(Control)FormatterServices.GetUninitializedObject(type);baseType.GetConstructor(Type.EmptyTypes).Invoke(c,new object[0]);Call(c,"InitializeComponent");Call(c,"InitializeOrderPricing");Call(c,"InitOrderItemTable");Call(c,"InitOrderEditor");Call(c,"InitPreviewEvent");return c;}
 static object Col(object grid,string name){var cols=Get(grid,"Cols");return cols.GetType().GetProperty("Item",new[]{typeof(string)}).GetValue(cols,new object[]{name});}
 [STAThread]static int Main(string[] args){int result=1;Application.EnableVisualStyles();using(var host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new System.Drawing.Point(-5000,-5000)}){host.Shown+=delegate{result=Run(args);host.Close();};Application.Run(host);}return result;}
 static int Run(string[] args){try{
 Application.EnableVisualStyles();app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
 string fixture=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"snapshot-source.jpg");using(var bitmap=new System.Drawing.Bitmap(300,600)){bitmap.Save(fixture,System.Drawing.Imaging.ImageFormat.Jpeg);}
 var state=Activator.CreateInstance(app.GetType("PineTree.Classes.OrderDesignPadState"));var editor=Activator.CreateInstance(app.GetType("PineTree.Classes.BannerOrderEditorState"));
 Set(editor,"WidthMm",1000m);Set(editor,"HeightMm",500m);Set(editor,"Quantity",2m);Set(editor,"SourceFilePath",fixture);Set(editor,"SourceFileInfo","Saved source");Set(editor,"EdgeBottomMm",7m);Set(editor,"SourceRotationDegreesClockwise",90);
 var output=Activator.CreateInstance(app.GetType("PineTree.Classes.OrderServerFile"));string hash=new string('a',64);Set(output,"FileId",hash+".pdf");Set(output,"Sha256",hash);Set(output,"Length",123L);Set(output,"OriginalFileName","saved-output.pdf");Set(editor,"OutputServerFile",output);
 Set(state,"EquipmentName","수성");Set(state,"PriceCategory","수성");Set(state,"CategoryName","합성지");Set(state,"ProductName","작업제품");Set(state,"UnitPrice",4000m);Set(state,"Editor",editor);
 string json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
 using(var pad=Empty("PineTree.UserControls.UcOrderDesignPad",typeof(UserControl))){Call(pad,"ApplyDesignPadState",json);string saved=(string)Call(pad,"CaptureDesignPadState");Check(saved.Contains("saved-output.pdf") && saved.Contains("snapshot-source.jpg"),"design pad saves source and output references without accessory rows");var save=Call(pad,"CreateOrderSaveData");Check((string)Get(save,"DesignPadStateJson")==saved && ((IList)Get(save,"Items")).Count==0,"work snapshot is separate from accessory save data");json=saved;}
 using(var form=(Form)Empty("PineTree.Dialogs.DialogOrderDesignPad",typeof(Form))){var f=form.GetType().GetField("m_DialogMode",F);f.SetValue(form,Enum.Parse(f.FieldType,"Edit"));
 var table=(DataTable)Field(form,"m_orderItems");var row=table.NewRow();row["ItemName"]="부자재";row["UnitPrice"]=2100;row["Qty"]=3;row["SupplyAmount"]=6300;row["VatAmount"]=630;row["TotalAmount"]=6930;table.Rows.Add(row);
 Call(form,"ApplyDesignPadState",json);Call(form,"UpdateOrderItemTotals");
 Check(Num(form,"tbAccessory")==6930 && Num(form,"tbTotalAmount")==14930,"accessory total is added once to product price");
 Check(Num(form,"tbUnitPrice")==4000 && Num(form,"tbQty")==2,"accessory selection does not replace work price or quantity");
 var grid=Field(form,"FGrid1");Check(!(bool)Get(Col(grid,"ItemCode"),"Visible") && (int)Get(Col(grid,"Qty"),"Width")==40,"accessory grid hides item code and halves quantity width");
 var restored=Field(form,"m_orderEditor");Check(restored!=null && restored.GetType().Name=="UcOrder수성_그룹A","saved synthetic paper opens in Group A");var captured=restored.GetType().GetMethod("CaptureEditorState").Invoke(restored,null);Check((decimal)Get(captured,"EdgeBottomMm")==7 && Get(captured,"OutputServerFile")!=null,"finishing and generated output references restored");
 form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-4000,-4000);form.Show();Application.DoEvents();
 Check(((PictureBox)Field(form,"pictureBox1")).Image==null,"legacy snapshot without a saved PNG does not start source analysis");
 Check((decimal)Get(restored,"WidthMm")==1000 && (decimal)Get(restored,"Quantity")==2,"cacheless restore preserves saved edit settings");
 captured=restored.GetType().GetMethod("CaptureEditorState").Invoke(restored,null);Check((decimal)Get(captured,"EdgeBottomMm")==7 && Get(captured,"OutputServerFile")!=null && (int)Get(captured,"SourceRotationDegreesClockwise")==90,"cacheless restore retains finishing, rotation, and saved output reference");
 row["Qty"]=4;Call(form,"CalculateAccessoryAmounts",row);Call(form,"UpdateOrderItemTotals");Check(Num(form,"tbAccessory")==9240 && Num(form,"tbTotalAmount")==17240,"accessory quantity edit updates footer without losing product");
 string again=(string)Call(form,"CaptureDesignPadState");Check(again.Contains("snapshot-source.jpg"),"dialog re-save retains source state");form.Close();}
 Console.WriteLine("PASS: "+checks+" snapshot regression checks");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
