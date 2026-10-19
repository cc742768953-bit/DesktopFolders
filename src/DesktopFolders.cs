using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DesktopFolders {
 public sealed class Shortcut {
  public string Id {get;set;}
  public string Name {get;set;}
  public string Target {get;set;}
  public string OriginalTarget {get;set;}
  public override string ToString(){return Name;}
 }
 public sealed class Category {
  public string Id {get;set;}
  public string Name {get;set;}
  public bool IsCollection {get;set;}
  public List<Shortcut> Items {get;set;}
  public override string ToString(){return Name;}
 }
 public sealed class Configuration {
  public int Version {get;set;}
  public double Left {get;set;}
  public double Top {get;set;}
  public bool Locked {get;set;}
  public int IconSize {get;set;}
  public bool ClickToReveal {get;set;}
  public List<Category> Groups {get;set;}
 }
 public static class Files {
  public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
  public static readonly string ConfigPath=Path.Combine(Root,"config.json");
  public static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=8*1024*1024};
  public static string Resolve(string target) {
   if(String.IsNullOrWhiteSpace(target))throw new InvalidDataException("快捷方式路径为空");
   Uri uri;if(Uri.TryCreate(target,UriKind.Absolute,out uri)&&!uri.IsFile)return target;
   return Path.GetFullPath(Path.IsPathRooted(target)?target:Path.Combine(Root,target));
  }
  public static List<Shortcut> All(Configuration c){return c.Groups.SelectMany(g=>g.Items??new List<Shortcut>()).GroupBy(i=>i.Target,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();}
  public static List<Shortcut> Items(Configuration c,Category g){return g.IsCollection?All(c):g.Items;}
  public static void Validate(Configuration c){
   if(c==null||c.Version!=1||c.Groups==null||c.Groups.Count==0||c.Groups.Count>100)throw new InvalidDataException("分类配置无效");
   if(c.Groups.Any(g=>g==null||String.IsNullOrWhiteSpace(g.Id)||String.IsNullOrWhiteSpace(g.Name)||g.Items==null))throw new InvalidDataException("分类名称或项目缺失");
   if(c.Groups.Select(g=>g.Id).Distinct().Count()!=c.Groups.Count)throw new InvalidDataException("分类标识重复");
   if(c.IconSize==0)c.IconSize=48;
   if(c.IconSize<32||c.IconSize>64)throw new InvalidDataException("图标大小须在 32 到 64 之间");
   foreach(var g in c.Groups)foreach(var i in g.Items){if(i==null||String.IsNullOrWhiteSpace(i.Name))throw new InvalidDataException("快捷方式名称缺失");Resolve(i.Target);}
  }
  public static Configuration Read(string path){var c=Json.Deserialize<Configuration>(File.ReadAllText(path,Encoding.UTF8));Validate(c);return c;}
  public static Configuration Load(){
   if(!File.Exists(ConfigPath)){var seed=FirstRun.Create();SaveTo(ConfigPath,seed);return seed;}
   try{var c=Read(ConfigPath);FirstRun.RefreshBuiltIns(c);return c;}catch(Exception ex){
    Log("Configuration read failed: "+ex.Message);
    var backup=ConfigPath+".bak";
    if(!File.Exists(backup))throw new InvalidDataException("分类配置无法读取，请使用备份恢复。原文件已保留。",ex);
    var c=Read(backup);File.Copy(ConfigPath,ConfigPath+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss"),false);
    File.Copy(backup,ConfigPath,true);Log("Recovered configuration from verified backup.");return c;
   }
  }
  public static void SaveTo(string path,Configuration c){
   Validate(c);var temp=path+".tmp-"+Guid.NewGuid().ToString("N");
   try{
    var bytes=Encoding.UTF8.GetBytes(Json.Serialize(c));
    using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}
    Read(temp);
    if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static void Log(string text){try{var path=Path.Combine(Root,"app.log");if(File.Exists(path)&&new FileInfo(path).Length>512*1024)File.Copy(path,path+".previous",true);if(File.Exists(path)&&new FileInfo(path).Length>512*1024)File.WriteAllText(path,"");File.AppendAllText(path,DateTime.Now.ToString("s")+" "+text+Environment.NewLine,Encoding.UTF8);}catch{}}
 }
 public static class UiTrace {
  public static bool Enabled;
  static readonly Stopwatch Watch=Stopwatch.StartNew();
  public static void Mark(string name){if(!Enabled)return;try{File.AppendAllText(Path.Combine(Files.Root,"ui-trace.log"),Watch.Elapsed.TotalSeconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" "+name+Environment.NewLine,Encoding.UTF8);}catch{}}
 }
 public static class Native {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct SHFILEINFO {public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName;}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr SHGetFileInfo(string path,uint attr,out SHFILEINFO info,uint size,uint flags);
  [DllImport("user32.dll")]public static extern bool DestroyIcon(IntPtr icon);
  [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
  [DllImport("user32.dll")]public static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")]public static extern int SetWindowLong(IntPtr h,int index,int value);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern uint RegisterWindowMessage(string name);
  [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)]public struct POINT {public int X,Y;}
  [DllImport("user32.dll")]public static extern bool GetCursorPos(out POINT point);
  [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(POINT point);
  [DllImport("user32.dll")]public static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("user32.dll")]public static extern bool IsChild(IntPtr parent,IntPtr child);
  [DllImport("user32.dll")]public static extern bool IsWindowEnabled(IntPtr window);
  public delegate IntPtr MouseHook(int code,IntPtr message,IntPtr data);
  [StructLayout(LayoutKind.Sequential)]public struct MOUSEHOOKDATA {public POINT Point;public uint MouseData,Flags,Time;public UIntPtr Extra;}
  [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr SetWindowsHookEx(int type,MouseHook callback,IntPtr module,uint thread);
  [DllImport("user32.dll")]public static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")]public static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr GetModuleHandle(string name);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr window,StringBuilder name,int length);
  public static bool IsDesktopWindow(IntPtr window){if(window==IntPtr.Zero)return false;var name=new StringBuilder(128);GetClassName(window,name,name.Capacity);var value=name.ToString();return value=="Progman"||value=="WorkerW";}
  [StructLayout(LayoutKind.Sequential)]public struct RECT {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]public struct APPBARDATA {public uint Size;public IntPtr Window;public uint Callback;public uint Edge;public RECT Rect;public IntPtr Param;}
  [DllImport("shell32.dll")]public static extern UIntPtr SHAppBarMessage(uint message,ref APPBARDATA data);
  public static bool TaskbarAutoHide(){var d=new APPBARDATA {Size=(uint)Marshal.SizeOf(typeof(APPBARDATA))};return (SHAppBarMessage(4,ref d).ToUInt64()&1)!=0;}
  public static void Bottom(IntPtr h){if(h!=IntPtr.Zero){UiTrace.Mark("LowerWindow");SetWindowPos(h,new IntPtr(1),0,0,0,0,0x13);}}
 }
 public static class Icons {
  static readonly Dictionary<string,ImageSource> Memory=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
  public static int Failures;
  public static ImageSource Get(Shortcut item){
   string target;try{target=Files.Resolve(item.Target);}catch{return null;}
   ImageSource result;if(Memory.TryGetValue(target,out result))return result;
   try{
    string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(target.ToLowerInvariant()))).Replace("-","");
    var dir=Path.Combine(Files.Root,"Icons");Directory.CreateDirectory(dir);var file=Path.Combine(dir,key+".png");
    if(File.Exists(file)){var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.UriSource=new Uri(file);b.EndInit();b.Freeze();Memory[target]=b;return b;}
    Native.SHFILEINFO info;Native.SHGetFileInfo(target,0,out info,(uint)Marshal.SizeOf(typeof(Native.SHFILEINFO)),0x100);
    if(info.Icon!=IntPtr.Zero){try{var b=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());b.Freeze();result=b;var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using(var stream=File.Create(file))enc.Save(stream);}finally{Native.DestroyIcon(info.Icon);}}
    if(result==null){Failures++;result=Imaging.CreateBitmapSourceFromHIcon(System.Drawing.SystemIcons.Application.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());result.Freeze();}
    Memory[target]=result;return result;
   }catch(Exception ex){Failures++;Files.Log("Icon read: "+item.Name+"; "+ex.Message);Memory[target]=null;return null;}
  }
 }
 public static class Ui {
  public static SolidColorBrush Color(string hex){return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);}
  public static TextBlock Text(string text,double size,bool bold=false){return new TextBlock {Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Foreground=Brushes.White,FontFamily=new FontFamily("Microsoft YaHei UI"),TextTrimming=TextTrimming.CharacterEllipsis};}
  public static Button Button(string text,Action action){var b=new Button {Content=text,Padding=new Thickness(12,6,12,6),Margin=new Thickness(3),MinHeight=30,Cursor=Cursors.Hand,ClickMode=ClickMode.Press};b.Click+=(s,e)=>action();return b;}
  static readonly ControlTemplate DesktopTemplate=CreateDesktopTemplate();
  static readonly Style DesktopFocus=CreateDesktopFocus();
  static ControlTemplate CreateDesktopTemplate(){
   var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
   var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));content.SetValue(ContentPresenter.ContentTemplateProperty,new TemplateBindingExtension(ContentControl.ContentTemplateProperty));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);return new ControlTemplate(typeof(Button)){VisualTree=border};
  }
  static Style CreateDesktopFocus(){var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BorderBrushProperty,Color("#A0FFFFFF"));border.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,1));border.SetValue(FrameworkElement.MarginProperty,new Thickness(10,0,10,2));var style=new Style(typeof(Control));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Control)){VisualTree=border}));return style;}
  public static Button DesktopButton(){return new Button {Template=DesktopTemplate,FocusVisualStyle=DesktopFocus,Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,ClickMode=ClickMode.Press,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};}
  public static Image DesktopIcon(Shortcut item,double size){var image=new Image {Source=Icons.Get(item),Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);return image;}
  public static TextBlock DesktopText(string text,double size,bool bold=false){var block=Text(text,size,bold);block.Effect=new DropShadowEffect {Color=Colors.Black,BlurRadius=5,ShadowDepth=1,Opacity=.9};return block;}
  public static void Name(DependencyObject element,string name){AutomationProperties.SetName(element,name);}
  public static ContextMenu Menu(){return new ContextMenu {FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=13};}
  public static void MenuItem(ContextMenu menu,string title,Action action){var item=new MenuItem {Header=title};item.Click+=(s,e)=>action();menu.Items.Add(item);}
  public static string Ask(Window owner,string title,string current){
   var d=new Window {Title=title,Width=370,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Color("#F2F5FA"),FontFamily=new FontFamily("Microsoft YaHei UI"),ShowInTaskbar=false};
   var p=new StackPanel {Margin=new Thickness(20)};d.Content=p;var input=new TextBox {Text=current??"",Padding=new Thickness(8),Margin=new Thickness(0,5,0,12),MaxLength=60};Name(input,"分类名称");p.Children.Add(input);var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};p.Children.Add(buttons);
   var ok=Button("保存",()=>{if(!String.IsNullOrWhiteSpace(input.Text)){d.DialogResult=true;d.Close();}});ok.IsDefault=true;buttons.Children.Add(ok);var cancel=Button("取消",()=>d.Close());cancel.IsCancel=true;buttons.Children.Add(cancel);d.Loaded+=(s,e)=>{input.Focus();input.SelectAll();};return d.ShowDialog()==true?input.Text.Trim():null;
  }
 }
 public static class BrandIcons {
  public static ImageSource WindowImage(){using(var stream=typeof(BrandIcons).Assembly.GetManifestResourceStream("DesktopFolders.Main.ico")){var image=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.OrderByDescending(f=>f.PixelWidth).First();image.Freeze();return image;}}
  public static System.Drawing.Icon TrayIcon(){using(var stream=typeof(BrandIcons).Assembly.GetManifestResourceStream("DesktopFolders.Tray.ico"))using(var icon=new System.Drawing.Icon(stream,new System.Drawing.Size(32,32)))return (System.Drawing.Icon)icon.Clone();}
 }
 public sealed class MainWindow:Window {
  public Configuration Config;
  readonly StackPanel root=new StackPanel();
  readonly StackPanel header=new StackPanel {Orientation=Orientation.Horizontal,Opacity=0,IsHitTestVisible=false};
  readonly StackPanel body=new StackPanel();
  readonly TextBlock status=Ui.Text("",11);
  readonly TextBox search=new TextBox();
  Category selected;
  string query="";
  bool ready;
  bool closing;
  bool dragging;
  bool dialogOpen;
  Native.MouseHook desktopClickCallback;
  IntPtr desktopClickHook;
  int viewVersion;
  IntPtr handle;
  Forms.NotifyIcon tray;
  System.Drawing.Icon trayIcon;
  DispatcherTimer saveTimer;
  DispatcherTimer hoverTimer;
  readonly SolidColorBrush hoverSurface=new SolidColorBrush(System.Windows.Media.Color.FromRgb(21,32,46)){Opacity=0};
  bool backdropVisible;
  int openMenus;
  DateTime pointerLeftAt=DateTime.MinValue;
  DateTime autoHideLeftAt=DateTime.MinValue;
  bool concealing;
  int concealVersion;
  static readonly uint TaskbarCreated=Native.RegisterWindowMessage("TaskbarCreated");
  static readonly uint ShowMessage=Native.RegisterWindowMessage("DesktopFolders.Show.v1");
  static readonly uint ManageMessage=Native.RegisterWindowMessage("DesktopFolders.Manage.v1");
  public MainWindow(Configuration config,bool preview,bool firstRun=false){
   Config=config;Title="桌面分类";Icon=BrandIcons.WindowImage();Width=536;Height=692;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=preview;ShowActivated=false;Topmost=false;FontFamily=new FontFamily("Microsoft YaHei UI");
   Left=preview?Math.Max(40,SystemParameters.WorkArea.Width-Width-60):Config.Left;Top=Config.Top;
   var frame=new Border {Padding=new Thickness(8),Background=Brushes.Transparent,Child=root};Content=frame;
    KeyboardNavigation.SetTabNavigation(header,KeyboardNavigationMode.None);root.Children.Add(header);
   search.Margin=new Thickness(6,2,6,12);search.Padding=new Thickness(10,7,10,7);search.FontSize=14;search.Visibility=Visibility.Collapsed;search.Background=Ui.Color("#ECF5F8FE");search.Foreground=Ui.Color("#152539");search.BorderThickness=new Thickness(0);Ui.Name(search,"搜索快捷方式");search.TextChanged+=(s,e)=>{query=search.Text;RenderBody();};root.Children.Add(search);
   root.Children.Add(body);status.Margin=new Thickness(12,8,0,0);status.Foreground=Ui.Color("#CFDEE9F5");root.Children.Add(status);
   saveTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(500)};saveTimer.Tick+=(s,e)=>{saveTimer.Stop();Persist();};
    hoverTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(100)};hoverTimer.Tick+=(s,e)=>RefreshBackdrop();
    MouseEnter+=(s,e)=>RefreshBackdrop();MouseLeave+=(s,e)=>RefreshBackdrop();
     IsVisibleChanged+=(s,e)=>{if(IsVisible){autoHideLeftAt=DateTime.UtcNow;if(ready)hoverTimer.Start();}else{hoverTimer.Stop();pointerLeftAt=DateTime.MinValue;autoHideLeftAt=DateTime.MinValue;SetBackdrop(false);}if(ready&&!closing)UpdateDesktopClickObserver();};
   SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(this).Handle;if(!preview)Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80)&~0x40000);HwndSource.FromHwnd(handle).AddHook(Message);};
      Loaded+=(s,e)=>{ClampToDisplay();ready=true;Render();Native.Bottom(handle);hoverTimer.Start();RefreshBackdrop();SetupTray();if(Config.ClickToReveal)ConcealPanel();if(firstRun)tray.ShowBalloonTip(7000,"桌面分类已准备好","点击桌面空白处展开分类；移出窗口 3 秒后渐隐。右键托盘可管理分类或切换常驻显示。",Forms.ToolTipIcon.Info);Files.Log("Ready "+Files.All(Config).Count+" shortcuts; "+Config.Groups.Count+" groups; pid="+Process.GetCurrentProcess().Id+" hwnd="+handle+" visible="+IsVisible+" x="+Left+" y="+Top+" appearance=transparent-hover iconSize="+Config.IconSize+" clickToReveal="+Config.ClickToReveal+" layering=event-driven");};
   Deactivated+=(s,e)=>{Dispatcher.BeginInvoke(new Action(()=>{if(!IsActive&&!IsMouseOver&&!dialogOpen&&!OwnedWindows.Cast<Window>().Any(w=>w.IsVisible))Native.Bottom(handle);}));};
   LocationChanged+=(s,e)=>{if(ready&&!dragging)QueueSave();};
   StateChanged+=(s,e)=>{if(WindowState==WindowState.Minimized)Dispatcher.BeginInvoke(new Action(()=>{WindowState=WindowState.Normal;Native.Bottom(handle);}));};
      Closing+=(s,e)=>{if(!closing){e.Cancel=true;Hide();if(tray!=null)tray.ShowBalloonTip(1800,"桌面分类","组件已隐藏，可从托盘重新显示。",Forms.ToolTipIcon.Info);}else{Persist();StopDesktopClickObserver();hoverTimer.Stop();if(tray!=null)tray.Dispose();if(trayIcon!=null)trayIcon.Dispose();}};
   Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
   Closed+=(s,e)=>{Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;};
    PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){ReturnToCategories();e.Handled=true;}if(e.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control){ToggleSearch();e.Handled=true;}};
    PreviewMouseLeftButtonDown+=(s,e)=>{if(!Expanded||dialogOpen||openMenus>0)return;var node=e.OriginalSource as DependencyObject;while(node!=null&&node!=this){if(node==header||node is ButtonBase||node is TextBoxBase||node is ScrollBar||node is Thumb)return;var content=node as FrameworkContentElement;node=content!=null?content.Parent:VisualTreeHelper.GetParent(node);}ReturnToCategories();};
     var menu=TrackMenu(Ui.Menu());Ui.MenuItem(menu,"管理分类",Manage);Ui.MenuItem(menu,"图标大小…",AdjustIconSize);Ui.MenuItem(menu,"搜索快捷方式",ToggleSearch);Ui.MenuItem(menu,"锁定 / 解锁位置",ToggleLock);Ui.MenuItem(menu,"退出桌面分类",Exit);ContextMenu=menu;
  }
   bool Expanded {get{return selected!=null||!String.IsNullOrWhiteSpace(query);}}
   void ReturnToCategories(){selected=null;query="";search.Text="";search.Visibility=Visibility.Collapsed;RenderBody();}
   void StopDesktopClickObserver(){if(desktopClickHook!=IntPtr.Zero){Native.UnhookWindowsHookEx(desktopClickHook);desktopClickHook=IntPtr.Zero;}}
   void UpdateDesktopClickObserver(){
   if(closing||(!Config.ClickToReveal&&(!IsVisible||!Expanded))){StopDesktopClickObserver();return;}if(desktopClickHook!=IntPtr.Zero)return;
    if(desktopClickCallback==null)desktopClickCallback=ObserveDesktopClick;
    desktopClickHook=Native.SetWindowsHookEx(14,desktopClickCallback,Native.GetModuleHandle(null),0);
    if(desktopClickHook==IntPtr.Zero)Files.Log("Desktop click observer failed: "+Marshal.GetLastWin32Error());
   }
   IntPtr ObserveDesktopClick(int code,IntPtr message,IntPtr data){
    try{long kind=message.ToInt64();bool reveal=Config.ClickToReveal&&!IsVisible&&kind==0x202;bool back=IsVisible&&Expanded&&kind==0x201;if(code>=0&&(reveal||back)&&!dialogOpen&&openMenus==0&&!dragging&&!closing){var point=((Native.MOUSEHOOKDATA)Marshal.PtrToStructure(data,typeof(Native.MOUSEHOOKDATA))).Point;var hit=Native.WindowFromPoint(point);if(Native.IsDesktopWindow(hit)||Native.IsDesktopWindow(Native.GetAncestor(hit,2))){int observedVersion=viewVersion;Dispatcher.BeginInvoke(new Action(()=>HandleDesktopBackgroundClick(reveal,observedVersion)));}}}catch(Exception ex){Files.Log("Desktop click observer: "+ex.Message);}
    return Native.CallNextHookEx(desktopClickHook,code,message,data);
   }
   void HandleDesktopBackgroundClick(bool reveal,int observedVersion){if(closing||dialogOpen||openMenus>0||dragging)return;if(reveal){if(Config.ClickToReveal&&!IsVisible){ReturnToCategories();RevealPanel();}}else if(observedVersion==viewVersion&&IsVisible&&Expanded)ReturnToCategories();}
   bool PointerInside(){
    if(!IsVisible||handle==IntPtr.Zero||WindowState==WindowState.Minimized)return false;
    Native.POINT point;if(!Native.GetCursorPos(out point))return IsMouseOver;
    var local=PointFromScreen(new Point(point.X,point.Y));if(local.X<0||local.Y<0||local.X>=ActualWidth||local.Y>=ActualHeight)return false;
    var hit=Native.WindowFromPoint(point);if(hit==handle||Native.IsChild(handle,hit))return true;
    return Native.IsDesktopWindow(hit)||Native.IsDesktopWindow(Native.GetAncestor(hit,2));
   }
   void RefreshBackdrop(){
    if(!IsVisible)return;
    bool active=openMenus>0||dragging||dialogOpen||!IsEnabled||OwnedWindows.Cast<Window>().Any(w=>w.IsVisible)||(handle!=IntPtr.Zero&&!Native.IsWindowEnabled(handle))||PointerInside();
    if(RefreshAutoHide(active,DateTime.UtcNow))return;
    if(active){pointerLeftAt=DateTime.MinValue;SetBackdrop(true);return;}
    if(!backdropVisible)return;if(pointerLeftAt==DateTime.MinValue)pointerLeftAt=DateTime.UtcNow;
    if((DateTime.UtcNow-pointerLeftAt).TotalMilliseconds>=140)SetBackdrop(false);
   }
   bool RefreshAutoHide(bool active,DateTime now){
    if(!Config.ClickToReveal||active){autoHideLeftAt=DateTime.MinValue;CancelConceal();return false;}
    if(autoHideLeftAt==DateTime.MinValue)autoHideLeftAt=now;
    if((now-autoHideLeftAt).TotalMilliseconds<3000)return false;
    FadeConcealPanel();return true;
   }
   void CancelConceal(){if(!concealing)return;concealing=false;concealVersion++;BeginAnimation(OpacityProperty,new DoubleAnimation {To=1,Duration=TimeSpan.FromMilliseconds(120),EasingFunction=new QuadraticEase {EasingMode=EasingMode.EaseOut}});UiTrace.Mark("PanelFadeCancelled");}
   void FadeConcealPanel(){if(concealing||!IsVisible)return;concealing=true;int version=++concealVersion;var animation=new DoubleAnimation {To=0,Duration=TimeSpan.FromMilliseconds(300),EasingFunction=new QuadraticEase {EasingMode=EasingMode.EaseInOut}};animation.Completed+=(s,e)=>{if(concealing&&version==concealVersion)ConcealPanel();};BeginAnimation(OpacityProperty,animation);UiTrace.Mark("PanelFadeStarted");}
   void ConcealPanel(){concealing=false;concealVersion++;Hide();BeginAnimation(OpacityProperty,null);Opacity=1;UiTrace.Mark("PanelHidden");UpdateDesktopClickObserver();}
   void RevealPanel(){concealing=false;concealVersion++;BeginAnimation(OpacityProperty,null);Opacity=1;autoHideLeftAt=DateTime.UtcNow;Show();WindowState=WindowState.Normal;Native.Bottom(handle);hoverTimer.Start();UpdateDesktopClickObserver();RefreshBackdrop();UiTrace.Mark("PanelShown");}
   void ToggleClickToReveal(){Config.ClickToReveal=!Config.ClickToReveal;Persist();UpdateDesktopClickObserver();autoHideLeftAt=DateTime.UtcNow;RenderHeader();if(!Config.ClickToReveal)RevealPanel();}
    void SetBackdrop(bool visible){if(backdropVisible==visible)return;backdropVisible=visible;UiTrace.Mark(visible?"BackdropOn":"BackdropOff");var duration=TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation?150:0);header.IsHitTestVisible=visible;KeyboardNavigation.SetTabNavigation(header,visible?KeyboardNavigationMode.Continue:KeyboardNavigationMode.None);header.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation {To=visible?1:0,Duration=duration,EasingFunction=new QuadraticEase {EasingMode=EasingMode.EaseOut}});hoverSurface.BeginAnimation(Brush.OpacityProperty,new DoubleAnimation {To=visible ? .60 : 0,Duration=duration,EasingFunction=new QuadraticEase {EasingMode=EasingMode.EaseOut}});}
   ContextMenu TrackMenu(ContextMenu menu){menu.Opened+=(s,e)=>{openMenus++;RefreshBackdrop();};menu.Closed+=(s,e)=>{openMenus=Math.Max(0,openMenus-1);RefreshBackdrop();};return menu;}
  IntPtr Message(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if((uint)msg==TaskbarCreated)Dispatcher.BeginInvoke(new Action(()=>{if(tray!=null){tray.Visible=false;tray.Visible=true;}if(IsVisible)Native.Bottom(handle);}));if((uint)msg==ShowMessage)Dispatcher.BeginInvoke(new Action(RevealPanel));if((uint)msg==ManageMessage)Dispatcher.BeginInvoke(new Action(()=>{RevealPanel();Manage();}));return IntPtr.Zero;}
  void DisplayChanged(object s,EventArgs e){Dispatcher.BeginInvoke(new Action(()=>{ClampToDisplay();Native.Bottom(handle);}));}
  public void ClampToDisplay(){
   var a=new Rect(Left,Top,Width,Height);var screens=Forms.Screen.AllScreens;bool seen=false;
   var source=PresentationSource.FromVisual(this);double sx=1,sy=1;if(source!=null){sx=source.CompositionTarget.TransformToDevice.M11;sy=source.CompositionTarget.TransformToDevice.M22;}
   foreach(var screen in screens){var r=screen.WorkingArea;var area=new Rect(r.X/sx,r.Y/sy,r.Width/sx,r.Height/sy);if(area.Contains(new Point(a.Left+80,a.Top+25))){seen=true;Top=Math.Min(Math.Max(Top,area.Top),Math.Max(area.Top,area.Bottom-Height));Left=Math.Min(Math.Max(Left,area.Left),Math.Max(area.Left,area.Right-Width));break;}}
   if(!seen){Left=40;Top=100;}
  }
  void QueueSave(){saveTimer.Stop();saveTimer.Start();}
  public void Persist(){try{Config.Left=Left;Config.Top=Top;Files.SaveTo(Files.ConfigPath,Config);}catch(Exception ex){status.Text="保存失败："+ex.Message;Files.Log("Save failed: "+ex.Message);}}
  public void Render(){RenderHeader();RenderBody();}
  void RenderHeader(){
   header.Children.Clear();
    var grip=new Border {Background=hoverSurface,CornerRadius=new CornerRadius(9),Margin=new Thickness(5,4,4,14),Padding=new Thickness(11,7,11,7),Width=288,Cursor=Config.Locked?Cursors.Arrow:Cursors.SizeAll};
    grip.Child=Ui.DesktopText("桌面分类",13,true);Ui.Name(grip,"拖动桌面分类");grip.ToolTip=Config.Locked?"位置已锁定，可在菜单中解锁":"拖动调整位置";
   grip.MouseLeftButtonDown+=(s,e)=>{if(!Config.Locked&&e.ButtonState==MouseButtonState.Pressed){dragging=true;try{DragMove();}catch{}finally{dragging=false;ClampToDisplay();Persist();}}};header.Children.Add(grip);
     header.Children.Add(HeaderButton("⌕","搜索",ToggleSearch));header.Children.Add(HeaderButton("管理","管理分类",Manage));header.Children.Add(HeaderButton("⋯","更多",()=>{var m=TrackMenu(Ui.Menu());Ui.MenuItem(m,"图标大小…",AdjustIconSize);Ui.MenuItem(m,Config.ClickToReveal?"切换为常驻显示":"启用桌面点击展开",ToggleClickToReveal);Ui.MenuItem(m,Config.Locked?"解锁位置":"锁定位置",ToggleLock);Ui.MenuItem(m,"恢复默认位置",()=>{Left=40;Top=100;Persist();});Ui.MenuItem(m,"备份分类…",Export);Ui.MenuItem(m,"导入分类…",Import);Ui.MenuItem(m,"隐藏组件",Hide);Ui.MenuItem(m,"退出桌面分类",Exit);m.PlacementTarget=header;m.IsOpen=true;}));
  }
   Button HeaderButton(string text,string name,Action action){var b=Ui.DesktopButton();b.Content=Ui.DesktopText(text,12);b.Click+=(s,e)=>action();b.Margin=new Thickness(2,4,2,14);b.Background=hoverSurface;b.MinHeight=30;b.FontSize=12;b.Padding=new Thickness(9,5,9,5);b.ToolTip=name;Ui.Name(b,name);return b;}
  void ToggleSearch(){search.Visibility=search.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;if(search.Visibility==Visibility.Visible)search.Focus();else{search.Text="";query="";}RenderBody();}
  void ToggleLock(){Config.Locked=!Config.Locked;Persist();RenderHeader();}
  public void Manage(){if(dialogOpen)return;dialogOpen=true;try{var d=new ManagerWindow(this);d.Loaded+=(s,e)=>d.Activate();d.ShowDialog();}finally{dialogOpen=false;}Render();Native.Bottom(handle);}
   public void AdjustIconSize(){if(dialogOpen)return;dialogOpen=true;try{var d=new IconSizeWindow(this);d.Loaded+=(s,e)=>d.Activate();d.ShowDialog();}finally{dialogOpen=false;RefreshBackdrop();}Native.Bottom(handle);}
   public void PreviewIconSize(int size){Config.IconSize=Math.Max(32,Math.Min(64,size));RenderBody();}
  void Export(){var d=new SaveFileDialog {Title="备份分类",FileName="桌面分类备份.json",Filter="分类配置 (*.json)|*.json"};if(d.ShowDialog(this)==true){try{Persist();File.Copy(Files.ConfigPath,d.FileName,true);status.Text="分类已备份";}catch(Exception ex){MessageBox.Show(this,ex.Message,"备份失败");}}}
  void Import(){var d=new OpenFileDialog {Title="导入分类",Filter="分类配置 (*.json)|*.json"};if(d.ShowDialog(this)!=true)return;try{var c=Files.Read(d.FileName);if(c.Groups.SelectMany(g=>g.Items).Any(i=>!Path.IsPathRooted(i.Target)&&!File.Exists(Files.Resolve(i.Target))))throw new InvalidDataException("备份引用的本地快捷方式不在当前安装目录，请同时恢复 Shortcuts 文件夹。");if(MessageBox.Show(this,"用备份中的分类替换当前分类？当前配置会保留一份备份。","导入分类",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;Config.Groups=c.Groups;selected=null;Persist();Render();}catch(Exception ex){MessageBox.Show(this,ex.Message,"导入失败");}}
  void SetupTray(){trayIcon=BrandIcons.TrayIcon();tray=new Forms.NotifyIcon {Icon=trayIcon,Text="桌面分类",Visible=true};var menu=new Forms.ContextMenuStrip();menu.Items.Add("显示分类",null,(s,e)=>Dispatcher.Invoke(new Action(RevealPanel)));menu.Items.Add("管理分类",null,(s,e)=>Dispatcher.Invoke(new Action(()=>{RevealPanel();Manage();})));menu.Items.Add("切换点击展开 / 常驻显示",null,(s,e)=>Dispatcher.Invoke(new Action(ToggleClickToReveal)));menu.Items.Add("退出",null,(s,e)=>Dispatcher.Invoke(new Action(Exit)));tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(new Action(RevealPanel));}
  public void Exit(){closing=true;Close();Application.Current.Shutdown();}
   void RenderBody(){
    viewVersion++;body.Children.Clear();status.Text="";UpdateDesktopClickObserver();
   bool searching=!String.IsNullOrWhiteSpace(query);
   if(selected!=null||searching){
    var title=new DockPanel {Margin=new Thickness(9,0,9,12)};
     var back=HeaderButton("‹ 分类","返回分类",ReturnToCategories);back.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(back,Dock.Left);title.Children.Add(back);
    var items=searching?Files.All(Config).Where(i=>i.Name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0).ToList():Files.Items(Config,selected);
     var label=Ui.DesktopText(searching?"搜索结果":selected.Name,15,true);label.VerticalAlignment=VerticalAlignment.Center;title.Children.Add(label);body.Children.Add(title);
     var wrap=new WrapPanel {Width=496};foreach(var i in items)wrap.Children.Add(ItemButton(i,selected));var scroll=new ScrollViewer {Content=wrap,MaxHeight=536,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};body.Children.Add(scroll);if(items.Count==0)status.Text="这里还没有快捷方式，可在管理分类中添加。";
   }else{
     var wrap=new WrapPanel {Width=498};foreach(var group in Config.Groups)wrap.Children.Add(CategoryTile(group));body.Children.Add(new ScrollViewer {Content=wrap,MaxHeight=595,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   }
  }
  Button CategoryTile(Category group){
     var items=Files.Items(Config,group);double iconSize=Config.IconSize*.75;double cardHeight=Math.Max(131,iconSize*2+58);var b=Ui.DesktopButton();b.Width=156;b.Height=cardHeight;b.Margin=new Thickness(5,1,5,9);b.Background=Brushes.Transparent;b.Padding=new Thickness(0);b.AllowDrop=!group.IsCollection;Ui.Name(b,group.Name+"，"+items.Count+" 个快捷方式");b.ToolTip=group.Name;
     var panel=new Border {Width=156,Height=cardHeight,CornerRadius=new CornerRadius(16),Background=hoverSurface,BorderThickness=new Thickness(0),Padding=new Thickness(12)};b.Content=panel;
    var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center};panel.Child=stack;var iconRegion=new Grid {Height=iconSize*2+8};var rows=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};iconRegion.Children.Add(rows);int count=Math.Min(4,items.Count);
    for(int start=0;start<count;start+=2){var row=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,start==0?0:8,0,0)};for(int j=start;j<Math.Min(start+2,count);j++){var image=Ui.DesktopIcon(items[j],iconSize);image.Margin=new Thickness(6,0,6,0);row.Children.Add(image);}rows.Children.Add(row);}stack.Children.Add(iconRegion);
     var name=Ui.DesktopText(group.Name,13,true);name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0,10,0,0);stack.Children.Add(name);
   b.Click+=(s,e)=>{selected=group;RenderBody();};
   b.Drop+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)){AddPaths(group,(string[])e.Data.GetData(DataFormats.FileDrop));e.Handled=true;}};
   b.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
    var menu=TrackMenu(Ui.Menu());if(!group.IsCollection){Ui.MenuItem(menu,"添加快捷方式…",()=>PickFiles(group));Ui.MenuItem(menu,"重命名分类…",()=>{var renamed=Ui.Ask(this,"重命名分类",group.Name);if(renamed!=null){group.Name=renamed;Persist();Render();}});}Ui.MenuItem(menu,"管理分类",Manage);b.ContextMenu=menu;return b;
  }
  Button ItemButton(Shortcut item,Category group){
     var b=Ui.DesktopButton();b.Width=116;b.Height=Math.Max(110,Config.IconSize+62);b.Margin=new Thickness(4);b.Background=hoverSurface;b.Padding=new Thickness(10);Ui.Name(b,item.Name);b.ToolTip=item.Name;
     var content=new Grid {Width=96,VerticalAlignment=VerticalAlignment.Center};content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(Config.IconSize)});content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(8)});content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(34)});content.Children.Add(Ui.DesktopIcon(item,Config.IconSize));var t=Ui.DesktopText(item.Name,11);t.TextAlignment=TextAlignment.Center;t.TextWrapping=TextWrapping.Wrap;t.MaxHeight=34;t.VerticalAlignment=VerticalAlignment.Top;Grid.SetRow(t,2);content.Children.Add(t);b.Content=content;
   b.Click+=(s,e)=>Launch(item);
    var menu=TrackMenu(Ui.Menu());Ui.MenuItem(menu,"打开",()=>Launch(item));var move=new MenuItem {Header="移动到分类"};foreach(var g in Config.Groups.Where(g=>!g.IsCollection)){var dest=g;var mi=new MenuItem {Header=g.Name};mi.Click+=(s,e)=>{MoveItem(item,dest);};move.Items.Add(mi);}menu.Items.Add(move);if(group!=null&&!group.IsCollection)Ui.MenuItem(menu,"从此分类移除",()=>{group.Items.Remove(item);Persist();Render();});b.ContextMenu=menu;return b;
  }
  public void MoveItem(Shortcut item,Category dest){foreach(var g in Config.Groups.Where(g=>!g.IsCollection))g.Items.RemoveAll(i=>String.Equals(i.Target,item.Target,StringComparison.OrdinalIgnoreCase));if(!dest.Items.Any(i=>String.Equals(i.Target,item.Target,StringComparison.OrdinalIgnoreCase)))dest.Items.Add(item);Persist();Render();}
   public void Launch(Shortcut item){viewVersion++;try{var target=Files.Resolve(item.Target);bool exists=File.Exists(target)||Directory.Exists(target);Uri uri;bool remote=Uri.TryCreate(target,UriKind.Absolute,out uri)&&!uri.IsFile;if(!exists&&!remote)throw new FileNotFoundException("快捷方式已不存在，请在管理分类中重新添加。",target);Process.Start(new ProcessStartInfo {FileName=target,UseShellExecute=true,WorkingDirectory=remote?Files.Root:Directory.Exists(target)?target:Path.GetDirectoryName(target)});Files.Log("Launch succeeded: "+item.Name);Native.Bottom(handle);}catch(Exception ex){Files.Log("Launch failed: "+item.Name+"; "+ex.Message);MessageBox.Show(this,"无法打开「"+item.Name+"」\n"+ex.Message,"桌面分类");}}
  public void PickFiles(Category group){var d=new OpenFileDialog {Title="添加快捷方式或程序",Multiselect=true,DereferenceLinks=false,Filter="快捷方式与程序|*.lnk;*.url;*.exe|所有文件|*.*"};if(d.ShowDialog(this)==true)AddPaths(group,d.FileNames);}
  public void AddPaths(Category group,string[] paths){
   if(group==null||group.IsCollection)return;int count=0;
   try{foreach(var path in paths){if(!File.Exists(path)&&!Directory.Exists(path))continue;var known=Files.All(Config).FirstOrDefault(i=>String.Equals(i.OriginalTarget,path,StringComparison.OrdinalIgnoreCase)||String.Equals(Files.Resolve(i.Target),path,StringComparison.OrdinalIgnoreCase));string target=known!=null?known.Target:path;var ext=Path.GetExtension(path);if(known==null&&(ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase))){Directory.CreateDirectory(Path.Combine(Files.Root,"Shortcuts"));var name=Guid.NewGuid().ToString("N")+ext;File.Copy(path,Path.Combine(Files.Root,"Shortcuts",name));target=Path.Combine("Shortcuts",name);}if(!group.Items.Any(i=>String.Equals(Files.Resolve(i.Target),Files.Resolve(target),StringComparison.OrdinalIgnoreCase))){group.Items.Add(known??new Shortcut {Id=Guid.NewGuid().ToString("N"),Name=Path.GetFileNameWithoutExtension(path),Target=target,OriginalTarget=path});count++;}}Persist();Render();status.Text="已添加 "+count+" 个快捷方式";}catch(Exception ex){MessageBox.Show(this,ex.Message,"添加失败");}
  }
 }
  public sealed class IconSizeWindow:Window {
   public IconSizeWindow(MainWindow owner){
    Owner=owner;Title="图标大小";Width=430;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");Background=Ui.Color("#F2F5FA");int original=owner.Config.IconSize;bool saved=false;
    var panel=new StackPanel {Margin=new Thickness(22)};Content=panel;
    panel.Children.Add(new TextBlock {Text="调整图标大小",FontSize=21,FontWeight=FontWeights.SemiBold,Foreground=Ui.Color("#192D45")});
    panel.Children.Add(new TextBlock {Text="分类预览和展开后的图标一起调整，拖动时即时预览。",TextWrapping=TextWrapping.Wrap,Foreground=Ui.Color("#61758D"),Margin=new Thickness(0,10,0,16)});
    var value=new TextBlock {Foreground=Ui.Color("#192D45"),FontSize=14,Margin=new Thickness(0,0,0,8)};panel.Children.Add(value);
    var slider=new Slider {Minimum=32,Maximum=64,TickFrequency=4,SmallChange=4,LargeChange=8,IsSnapToTickEnabled=true,TickPlacement=TickPlacement.BottomRight,Value=original,Margin=new Thickness(0,0,0,14)};Ui.Name(slider,"图标大小滑块");panel.Children.Add(slider);
    Action update=()=>{int size=(int)Math.Round(slider.Value);value.Text="图标 "+size+"  ·  分类预览 "+(size*.75).ToString("0")+"（按系统缩放显示）";owner.PreviewIconSize(size);};slider.ValueChanged+=(s,e)=>update();update();
    var actions=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(actions);actions.Children.Add(Ui.Button("恢复默认",()=>slider.Value=48));actions.Children.Add(Ui.Button("取消",Close));var save=Ui.Button("保存",()=>{owner.Persist();saved=true;Close();});save.IsDefault=true;actions.Children.Add(save);
    Closed+=(s,e)=>{if(!saved)owner.PreviewIconSize(original);};
   }
  }
  public sealed class ManagerWindow:Window {
  readonly MainWindow main;
  readonly ListBox groups=new ListBox();
  readonly ListBox items=new ListBox();
  public ManagerWindow(MainWindow owner){
   main=owner;Owner=owner;Title="管理分类";Width=820;Height=560;MinWidth=700;MinHeight=460;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Microsoft YaHei UI");Background=Ui.Color("#F2F5FA");
   var root=new DockPanel {Margin=new Thickness(20)};Content=root;
   var title=new TextBlock {Text="整理你的程序分类",FontSize=22,FontWeight=FontWeights.SemiBold,Foreground=Ui.Color("#192D45"),Margin=new Thickness(0,0,0,6)};DockPanel.SetDock(title,Dock.Top);root.Children.Add(title);
   var help=new TextBlock {Text="选择分类后添加或调整快捷方式。所有更改自动保存，原桌面文件保留。",Foreground=Ui.Color("#61758D"),Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(help,Dock.Top);root.Children.Add(help);
   var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(Ui.Button("完成",Close));
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(245)});grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});root.Children.Add(grid);
   var left=new DockPanel {Margin=new Thickness(0,0,16,0)};grid.Children.Add(left);var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);left.Children.Add(actions);
   actions.Children.Add(Ui.Button("新建",()=>{var n=Ui.Ask(this,"新建分类","");if(n==null)return;var g=new Category {Id=Guid.NewGuid().ToString("N"),Name=n,Items=new List<Shortcut>()};main.Config.Groups.Insert(Math.Max(0,main.Config.Groups.Count-1),g);Save();Select(g);}));
   actions.Children.Add(Ui.Button("重命名",()=>{var g=Selected();if(g==null)return;var n=Ui.Ask(this,"重命名分类",g.Name);if(n!=null){g.Name=n;Save();Select(g);}}));
   actions.Children.Add(Ui.Button("上移",()=>Reorder(-1)));actions.Children.Add(Ui.Button("下移",()=>Reorder(1)));
   actions.Children.Add(Ui.Button("移除分类",()=>{var g=Selected();if(g==null||g.IsCollection)return;if(MessageBox.Show(this,"移除此分类？其中的快捷方式会移到「未分类」，不会删除任何文件。","移除分类",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;var dest=main.Config.Groups.FirstOrDefault(x=>x.Id=="unassigned");if(dest==null){dest=new Category {Id="unassigned",Name="未分类",Items=new List<Shortcut>()};main.Config.Groups.Insert(0,dest);}if(dest==g)return;foreach(var i in g.Items)if(!dest.Items.Any(x=>x.Target.Equals(i.Target,StringComparison.OrdinalIgnoreCase)))dest.Items.Add(i);main.Config.Groups.Remove(g);Save();}));
   groups.SelectionChanged+=(s,e)=>RefreshItems();Ui.Name(groups,"分类列表");left.Children.Add(groups);
   var right=new DockPanel();Grid.SetColumn(right,1);grid.Children.Add(right);var itemActions=new WrapPanel();DockPanel.SetDock(itemActions,Dock.Bottom);right.Children.Add(itemActions);
   itemActions.Children.Add(Ui.Button("添加快捷方式",()=>{var g=Selected();if(g==null||g.IsCollection){MessageBox.Show(this,"请先选择一个普通分类。");return;}main.PickFiles(g);RefreshItems();}));
   itemActions.Children.Add(Ui.Button("上移项目",()=>ReorderItem(-1)));itemActions.Children.Add(Ui.Button("下移项目",()=>ReorderItem(1)));
   itemActions.Children.Add(Ui.Button("移动到…",()=>{var i=items.SelectedItem as Shortcut;if(i==null)return;var m=Ui.Menu();foreach(var g in main.Config.Groups.Where(x=>!x.IsCollection)){var dest=g;Ui.MenuItem(m,g.Name,()=>{main.MoveItem(i,dest);Save();});}m.PlacementTarget=items;m.IsOpen=true;}));
   itemActions.Children.Add(Ui.Button("移出此分类",()=>{var g=Selected();var i=items.SelectedItem as Shortcut;if(g==null||g.IsCollection||i==null)return;var dest=main.Config.Groups.FirstOrDefault(x=>x.Id=="unassigned");if(dest==null){dest=new Category {Id="unassigned",Name="未分类",Items=new List<Shortcut>()};main.Config.Groups.Insert(0,dest);}if(dest==g)return;main.MoveItem(i,dest);Save();}));
   items.DisplayMemberPath="Name";items.AllowDrop=true;items.Drop+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)){main.AddPaths(Selected(),(string[])e.Data.GetData(DataFormats.FileDrop));RefreshItems();}};Ui.Name(items,"快捷方式列表");right.Children.Add(items);
   WireReorder(groups,"DesktopFolders.Group",()=>Selected(),(source,target)=>{var g=(Category)source;var dest=(Category)target;int index=main.Config.Groups.IndexOf(dest);main.Config.Groups.Remove(g);main.Config.Groups.Insert(Math.Min(index,main.Config.Groups.Count),g);Save();Select(g);});
   WireReorder(items,"DesktopFolders.Item",()=>items.SelectedItem,(source,target)=>{var g=Selected();if(g==null||g.IsCollection)return;var item=(Shortcut)source;int index=g.Items.IndexOf((Shortcut)target);g.Items.Remove(item);g.Items.Insert(Math.Min(index,g.Items.Count),item);Save();items.SelectedItem=item;});
   RefreshGroups();
  }
  void WireReorder(ListBox list,string format,Func<object> selection,Action<object,object> reorder){
   Point start=new Point();object dragSource=null;list.AllowDrop=true;list.PreviewMouseLeftButtonDown+=(s,e)=>{start=e.GetPosition(list);var c=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;dragSource=c!=null?list.ItemContainerGenerator.ItemFromContainer(c):null;};
   list.PreviewMouseMove+=(s,e)=>{if(e.LeftButton!=MouseButtonState.Pressed||dragSource==null)return;var now=e.GetPosition(list);if(Math.Abs(now.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(now.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;var item=dragSource;dragSource=null;e.Handled=true;DragDrop.DoDragDrop(list,new DataObject(format,item),DragDropEffects.Move);};
   list.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent(format)?DragDropEffects.Move:e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
   list.Drop+=(s,e)=>{if(!e.Data.GetDataPresent(format))return;var container=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;var source=e.Data.GetData(format);var target=container!=null?list.ItemContainerGenerator.ItemFromContainer(container):null;if(source!=null&&target!=null&&source!=target)reorder(source,target);e.Handled=true;};
  }
  Category Selected(){return groups.SelectedItem as Category;}
  void Select(Category g){groups.SelectedItem=g;}
  void RefreshGroups(){var g=Selected();groups.ItemsSource=null;groups.ItemsSource=main.Config.Groups;if(g!=null&&main.Config.Groups.Contains(g))Select(g);else groups.SelectedIndex=0;}
  void RefreshItems(){items.ItemsSource=null;var g=Selected();if(g!=null)items.ItemsSource=Files.Items(main.Config,g);}
  void Save(){main.Persist();RefreshGroups();RefreshItems();main.Render();}
  void Reorder(int step){var g=Selected();if(g==null)return;int old=main.Config.Groups.IndexOf(g),next=old+step;if(next<0||next>=main.Config.Groups.Count)return;main.Config.Groups.RemoveAt(old);main.Config.Groups.Insert(next,g);Save();Select(g);}
  void ReorderItem(int step){var g=Selected();var i=items.SelectedItem as Shortcut;if(g==null||g.IsCollection||i==null)return;int old=g.Items.IndexOf(i),next=old+step;if(next<0||next>=g.Items.Count)return;g.Items.RemoveAt(old);g.Items.Insert(next,i);Save();items.SelectedItem=i;}
 }
 public static class Program {
  static Mutex singleton;
  [STAThread]public static int Main(string[] args){
   try{
    UiTrace.Enabled=args.Contains("--trace-ui");UiTrace.Mark("Start");
    if(args.Contains("--self-test"))return Tests.Run();
    if(args.Contains("--verify")){var c=Files.Load();var all=Files.All(c);foreach(var i in all)Icons.Get(i);var missing=all.Where(i=>!File.Exists(Files.Resolve(i.Target))&&!Directory.Exists(Files.Resolve(i.Target))).Select(i=>i.Name).ToArray();File.WriteAllText(Path.Combine(Files.Root,"verification.json"),Files.Json.Serialize(new {groups=c.Groups.Count,shortcuts=all.Count,iconSize=c.IconSize,missing=missing,iconFallbacks=Icons.Failures,taskbarAutoHide=Native.TaskbarAutoHide(),pid=Process.GetCurrentProcess().Id}),Encoding.UTF8);return missing.Length==0?0:1;}
    bool created;singleton=new Mutex(true,"Local\\DesktopFolders-Standalone-v1",out created);if(!created){var old=Native.FindWindow(null,"桌面分类");if(old!=IntPtr.Zero)Native.PostMessage(old,Native.RegisterWindowMessage(args.Contains("--manage")?"DesktopFolders.Manage.v1":"DesktopFolders.Show.v1"),IntPtr.Zero,IntPtr.Zero);return 0;}
    var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.DispatcherUnhandledException+=(s,e)=>{Files.Log("Unhandled UI error: "+e.Exception);MessageBox.Show("桌面分类遇到错误，配置和日志已保留。\n"+e.Exception.Message,"桌面分类");e.Handled=true;};
    bool firstRun=!File.Exists(Files.ConfigPath);var window=new MainWindow(Files.Load(),args.Contains("--preview"),firstRun);app.MainWindow=window;window.Show();app.Run();return 0;
   }catch(Exception ex){Files.Log("Fatal: "+ex);MessageBox.Show(ex.Message,"桌面分类启动失败");return 1;}finally{if(singleton!=null)singleton.Dispose();}
  }
 }
 public static class Tests {
  public static int Run(){var folder=Path.Combine(Files.Root,"TestResults",DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(folder);var results=new List<string>();
   try{
    var c=FirstRun.Empty();var sample=Path.Combine(folder,"sample.txt");File.WriteAllText(sample,"DesktopFolders test fixture");c.Groups[0].Items.Add(new Shortcut {Id="fixture",Name="示例文件",Target=sample,OriginalTarget=sample});var initial=Files.All(c).Count;Check(initial==1,"fixture contains one unique shortcut",results);Check(c.Groups.Count==10,"10 default categories",results);Check(c.Groups.Any(g=>g.IsCollection),"dynamic collection present",results);Check(c.ClickToReveal,"new configurations use click-to-reveal",results);
    Check(c.IconSize==48,"older config receives larger default icon size",results);
    c.Groups[1].Items.Add(new Shortcut {Id="fixture-copy",Name="同一文件的另一个分类入口",Target=sample,OriginalTarget=sample});Check(Files.All(c).Count==1,"collection deduplicates targets across categories",results);Check(Files.Items(c,c.Groups.First(g=>g.IsCollection)).Count==1,"dynamic collection exposes the deduplicated result",results);
    Check(Files.All(c).All(i=>File.Exists(Files.Resolve(i.Target))),"all shortcut copies exist",results);
    var path=Path.Combine(folder,"roundtrip.json");Files.SaveTo(path,c);var again=Files.Read(path);Check(Files.All(again).Count==initial,"save and reload preserves shortcuts",results);
    c.IconSize=64;Files.SaveTo(path,c);Check(Files.Read(path).IconSize==64,"icon size survives save and reload",results);c.IconSize=31;bool invalidSize=false;try{Files.SaveTo(path,c);}catch(InvalidDataException){invalidSize=true;}Check(invalidSize&&Files.Read(path).IconSize==64,"out-of-range size cannot overwrite valid configuration",results);c.IconSize=48;
    c.Groups[0].Name="保存测试";Files.SaveTo(path,c);Check(File.Exists(path+".bak"),"atomic write retains prior version",results);Check(Files.Read(path+".bak").Groups[0].Name!="保存测试","backup contains prior data",results);
    var before=Files.All(c).Select(x=>x.Target).OrderBy(x=>x).ToArray();var item=c.Groups[0].Items[0];c.Groups[0].Items.RemoveAt(0);c.Groups[1].Items.Add(item);Check(before.SequenceEqual(Files.All(c).Select(x=>x.Target).OrderBy(x=>x)),"moving categories preserves target identity",results);
    var invalid=new Configuration {Version=1,Groups=new List<Category>()};bool rejected=false;try{Files.SaveTo(path,invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid config rejected before write",results);Check(Files.Read(path).Groups.Count==10,"failed write preserves valid config",results);
    foreach(var i in Files.All(c))Check(Icons.Get(i)!=null,"icon: "+i.Name,results);
    File.WriteAllText(Path.Combine(folder,"result.json"),Files.Json.Serialize(new {passed=true,checks=results.Count,results=results}),Encoding.UTF8);return 0;
   }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"result.json"),Files.Json.Serialize(new {passed=false,error=ex.ToString(),results=results}),Encoding.UTF8);return 1;}
  }
  static void Check(bool test,string name,List<string> results){if(!test)throw new Exception("Failed: "+name);results.Add(name);}
 }
}
