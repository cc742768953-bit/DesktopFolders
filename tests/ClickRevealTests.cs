using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DesktopFolders;

public static class ClickRevealTests {
 static MainWindow window;
 static Application app;
 static int exitCode=1;
 static readonly List<string> checks=new List<string>();
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Get(string name){return typeof(MainWindow).GetField(name,Flags).GetValue(window);}
 static void Set(string name,object value){typeof(MainWindow).GetField(name,Flags).SetValue(window,value);}
 static object Call(string name,params object[] args){return typeof(MainWindow).GetMethod(name,Flags).Invoke(window,args);}
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
 static void PauseTimer(){((DispatcherTimer)Get("hoverTimer")).Stop();}
 [STAThread]public static int Main(){
  var config=Files.Load();config.ClickToReveal=true;
  app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
  window=new MainWindow(config,false);
  window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(Run),DispatcherPriority.ApplicationIdle);
  window.Show();app.Run();return exitCode;
 }
 static async void Run(){
  try{
   Check(!window.IsVisible,"Click-to-reveal starts with the whole window hidden");
   Check((IntPtr)Get("desktopClickHook")!=IntPtr.Zero,"Desktop click listener remains installed while hidden");
   Call("HandleDesktopBackgroundClick",true,0);PauseTimer();
   Check(window.IsVisible&&Get("selected")==null,"Desktop background handler reveals the categories overview");
   var now=DateTime.UtcNow;Set("autoHideLeftAt",DateTime.MinValue);
   Call("RefreshAutoHide",false,now);Call("RefreshAutoHide",false,now.AddMilliseconds(2999));
   Check(window.IsVisible&&!(bool)Get("concealing"),"Window stays fully visible for the three-second grace period");
   Call("RefreshAutoHide",false,now.AddMilliseconds(3000));
   Check(window.IsVisible&&(bool)Get("concealing"),"At three seconds a fade begins without abruptly hiding the window");
   await Task.Delay(140);
   Check(window.IsVisible&&window.Opacity>0&&window.Opacity<1,"Whole-window opacity changes during the fade");
   Call("RefreshAutoHide",true,DateTime.UtcNow);await Task.Delay(180);
   Check(window.IsVisible&&!(bool)Get("concealing")&&window.Opacity>.98,"Re-entry cancels the fade and restores opacity");
   now=DateTime.UtcNow;Set("autoHideLeftAt",now);Call("RefreshAutoHide",false,now.AddMilliseconds(3001));await Task.Delay(450);
   Check(!window.IsVisible,"After the fade the real window is hidden and no longer intercepts clicks");
   Check((IntPtr)Get("desktopClickHook")!=IntPtr.Zero,"Hidden window still accepts the next desktop reveal");
   Call("HandleDesktopBackgroundClick",true,0);PauseTimer();
   Check(window.IsVisible&&Math.Abs(window.Opacity-1)<.01,"A second desktop reveal restores full opacity");
   Set("selected",window.Config.Groups[0]);int version=(int)Get("viewVersion");
   Call("HandleDesktopBackgroundClick",false,version-1);
   Check(Get("selected")!=null,"Stale background return cannot reset the selected category after an app launch");
   Call("HandleDesktopBackgroundClick",false,version);
   Check(Get("selected")==null,"Normal blank-background return still restores all categories");
   foreach(string guard in new[]{"dialogOpen","dragging"}){
    Set(guard,true);Set("autoHideLeftAt",DateTime.UtcNow.AddSeconds(-10));Call("RefreshBackdrop");
    Check(window.IsVisible&&!(bool)Get("concealing"),guard+" suspends automatic hiding");Set(guard,false);
   }
   Set("openMenus",1);Set("autoHideLeftAt",DateTime.UtcNow.AddSeconds(-10));Call("RefreshBackdrop");
   Check(window.IsVisible&&!(bool)Get("concealing"),"Open menus suspend automatic hiding");Set("openMenus",0);
   window.IsEnabled=false;Set("autoHideLeftAt",DateTime.UtcNow.AddSeconds(-10));Call("RefreshBackdrop");
   Check(window.IsVisible&&!(bool)Get("concealing"),"Disabled modal owner suspends automatic hiding");window.IsEnabled=true;
   Call("ToggleClickToReveal");PauseTimer();Call("RefreshAutoHide",false,DateTime.UtcNow.AddMinutes(1));
   Check(window.IsVisible&&!window.Config.ClickToReveal&&(IntPtr)Get("desktopClickHook")==IntPtr.Zero,"Always-visible mode disables automatic hiding and unnecessary mouse observation");
   Call("ToggleClickToReveal");PauseTimer();
   Check(Files.Read(Files.ConfigPath).ClickToReveal,"Mode setting survives a configuration round trip");
   exitCode=0;
  }catch(Exception ex){checks.Add("ERROR: "+ex);}
  finally{
   File.WriteAllText(Path.Combine(Files.Root,"click-reveal-tests.json"),Files.Json.Serialize(new {passed=exitCode==0,checks=checks}),System.Text.Encoding.UTF8);
   window.Exit();
  }
 }
}
