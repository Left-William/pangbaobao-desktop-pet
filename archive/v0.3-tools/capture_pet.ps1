param([int]$ProcessId, [string]$OutputPath, [long]$Handle = 0)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowRectReader {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out Rect rect, int size);
}
'@
$proc = Get-Process -Id $ProcessId -ErrorAction Stop
$windowHandle = if ($Handle -ne 0) { [IntPtr]::new($Handle) } else { $proc.MainWindowHandle }
$rect = New-Object WindowRectReader+Rect
$dwmResult = [WindowRectReader]::DwmGetWindowAttribute($windowHandle, 9, [ref]$rect, [Runtime.InteropServices.Marshal]::SizeOf($rect))
if ($dwmResult -ne 0 -and -not [WindowRectReader]::GetWindowRect($windowHandle, [ref]$rect)) { throw 'Cannot read pet window rect.' }
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -lt 10 -or $height -lt 10) { throw 'Invalid pet window size.' }
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
  $hdc = $graphics.GetHdc()
  try {
    if (-not [WindowRectReader]::PrintWindow($windowHandle, $hdc, 2)) { throw 'PrintWindow failed.' }
  } finally { $graphics.ReleaseHdc($hdc) }
  $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output "Captured ${width}x${height} at $($rect.Left),$($rect.Top)"
} finally {
  $graphics.Dispose()
  $bitmap.Dispose()
}
