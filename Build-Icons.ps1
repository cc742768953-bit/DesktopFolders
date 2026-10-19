param([string]$AssetDirectory=(Join-Path $PSScriptRoot 'assets'))
$ErrorActionPreference='Stop'
if($PSEdition -eq 'Core'){
 & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -AssetDirectory $AssetDirectory
 if($LASTEXITCODE -ne 0){throw 'Icon generation failed'}
 return
}
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class DesktopFoldersIconBuilder {
 static GraphicsPath Round(float x,float y,float w,float h,float r){var p=new GraphicsPath();float d=r*2;p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;}
 public static void Tray(string png){using(var b=new Bitmap(256,256,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b)){
  g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
  using(var outline=Round(24,49,208,183,33))using(var fill=new SolidBrush(Color.FromArgb(255,20,34,55)))using(var pen=new Pen(Color.FromArgb(255,112,206,255),12)){g.FillPath(fill,outline);g.DrawPath(pen,outline);}
  float[,] boxes={{47,75},{47,147},{137,147},{137,21}};
  for(int i=0;i<4;i++)using(var p=Round(boxes[i,0],boxes[i,1],66,58,12))using(var brush=new LinearGradientBrush(new RectangleF(boxes[i,0],boxes[i,1],66,58),i==3?Color.FromArgb(129,166,255):Color.FromArgb(67,229,255),i>=2?Color.FromArgb(135,87,251):Color.FromArgb(28,142,246),45f)){g.FillPath(brush,p);}
  b.Save(png,ImageFormat.Png);
 }}
 public static void Pack(string png,string ico,int[] sizes){using(var source=new Bitmap(png))using(var output=new BinaryWriter(File.Create(ico))){
  byte[][] frames=new byte[sizes.Length][];
  for(int k=0;k<sizes.Length;k++){int n=sizes[k];using(var b=new Bitmap(n,n,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(source,new Rectangle(0,0,n,n),0,0,source.Width,source.Height,GraphicsUnit.Pixel);}
   using(var mem=new MemoryStream())using(var w=new BinaryWriter(mem)){int stride=((n+31)/32)*4;w.Write(40);w.Write(n);w.Write(n*2);w.Write((short)1);w.Write((short)32);w.Write(0);w.Write(n*n*4+stride*n);w.Write(0);w.Write(0);w.Write(0);w.Write(0);
    for(int y=n-1;y>=0;y--)for(int x=0;x<n;x++){var c=b.GetPixel(x,y);w.Write(c.B);w.Write(c.G);w.Write(c.R);w.Write(c.A);}
    for(int y=n-1;y>=0;y--){byte[] mask=new byte[stride];for(int x=0;x<n;x++)if(b.GetPixel(x,y).A==0)mask[x/8]|=(byte)(128>>(x%8));w.Write(mask);}frames[k]=mem.ToArray();
   }
  }}
  output.Write((short)0);output.Write((short)1);output.Write((short)sizes.Length);int offset=6+sizes.Length*16;
  for(int k=0;k<sizes.Length;k++){int n=sizes[k];output.Write((byte)(n==256?0:n));output.Write((byte)(n==256?0:n));output.Write((byte)0);output.Write((byte)0);output.Write((short)1);output.Write((short)32);output.Write(frames[k].Length);output.Write(offset);offset+=frames[k].Length;}foreach(var frame in frames)output.Write(frame);
 }}
}
'@
[DesktopFoldersIconBuilder]::Tray((Join-Path $AssetDirectory 'desktopfolders-tray.png'))
[DesktopFoldersIconBuilder]::Pack((Join-Path $AssetDirectory 'desktopfolders-icon-20261019.png'),(Join-Path $AssetDirectory 'desktopfolders.ico'),[int[]]@(16,20,24,32,40,48,64,128,256))
[DesktopFoldersIconBuilder]::Pack((Join-Path $AssetDirectory 'desktopfolders-tray.png'),(Join-Path $AssetDirectory 'desktopfolders-tray.ico'),[int[]]@(16,20,24,32,40,48,64))
Get-Item -LiteralPath (Join-Path $AssetDirectory 'desktopfolders.ico'),(Join-Path $AssetDirectory 'desktopfolders-tray.ico') | Select-Object FullName,Length
