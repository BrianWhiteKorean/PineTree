using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
class SourceTransformRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Assembly app; static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n,F).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
 static void Click(object pad,string name){Call(Field(pad,name),"PerformClick");}
 static void Orient(object editor,int angle,bool flip,decimal w,decimal h){Check((int)Get(editor,"SourceRotationDegreesClockwise")==angle && (bool)Get(editor,"SourceFlipHorizontal")==flip,"Transform state");Check((decimal)Get(editor,"WidthMm")==w && (decimal)Get(editor,"HeightMm")==h,"Oriented size: "+Get(editor,"WidthMm")+" x "+Get(editor,"HeightMm"));}
 static void Pixel(PictureBox picture,Color color){var b=(Bitmap)picture.Image;Color p=b.GetPixel(b.Width/4,b.Height/4);Check(Math.Abs(p.R-color.R)<10&&Math.Abs(p.G-color.G)<10&&Math.Abs(p.B-color.B)<10,"Preview top-left color "+p+" expected "+color);}
 static async Task Run(object pad,string folder){
  Check(!(bool)Get(Field(pad,"btn90CW"),"Enabled"),"Disabled without artwork");
  var table=new DataTable();table.Columns.Add("CategoryName");table.Rows.Add("현수막");
  Call(pad,"BindProductCombo",Field(pad,"cbCategoryName"),table,"CategoryName");Call(pad,"UpdateSelectedOrderEditor");
  var editor=Field(pad,"m_orderEditor");var source=Field(pad,"m_sourceDocument");var picture=(PictureBox)Field(pad,"pictureBox1");
  await (Task)Call(source,"LoadAsync",Path.Combine(folder,"source.pdf"));
  Check((bool)Get(Field(pad,"btn90CW"),"Enabled"),"Enabled with artwork");
  Orient(editor,0,false,80,40);Pixel(picture,Color.Red);var raw=Get(source,"CurrentImage");
  Click(pad,"btn90CW");Orient(editor,90,false,40,80);Pixel(picture,Color.Blue);
  Click(pad,"btn90CCW");Orient(editor,0,false,80,40);Pixel(picture,Color.Red);
  for(int i=0;i<4;i++)Click(pad,"btn90CW");Orient(editor,0,false,80,40);
  Click(pad,"btnFlip");Pixel(picture,Color.Lime);Click(pad,"btnFlip");Orient(editor,0,false,80,40);
  Click(pad,"btnFlip");Click(pad,"btn90CW");Orient(editor,270,true,40,80);Pixel(picture,Color.Yellow);
  Check(Object.ReferenceEquals(raw,Get(source,"CurrentImage")),"Raw bitmap unchanged");
  Set(source,"OutputBusy",true);Check(!(bool)Get(Field(pad,"btnFlip"),"Enabled"),"Disabled during generation");Click(pad,"btn90CW");Orient(editor,270,true,40,80);Set(source,"OutputBusy",false);
  var state=Call(editor,"CaptureEditorState");var json=(string)Call(state,"ToJson");state=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
  Check((int)Get(state,"SourceRotationDegreesClockwise")==270 && (bool)Get(state,"SourceFlipHorizontal"),"JSON preserves transform");
  var settings=Call(editor,"ReadPdfGenerationSettings");
  Check((int)Get(settings,"SourceRotationDegreesClockwise")==270 && (bool)Get(settings,"SourceFlipHorizontal"),"PDF settings match preview");
  await (Task)Call(editor,"GeneratePdfToPathAsync",Path.Combine(folder,"output.pdf"),settings);
  await (Task)Call(source,"SelectPageAsync",1);Orient(editor,0,false,80,40);Pixel(picture,Color.Red);
  await (Task)Call(source,"SelectPageAsync",0);Orient(editor,270,true,40,80);Pixel(picture,Color.Yellow);
  using(var restored=(Control)Activator.CreateInstance(editor.GetType())){
   Call(restored,"RestoreEditorState",state);await(Task)Call(Get(restored,"SourceDocument"),"EnsureLoadedAsync");Orient(restored,270,true,40,80);
  }
  editor.GetType().GetField("_updatingInputs",F).SetValue(editor,true);Set(Field(editor,"cbZoomType"),"SelectedOption",Field(editor,"inputOption13"));editor.GetType().GetField("_updatingInputs",F).SetValue(editor,false);
  Set(Field(editor,"tbTargetWidth"),"Value",120m);Orient(editor,270,true,120,240);Click(pad,"btn90CW");Orient(editor,180,true,240,120);
  var petType=app.GetType("PineTree.UserControlsOrder.UcOrder페트");using(var pet=(Control)Activator.CreateInstance(petType)){
   Call(pet,"RestoreEditorState",state);await(Task)Call(Get(pet,"SourceDocument"),"EnsureLoadedAsync");Orient(pet,270,true,40,80);Call(pet,"RotateSource",90);Orient(pet,180,true,80,40);
  }
  await(Task)Call(source,"LoadAsync",Path.Combine(folder,"replacement.pdf"));Orient(editor,0,false,80,40);Pixel(picture,Color.Red);
 }
 [STAThread]static int Main(string[] args){int exit=0;try{Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
 var pad=FormatterServices.GetUninitializedObject(app.GetType("PineTree.UserControls.UcOrderDesignPad"));typeof(UserControl).GetConstructor(Type.EmptyTypes).Invoke(pad,new object[0]);Call(pad,"InitializeComponent");Call(pad,"InitOrderItemTable");Call(pad,"InitOrderEditor");Call(pad,"InitPreviewEvent");
 using(var host=new Form{ClientSize=new Size(1500,920),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Left=-4000,Top=-4000})using(var control=(Control)pad){control.Dock=DockStyle.Fill;host.Controls.Add(control);host.Shown+=async delegate{try{await Run(pad,Path.GetFullPath(args[1]));Console.WriteLine("PASS: "+checks+" source-transform UI checks");}catch(Exception e){Console.Error.WriteLine(e);exit=1;}finally{host.Close();}};Application.Run(host);}
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}return exit;}
}
