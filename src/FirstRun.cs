using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace DesktopFolders {
 public static class FirstRun {
  public static Configuration Empty(){
   string[] ids={"ai-dev","office","design","browsers","cloud-media","games","finance","remote","system","all-desktop-apps"};
   string[] names={"AI 与开发","办公与沟通","设计与创作","浏览器","网盘与影音","游戏娱乐","证券交易","远程与网络","系统与效率","集合"};
   var c=new Configuration {Version=1,Left=40,Top=100,IconSize=48,ClickToReveal=true,Groups=new List<Category>()};
   for(int i=0;i<ids.Length;i++)c.Groups.Add(new Category {Id=ids[i],Name=names[i],IsCollection=i==ids.Length-1,Items=new List<Shortcut>()});
   return c;
  }
  public static Configuration Create(){
   var c=Empty();var system=c.Groups.First(g=>g.Id=="system");
   system.Items.Add(new Shortcut {Id="desktopfolders-manage",Name="桌面分类",Target="Shortcuts\\DesktopFolders-Manage.lnk",OriginalTarget=""});
   system.Items.Add(new Shortcut {Id="this-pc",Name="此电脑",Target="Shortcuts\\ThisPC.lnk",OriginalTarget=""});
   RefreshBuiltIns(c);
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var desktop in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Distinct()){
    if(String.IsNullOrWhiteSpace(desktop)||!Directory.Exists(desktop))continue;
    string[] paths;try{paths=Directory.GetFiles(desktop);}catch(Exception ex){Files.Log("Desktop scan: "+ex.Message);continue;}
    foreach(var path in paths.OrderBy(p=>p,StringComparer.CurrentCultureIgnoreCase)){
     var extension=Path.GetExtension(path);if(!extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".url",StringComparison.OrdinalIgnoreCase))continue;
     var name=Path.GetFileNameWithoutExtension(path);if(name=="桌面分类"||name=="DesktopFolders"||!names.Add(name))continue;
     try{
      string id;using(var sha=SHA256.Create())id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()))).Replace("-","").Substring(0,24).ToLowerInvariant();
      string relative=Path.Combine("Shortcuts",id+extension);File.Copy(path,Path.Combine(Files.Root,relative),false);
      c.Groups.First(g=>g.Id==Classify(name)).Items.Add(new Shortcut {Id=id,Name=name,Target=relative,OriginalTarget=path});
     }catch(Exception ex){Files.Log("Shortcut import: "+ex.Message);}
    }
   }
   return c;
  }
  public static string Classify(string name){
   string value=name.ToLowerInvariant();
   string[][] rules={
    new[]{"ai-dev","codex","claude","visual studio","vscode","pycharm","idea","git","python","node","unity","godot"},
    new[]{"browsers","chrome","edge","firefox","浏览器","brave","opera"},
    new[]{"office","word","excel","powerpoint","wps","office","微信","wechat","qq","钉钉","飞书","notion","obsidian","teams"},
    new[]{"design","blender","photoshop","illustrator","figma","剪映","premiere","after effects","krita","gimp"},
    new[]{"cloud-media","网盘","icloud","onedrive","dropbox","网易云","音乐","music","vlc","potplayer","哔哩","影音"},
    new[]{"games","steam","epic","游戏","wegame","xbox","battle.net"},
    new[]{"finance","证券","同花顺","东方财富","通达信","tradingview"},
    new[]{"remote","远程","向日葵","todesk","rustdesk","anydesk","teamviewer","clash","tailscale","zerotier"}
   };
   foreach(var rule in rules)if(rule.Skip(1).Any(value.Contains))return rule[0];
   return "system";
  }
  public static void RefreshBuiltIns(Configuration config){
   Directory.CreateDirectory(Path.Combine(Files.Root,"Shortcuts"));
   var items=Files.All(config);
   if(items.Any(i=>String.Equals(i.Target,"Shortcuts\\DesktopFolders-Manage.lnk",StringComparison.OrdinalIgnoreCase)))WriteLink("DesktopFolders-Manage.lnk",Path.Combine(Files.Root,"DesktopFolders.exe"),"--manage",Path.Combine(Files.Root,"DesktopFolders.exe")+",0");
   if(items.Any(i=>String.Equals(i.Target,"Shortcuts\\ThisPC.lnk",StringComparison.OrdinalIgnoreCase)))WriteLink("ThisPC.lnk",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"shell:MyComputerFolder",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shell32.dll")+",15");
  }
  static void WriteLink(string name,string target,string args,string icon){
   object shell=null,link=null;
   try{
    shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
    link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Path.Combine(Files.Root,"Shortcuts",name)});
    var type=link.GetType();
    type.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
    type.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{args});
    type.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Files.Root});
    type.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{icon});
    type.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
   }finally{if(link!=null&&Marshal.IsComObject(link))Marshal.FinalReleaseComObject(link);if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
  }
 }
}
