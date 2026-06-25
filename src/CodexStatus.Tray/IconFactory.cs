using System.Drawing;
using System.Runtime.InteropServices;
using CodexStatus.Core;

namespace CodexStatus.Tray;

internal static class IconFactory
{
    public static Icon Create(AgentDisplayState state)
    {
        var color = state switch
        {
            AgentDisplayState.WaitingForApproval => Color.FromArgb(245, 158, 11),
            AgentDisplayState.Done => Color.FromArgb(34, 197, 94),
            AgentDisplayState.Failed => Color.FromArgb(239, 68, 68),
            AgentDisplayState.Stale => Color.FromArgb(148, 163, 184),
            AgentDisplayState.Idle or AgentDisplayState.Unknown => Color.FromArgb(156, 163, 175),
            _ => Color.FromArgb(59, 130, 246)
        };

        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(color);
            using var border = new Pen(Color.FromArgb(245, 245, 245), 1);
            graphics.FillEllipse(fill, 2, 2, 12, 12);
            graphics.DrawEllipse(border, 2, 2, 12, 12);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
