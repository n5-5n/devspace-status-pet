using System.Runtime.InteropServices;
using DevSpaceStatusPet.Models;

namespace DevSpaceStatusPet.UI;

public static class IconFactory
{
    public static Icon Create(ActivityState state)
    {
        var color = state switch
        {
            ActivityState.Working => Color.LimeGreen,
            ActivityState.Waiting => Color.Gold,
            ActivityState.Failed => Color.OrangeRed,
            ActivityState.Stalled => Color.MediumPurple,
            ActivityState.Stopped => Color.Crimson,
            _ => Color.DodgerBlue
        };

        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        using var border = new Pen(Color.FromArgb(100, Color.Black), 1f);
        graphics.FillEllipse(brush, 1, 1, 13, 13);
        graphics.DrawEllipse(border, 1, 1, 13, 13);

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
