using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DevSpaceStatusPet.UI;

internal static class LayeredWindowRenderer
{
    internal const int WsExLayered = 0x00080000;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExTopMost = 0x00000008;

    private const int GwlExStyle = -20;
    private const int UlwAlpha = 0x00000002;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;

    public static void Apply(Form form, Bitmap bitmap, byte opacity)
    {
        if (!OperatingSystem.IsWindows() ||
            !form.IsHandleCreated ||
            form.IsDisposed ||
            bitmap.Width <= 0 ||
            bitmap.Height <= 0)
        {
            return;
        }

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not acquire the screen device context.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmapHandle = IntPtr.Zero;
        var previousObject = IntPtr.Zero;

        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the layered-window device context.");
            }

            bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
            previousObject = SelectObject(memoryDc, bitmapHandle);
            if (previousObject == IntPtr.Zero || previousObject == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not select the layered-window bitmap.");
            }

            var destination = new NativePoint(form.Left, form.Top);
            var source = new NativePoint(0, 0);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = opacity,
                AlphaFormat = AcSrcAlpha
            };

            if (!UpdateLayeredWindow(
                    form.Handle,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    UlwAlpha))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update the layered pet window.");
            }
        }
        finally
        {
            if (previousObject != IntPtr.Zero && previousObject != new IntPtr(-1) && memoryDc != IntPtr.Zero)
            {
                _ = SelectObject(memoryDc, previousObject);
            }
            if (bitmapHandle != IntPtr.Zero)
            {
                _ = DeleteObject(bitmapHandle);
            }
            if (memoryDc != IntPtr.Zero)
            {
                _ = DeleteDC(memoryDc);
            }
            _ = ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public static bool IsCloaked(IntPtr windowHandle)
    {
        if (!OperatingSystem.IsWindows() || windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var cloaked = 0;
        var result = DwmGetWindowAttribute(windowHandle, 14, out cloaked, sizeof(int));
        return result == 0 && cloaked != 0;
    }

    public static bool IsTopMost(IntPtr windowHandle)
    {
        if (!OperatingSystem.IsWindows() || windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var extendedStyle = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        return (extendedStyle & WsExTopMost) != 0;
    }

    public static Bitmap CreateLayerBitmap(Size size)
    {
        return new Bitmap(
            Math.Max(1, size.Width),
            Math.Max(1, size.Height),
            PixelFormat.Format32bppPArgb);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        out int attributeValue,
        int attributeSize);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr windowHandle,
        IntPtr destinationDeviceContext,
        ref NativePoint destinationPosition,
        ref NativeSize size,
        IntPtr sourceDeviceContext,
        ref NativePoint sourcePosition,
        int colorKey,
        ref BlendFunction blend,
        int flags);
}
