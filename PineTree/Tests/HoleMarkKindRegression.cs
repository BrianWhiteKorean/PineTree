using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Collections;
class HoleMarkKindRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Get(object o,string n){return o.GetType().GetProperty(n).GetValue(o,null);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static void Click(object form,string name){
  var button=Field(form,name);
  if(button is Control) button.GetType().GetMethod("PerformClick").Invoke(button,null);
  else button.GetType().GetMethod("OnClick",F,null,new[]{typeof(EventArgs)},null).Invoke(button,new object[]{EventArgs.Empty});
 }
 static object Cell(object grid,int row,int col){return grid.GetType().GetProperty("Item",new[]{typeof(int),typeof(int)}).GetValue(grid,new object[]{row,col});}
 static void AddCustomPlacement(Form dialog){
  Set(Field(dialog,"cbDeployType"),"SelectedIndex",1);
  Check((bool)Get(Field(dialog,"btnAdd"),"Enabled"),"custom placement can be added");
  using(var timer=new Timer { Interval=50 }){
   timer.Tick+=delegate {
    foreach(Form child in Application.OpenForms){
     if(child.GetType().Name!="Dlg타공_커스텀타공_상단하단") continue;
     timer.Stop();
     Check(!(bool)Get(Field(child,"tbTopPos1"),"Enabled"),"unchecked top positions are disabled");
     Check(!(bool)Get(Field(child,"inputLabel1"),"Enabled") && !(bool)Get(Field(child,"rbTopSameGap"),"Enabled"),"unchecked top count label and radio are disabled");
     foreach(string side in new[]{"Top","Bottom"}){
      Check((decimal)Get(Field(child,"tb"+side+"HoleCount"),"Value")==4m,"default count is four");
      for(int pos=1;pos<=6;pos++)
       Check((decimal)Get(Field(child,"tb"+side+"Pos"+pos),"Value")==20m,"default coordinate is 20 mm at 1:1");
      Check((decimal)Get(Field(child,"tb"+side+"Gap"),"Value")==0m,"default fixed interval is zero");
     }
     Set(Field(child,"chkTopHole"),"Checked",true);
     Set(Field(child,"chkBottomHole"),"Checked",true);
     Set(Field(child,"rbBottomFixGap"),"Checked",true);
     Check((bool)Get(Field(child,"tbTopHoleCount"),"Enabled") &&
           !(bool)Get(Field(child,"tbBottomHoleCount"),"Enabled"),"only uniform spacing uses the count field");
     Check((bool)Get(Field(child,"rbTopSameGap"),"Checked"),"top and bottom radio groups are independent");
     Check((bool)Get(Field(child,"tbTopPos1"),"Enabled") && !(bool)Get(Field(child,"tbBottomPos1"),"Enabled"),"only chosen mode's coordinates are enabled");
     Check((bool)Get(Field(child,"inputLabel5"),"Enabled") && !(bool)Get(Field(child,"inputLabel17"),"Enabled"),"top uniform labels follow the selection");
     Check(!(bool)Get(Field(child,"inputLabel13"),"Enabled") && (bool)Get(Field(child,"inputLabel18"),"Enabled"),"bottom fixed labels follow the selection");
     Set(Field(child,"chkBottomHole"),"Checked",false);
     Check(!(bool)Get(Field(child,"inputLabel18"),"Enabled") && !(bool)Get(Field(child,"tbBottomGap"),"Enabled") &&
           (bool)Get(Field(child,"inputLabel5"),"Enabled"),"unchecking bottom disables only the bottom group");
     Set(Field(child,"chkBottomHole"),"Checked",true);
     Check((bool)Get(Field(child,"inputLabel18"),"Enabled") && (bool)Get(Field(child,"tbBottomGap"),"Enabled"),"rechecking bottom restores its selected group");
     Set(Field(child,"tbTopHoleCount"),"Value",10m);
     Set(Field(child,"tbTopPos1"),"Value",2m);
     Set(Field(child,"tbTopPos2"),"Value",2m);
     Set(Field(child,"tbTopPos3"),"Value",2m);
     Set(Field(child,"tbTopPos4"),"Value",2m);
     Set(Field(child,"tbBottomHoleCount"),"Value",10m);
     Set(Field(child,"tbBottomPos5"),"Value",10m);
     Set(Field(child,"tbBottomPos6"),"Value",2m);
     Set(Field(child,"tbBottomGap"),"Value",250m);
     Click(child,"btnOK");
     Check(child.DialogResult==DialogResult.OK,"custom placement is accepted");
     break;
    }
   };
   timer.Start();Click(dialog,"btnAdd");timer.Stop();
  }
 }
 static void EditCustomPlacement(object grid,bool save){
  bool opened=false;
  using(var timer=new Timer { Interval=50 }){
   timer.Tick+=delegate {
    foreach(Form child in Application.OpenForms){
     if(child.GetType().Name!="Dlg타공_커스텀타공_상단하단") continue;
     timer.Stop();opened=true;
     Check(child.Text.Contains("수정") && (bool)Get(Field(child,"chkTopHole"),"Checked") &&
           (bool)Get(Field(child,"chkBottomHole"),"Checked"),"edit restores both selected runs");
     Check((bool)Get(Field(child,"rbTopSameGap"),"Checked") && (bool)Get(Field(child,"rbBottomFixGap"),"Checked"),
           "edit restores both interval modes");
     Check((decimal)Get(Field(child,"tbTopHoleCount"),"Value")==10m &&
           (decimal)Get(Field(child,"tbTopPos3"),"Value")==2m &&
           (decimal)Get(Field(child,"tbBottomPos5"),"Value")==10m &&
           (decimal)Get(Field(child,"tbBottomGap"),"Value")==250m,"edit restores coordinates and fixed interval");
     Set(Field(child,"tbTopHoleCount"),"Value",9m);
     Set(Field(child,"tbTopPos3"),"Value",3m);
     Click(child,save?"btnOK":"btnCancel");
     break;
    }
   };
   var bounds=(Rectangle)grid.GetType().GetMethod("GetCellRect",new[]{typeof(int),typeof(int)})
       .Invoke(grid,new object[]{2,0});
   timer.Start();
   Call(grid,"OnMouseDoubleClick",new MouseEventArgs(MouseButtons.Left,2,bounds.Left+5,bounds.Top+5,0));
   timer.Stop();
  }
  Check(opened,"double clicking a placement opens the edit dialog");
  Check((int)Get(Get(grid,"Rows"),"Count")==3 && Convert.ToInt32(Cell(grid,2,1))==(save?12:13),
        "only confirmed edits change the selected grid row");
 }
 static void SetSidePlacement(Form dialog,object grid,bool edit){
  bool opened=false;
  Set(Field(dialog,"cbDeployType"),"SelectedIndex",edit?1:2);
  using(var timer=new Timer { Interval=50 }){
   timer.Tick+=delegate {
    foreach(Form child in Application.OpenForms){
     if(child.GetType().Name!="Dlg타공_커스텀타공_좌우") continue;
     timer.Stop();opened=true;
     Check((string)Get(Field(child,"inputLabel11"),"Text")=="상(Top)" &&
           (string)Get(Field(child,"inputLabel15"),"Text")=="상(Top)" &&
           (string)Get(Field(child,"inputLabel14"),"Text")=="우(Right)","right runs start from the top right");
     if(edit){
      Check(child.Text.Contains("수정") && (bool)Get(Field(child,"rbLeftSameGap"),"Checked") &&
            (bool)Get(Field(child,"rbRightFixGap"),"Checked"),"edit restores separate side modes");
      Check((decimal)Get(Field(child,"tbRightGap"),"Value")==600m,"edit restores vertical interval");
     }
     else{
      foreach(string side in new[]{"Left","Right"}){
       var endX=Field(child,"tb"+side+"Pos7");
       var endY=Field(child,"tb"+side+"Pos8");
       Check((decimal)Get(endX,"Value")==20m && (decimal)Get(endY,"Value")==20m,
             "fixed endpoints default to the same 20 mm as other coordinates");
       Check(!(bool)Get(endX,"Enabled") && !(bool)Get(endY,"Enabled"),"unchecked side disables fixed endpoints");
       Set(Field(child,"chk"+side+"Hole"),"Checked",true);
       Check(!(bool)Get(endX,"Enabled") && !(bool)Get(endY,"Enabled"),"uniform mode disables fixed endpoints");
       Set(Field(child,"rb"+side+"FixGap"),"Checked",true);
       Check((bool)Get(endX,"Enabled") && (bool)Get(endY,"Enabled"),"fixed mode enables fixed endpoints");
       Set(Field(child,"chk"+side+"Hole"),"Checked",false);
       Check(!(bool)Get(endX,"Enabled") && !(bool)Get(endY,"Enabled"),"unchecking side disables fixed endpoints");
       Set(Field(child,"chk"+side+"Hole"),"Checked",true);
       Check((bool)Get(endX,"Enabled") && (bool)Get(endY,"Enabled"),"rechecking restores fixed endpoints");
       Set(Field(child,"rb"+side+"SameGap"),"Checked",true);
       Check(!(bool)Get(endX,"Enabled") && !(bool)Get(endY,"Enabled"),"switching to uniform disables fixed endpoints");
      }
      Set(Field(child,"chkLeftHole"),"Checked",true);
      Set(Field(child,"chkRightHole"),"Checked",true);
      Set(Field(child,"rbRightFixGap"),"Checked",true);
     }
     Set(Field(child,"tbRightGap"),"Value",edit?900m:600m);
     Click(child,"btnOK");
     Check(child.DialogResult==DialogResult.OK,"side placement is accepted");
     break;
    }
   };
   timer.Start();
   if(edit){
    var bounds=(Rectangle)grid.GetType().GetMethod("GetCellRect",new[]{typeof(int),typeof(int)})
        .Invoke(grid,new object[]{3,0});
    Call(grid,"OnMouseDoubleClick",new MouseEventArgs(MouseButtons.Left,2,bounds.Left+5,bounds.Top+5,0));
   }
   else Click(dialog,"btnAdd");
   timer.Stop();
  }
  Check(opened,"correct side dialog opens from add and row edit");
  Check((int)Get(Get(grid,"Rows"),"Count")==4 && Convert.ToInt32(Cell(grid,3,1))==(edit?6:7),
        "side grid count follows the height limit");
 }
 [STAThread]static int Main(string[] args){try{
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
  var settingType=app.GetType("PineTree.Classes.OrderPostProcessSetting",true);
  Application.EnableVisualStyles();
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  foreach(string type in new[]{"UcOrder수성_그룹A","UcOrder수성_그룹B","UcOrder라텍스_그룹A"})
  using(var host=new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Left=-4000, Top=-4000, Width=1100, Height=800 })
  using(var editor=(UserControl)Activator.CreateInstance(app.GetType("PineTree.UserControlsOrder."+type,true))){
   host.Controls.Add(editor);host.Show();
   Set(Field(editor,"tbTargetWidth"),"Value",600m); Set(Field(editor,"tbTargetHeight"),"Value",1500m);
   if(type=="UcOrder수성_그룹B") {
    bool opened=false;
    foreach(var option in (IEnumerable)Get(Field(editor,"cbWork"),"Items"))
     if((string)Get(option,"Text")=="타공") Set(Field(editor,"cbWork"),"SelectedOption",option);
    Check((string)Get(Get(Field(editor,"cbWork"),"SelectedOption"),"Text")=="타공","PET hole option selected");
    using(var timer=new System.Windows.Forms.Timer { Interval=50 }) {
     timer.Tick+=delegate {
      foreach(Form dialog in Application.OpenForms) {
       if(dialog.GetType().Name!="Dlg후가공공통_타공마크") continue;
       timer.Stop();opened=true;
       Check((int)Get(Get(Field(dialog,"FGrid1"),"Rows"),"Count")==1,"new hole dialog starts without a placement");
       dialog.DialogResult=DialogResult.Cancel;dialog.Close();break;
      }
     };
     timer.Start();
     Call(editor,"AddPostProcess");timer.Stop();
    }
    Check(opened,"PET Add opens shared hole dialog");
    Check(((IList)Field(editor,"_postProcesses")).Count==0,"cancel leaves finishing list unchanged");
   }
   var hole=settingType.GetMethod("Create").Invoke(null,new object[]{"HOLE"});
   foreach(int index in new[]{1,0}){
    object accepted;
    using(var dialog=(Form)Call(editor,"CreatePostProcessDialog",hole)){
     Check(dialog.GetType().Name=="Dlg후가공공통_타공마크","shared hole dialog used");
     Check((int)Get(Field(dialog,"cbHoleKind"),"SelectedIndex")== (index==1?0:1),"selected kind restored");
     Set(Field(dialog,"cbHoleKind"),"SelectedIndex",index);
     dialog.Shown+=delegate {
      var grid=Field(dialog,"FGrid1");
      Check((int)Get(Get(grid,"Rows"),"Count")==2,"saved placement appears in grid");
      if(index==1){
       using(var timer=new System.Windows.Forms.Timer { Interval=50 }) {
        timer.Tick+=delegate {
         foreach(Form child in Application.OpenForms) {
          if(child.GetType().Name!="Dlg타공_사방타공") continue;
          timer.Stop();
          Set(Field(child,"inputCheckBox2"),"Checked",false);
          Set(Field(child,"inputCheckBox4"),"Checked",false);
          Set(Field(child,"tbLeft"),"Value",10m);
          Set(Field(child,"tbRight"),"Value",15m);
          Set(Field(child,"tbTop"),"Value",25m);
          Set(Field(child,"tbBottom"),"Value",20m);
          Click(child,"btnOK");
          break;
         }
        };
        timer.Start();Click(dialog,"btnAdd");timer.Stop();
       }
       Check((int)Get(Get(grid,"Rows"),"Count")==3,"new placement added to grid");
       Check(Convert.ToInt32(Cell(grid,2,1))==3,"adjacent selected edges share a corner");
       Set(grid,"Row",1);Click(dialog,"btnRemove");
       Check((int)Get(Get(grid,"Rows"),"Count")==2,"remove deletes only selected row");
      }
      Check(Convert.ToInt32(Cell(grid,1,1))==3 && ((string)Cell(grid,1,2)).Contains("상 25"),"remaining row shows count and offset");
      if(index==0){
       AddCustomPlacement(dialog);
       string summary=(string)Cell(grid,2,2);
       Check(Convert.ToInt32(Cell(grid,2,1))==13 && summary.Contains("상단 균등") && summary.Contains("하단 고정 반대쪽까지"),"grid displays both custom runs and actual count");
       EditCustomPlacement(grid,false);
       EditCustomPlacement(grid,true);
       SetSidePlacement(dialog,grid,false);
       SetSidePlacement(dialog,grid,true);
      }
      Click(dialog,"btnOK");
     };
     Check(dialog.ShowDialog()==DialogResult.OK,"dialog accepts kind");accepted=Get(dialog,"Setting");
    }
    string kind=index==1?"CROSS":"DOT";
    Check((string)Get(Get(accepted,"Parameters"),"HoleMarkKind")==kind,"dialog setting kind");
    Call(editor,"CommitPostProcess",index==1?-1:0,accepted);
    var pdfSettings=Call(editor,"ReadPdfGenerationSettings");
    Check((string)Get(pdfSettings,"HoleMarkKind")==kind,"kind reaches engine");
    Check((int)pdfSettings.GetType().GetMethod("GetMarkCount").Invoke(pdfSettings,null)==(index==0?21:3),"calculated count reaches engine");
    var pdfRows=(IList)Get(pdfSettings,"HolePlacements");
    Check(pdfRows.Count==(index==0?3:1) && (double)Get(Get(pdfRows[0],"Edges"),"TopMm")==25,"remaining placement reaches engine");
    var state=editor.GetType().GetMethod("CaptureEditorState").Invoke(editor,null);
    string json=(string)state.GetType().GetMethod("ToJson").Invoke(state,null);
    var decoded=state.GetType().GetMethod("FromJson").Invoke(null,new object[]{json});
    using(var restored=(UserControl)Activator.CreateInstance(editor.GetType())){
     restored.GetType().GetMethod("RestoreEditorState").Invoke(restored,new[]{decoded});
     var items=(IList)Field(restored,"_postProcesses");
     var parameters=Get(items[0],"Parameters");
     Check((string)Get(parameters,"HoleMarkKind")==kind,"kind JSON roundtrip");
     var rows=(IList)Get(parameters,"HolePlacements");
     Check(rows.Count==(index==0?3:1),"selected rows survive reopening");
     var edges=Get(rows[0],"Edges");
     Check(!(bool)Get(edges,"RightEnabled") && !(bool)Get(edges,"BottomEnabled"),"unchecked directions survive JSON roundtrip");
     Check((double)Get(edges,"LeftMm")==10 && (double)Get(edges,"RightMm")==15 &&
           (double)Get(edges,"TopMm")==25 && (double)Get(edges,"BottomMm")==20,"all insets survive JSON roundtrip");
     if(index==0){
      var top=Get(rows[1],"TopRun");
      var bottom=Get(rows[1],"BottomRun");
      Check((string)Get(top,"Mode")=="UNIFORM" && (double)Get(top,"StartLeftMm")==2 &&
            (double)Get(top,"EndRightMm")==3 && (int)Get(top,"Count")==9,"edited count and right inset survive reopening");
      Check((string)Get(bottom,"Mode")=="FIXED" && (double)Get(bottom,"SpacingMm")==250 &&
            (int)Get(bottom,"Count")==0 && (double)Get(bottom,"StartBottomMm")==2,"automatic fixed spacing and bottom-relative position survive reopening");
      var left=Get(rows[2],"LeftRun");
      var right=Get(rows[2],"RightRun");
      Check((double)Get(left,"StartLeftMm")==20 && (double)Get(left,"EndBottomMm")==20 &&
            (double)Get(right,"StartRightMm")==20 && (double)Get(right,"SpacingMm")==900,"side anchors and edited interval survive reopening");
     }
    }
    hole=accepted;
   }
  }
  Console.WriteLine("PASS "+checks+" hole kind UI/state/engine checks");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
