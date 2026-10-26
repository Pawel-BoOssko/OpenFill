<#
  OpenFill - pins the OpenFill shortcut to the Windows taskbar (Windows 10 and 11), no clicking.
  Metadata:
    wersja: 1.0
    data:   2026-10-12 21:26
  Usage: pin-taskbar.ps1 -Lnk <path of the Start menu shortcut> -Exe <path of OpenFill.exe> [-Log <file>]
  Run it as the signed-in user (the installer does that). Does nothing when OpenFill is already pinned.
  Method: the shell's own pinned-list interface (the one the Firefox and Chrome installers use).
#>
param([Parameter(Mandatory = $true)][string]$Lnk, [Parameter(Mandatory = $true)][string]$Exe, [string]$Log = '')
$ErrorActionPreference = 'Stop'
function Say($m) { if ($Log) { Add-Content -Path $Log -Value $m } else { Write-Host $m } }
$pinDir = Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'
function Test-Pinned {
    if (-not (Test-Path $pinDir)) { return $false }
    $sh = New-Object -ComObject WScript.Shell
    foreach ($f in Get-ChildItem $pinDir -Filter *.lnk -ErrorAction SilentlyContinue) {
        try { if ($sh.CreateShortcut($f.FullName).TargetPath -ieq $Exe) { return $true } } catch { }
    }
    return $false
}
try {
    if (Test-Pinned) { Say 'Already pinned to the taskbar.'; return }
    if (-not (Test-Path $Lnk)) { throw "Shortcut not found: $Lnk" }
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace OfPin {
  [ComImport, Guid("0dd79ae2-d156-45d4-9eeb-3b549769e940"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  public interface IPinnedList3 {
    void M3(); void M4(); void M5(); void M6(); void M7(); void M8(); void M9(); void M10(); void M11(); void M12(); void M13(); void M14(); void M15();
    [PreserveSig] int Modify(IntPtr unpin, IntPtr pin, int caller);
  }
  public static class Native {
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHParseDisplayName(string name, IntPtr bc, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);
    [DllImport("ole32.dll")] public static extern void CoTaskMemFree(IntPtr p);
    public static int Pin(string lnk) {
      IntPtr pidl; uint a;
      int hr = SHParseDisplayName(lnk, IntPtr.Zero, out pidl, 0, out a);
      if (hr != 0) return hr;
      try {
        var t = Type.GetTypeFromCLSID(new Guid("90aa3a4e-1cba-4233-b8bb-535773d48449"));
        var pl = (IPinnedList3)Activator.CreateInstance(t);
        try { return pl.Modify(IntPtr.Zero, pidl, Int32.MaxValue); } finally { Marshal.ReleaseComObject(pl); }
      } finally { CoTaskMemFree(pidl); }
    }
  }
}
"@
    $hr = [OfPin.Native]::Pin($Lnk)
    if ($hr -ne 0) { throw ('The shell refused the pin, code 0x' + $hr.ToString('x')) }
    for ($i = 0; $i -lt 20 -and -not (Test-Pinned); $i++) { Start-Sleep -Milliseconds 500 }
    if (Test-Pinned) { Say 'Pinned to the taskbar.' } else { Say 'The shell accepted the pin, but the icon did not appear yet.' }
} catch { Say ('Could not pin to the taskbar: ' + $_.Exception.Message) }
