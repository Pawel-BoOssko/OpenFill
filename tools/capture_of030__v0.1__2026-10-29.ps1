$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$code=@'
using System;
using System.Runtime.InteropServices;
public class UICapture {
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
}
'@
Add-Type $code
$proc=Get-Process OpenFill -ErrorAction Stop
$r=New-Object UICapture+Rect
if(-not [UICapture]::GetWindowRect($proc.MainWindowHandle,[ref]$r)){throw 'WINDOW_RECT_FAILED'}
$width=$r.Right-$r.Left;$height=$r.Bottom-$r.Top
if($width -lt 100 -or $height -lt 100){throw ('INVALID_WINDOW_SIZE_'+$width+'x'+$height)}
$bmp=New-Object System.Drawing.Bitmap($width,$height)
$g=[System.Drawing.Graphics]::FromImage($bmp)
$dc=$g.GetHdc()
try{$ok=[UICapture]::PrintWindow($proc.MainWindowHandle,$dc,2)}finally{$g.ReleaseHdc($dc)}
$private='C:\Users\pboru\OpenFill\data\stable\logs\private-ui-captures'
[IO.Directory]::CreateDirectory($private) | Out-Null
$f=Join-Path $private 'OF030_GUI_capture_20261029.png'
$bmp.Save($f,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
'WIN_RECT='+$r.Left+','+$r.Top+' '+$width+'x'+$height
'PRINTWINDOW='+$ok
'FILE='+$f
'BYTES='+((Get-Item $f).Length)