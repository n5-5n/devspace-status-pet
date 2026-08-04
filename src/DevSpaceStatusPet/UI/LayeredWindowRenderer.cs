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
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;

    internal sealed class Surface : IDisposable
    {
        private readonly Size _size;
        private IntPtr _memoryDc;
        private IntPtr _bitmapHandle;
        private IntPtr _previousObject;
        private Bitmap? _bitmap;
        private Graphics? _graphics;
        private bool _disposed;

        public Surface(Size size)
        {
            _size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));

            var screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not acquire the screen device context.");
            }

            try
            {
                _memoryDc = CreateCompatibleDC(screenDc);
                if (_memoryDc == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the layered-window device context.");
                }

                var bitmapInfo = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = _size.Width,
                        Height = -_size.Height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = BiRgb,
                        SizeImage = checked((uint)(_size.Width * _size.Height * 4))
                    }
                };

                _bitmapHandle = CreateDIBSection(
                    screenDc,
                    ref bitmapInfo,
                    DibRgbColors,
                    out var bits,
                    IntPtr.Zero,
                    0);
                if (_bitmapHandle == IntPtr.Zero || bits == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the layered-window DIB surface.");
                }

                _previousObject = SelectObject(_memoryDc, _bitmapHandle);
                if (_previousObject == IntPtr.Zero || _previousObject == new IntPtr(-1))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not select the layered-window DIB surface.");
                }

                _bitmap = new Bitmap(
                    _size.Width,
                    _size.Height,
                    checked(_size.Width * 4),
                    PixelFormat.Format32bppPArgb,
                    bits);
                _graphics = Graphics.FromImage(_bitmap);
            }
            catch
            {
                Dispose();
                throw;
            }
            finally
            {
                _ = ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public Size Size => _size;

        public Graphics BeginDraw()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var graphics = _graphics ?? throw new ObjectDisposedException(nameof(Surface));
            graphics.ResetTransform();
            graphics.ResetClip();
            graphics.Clear(Color.Transparent);
            return graphics;
        }

        internal Bitmap CaptureBitmap()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var bitmap = _bitmap ?? throw new ObjectDisposedException(nameof(Surface));
            return bitmap.Clone(
                new Rectangle(Point.Empty, _size),
                PixelFormat.Format32bppPArgb);
        }

        public void Apply(Form form, byte opacity)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!OperatingSystem.IsWindows() ||
                !form.IsHandleCreated ||
                form.IsDisposed)
            {
                return;
            }

            var screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not acquire the screen device context.");
            }

            try
            {
                var destination = new NativePoint(form.Left, form.Top);
                var source = new NativePoint(0, 0);
                var nativeSize = new NativeSize(_size.Width, _size.Height);
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
                        ref nativeSize,
                        _memoryDc,
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
                _ = ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _graphics?.Dispose();
            _graphics = null;
            _bitmap?.Dispose();
            _bitmap = null;

            if (_previousObject != IntPtr.Zero &&
                _previousObject != new IntPtr(-1) &&
                _memoryDc != IntPtr.Zero)
            {
                _ = SelectObject(_memoryDc, _previousObject);
            }
            _previousObject = IntPtr.Zero;

            if (_bitmapHandle != IntPtr.Zero)
            {
                _ = DeleteObject(_bitmapHandle);
                _bitmapHandle = IntPtr.Zero;
            }
            if (_memoryDc != IntPtr.Zero)
            {
                _ = DeleteDC(_memoryDc);
                _memoryDc = IntPtr.Zero;
            }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
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

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

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
