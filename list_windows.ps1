param([int]$ProcessId, [switch]$OutputObject)
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class PetWindows {
  public delegate bool EnumCallback(IntPtr hwnd, IntPtr lParam);
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback, IntPtr extra);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hwnd, StringBuilder title, int max);
}
'@
$found = [System.Collections.Generic.List[object]]::new()
$callback = [PetWindows+EnumCallback]{ param($handle, $extra)
  $ownerId = [uint32]0
  [void][PetWindows]::GetWindowThreadProcessId($handle, [ref]$ownerId)
  if ($ownerId -eq $ProcessId) {
    $r = New-Object PetWindows+Rect
    [void][PetWindows]::GetWindowRect($handle,[ref]$r)
    $title = [System.Text.StringBuilder]::new(256)
    [void][PetWindows]::GetWindowText($handle,$title,256)
    $found.Add([pscustomobject]@{Handle=$handle;Visible=[PetWindows]::IsWindowVisible($handle);Width=$r.Right-$r.Left;Height=$r.Bottom-$r.Top;Left=$r.Left;Top=$r.Top;Title=$title.ToString()})
  }
  return $true
}
[void][PetWindows]::EnumWindows($callback,[IntPtr]::Zero)
if ($OutputObject) { $found } else { $found | Format-Table -AutoSize }
