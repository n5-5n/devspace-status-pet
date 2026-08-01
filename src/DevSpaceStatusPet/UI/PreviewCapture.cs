using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;

namespace DevSpaceStatusPet.UI;

internal static class PreviewCapture
{
    private static readonly Color PreviewBackground = Color.FromArgb(18, 20, 26);
    private static readonly Color PreviewFrame = Color.FromArgb(31, 35, 44);
    private static readonly Color PreviewBorder = Color.FromArgb(73, 84, 104);

    public static void Capture(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var snapshot = CreateSampleSnapshot();

        CapturePetPreview(outputDirectory, snapshot, PetTheme.Classic, BubbleVisualStyle.Speech, "preview-classic.png");
        CapturePetPreview(outputDirectory, snapshot, PetTheme.Neon, BubbleVisualStyle.Speech, "preview-neon.png");
        CapturePetPreview(outputDirectory, snapshot, PetTheme.Neon, BubbleVisualStyle.MonitorCardNeon, "preview-monitor-card-neon.png");
        CapturePetPreview(outputDirectory, snapshot, PetTheme.Classic, BubbleVisualStyle.MonitorCardClean, "preview-monitor-card-clean.png");
        CaptureMenuPreview(outputDirectory, snapshot);
        CaptureSettingsPreview(outputDirectory, snapshot);
        CaptureUpdaterPreview(outputDirectory);
    }

    private static void CapturePetPreview(
        string outputDirectory,
        DevSpaceSnapshot snapshot,
        PetTheme theme,
        BubbleVisualStyle bubbleStyle,
        string fileName)
    {
        var settings = CreatePreviewSettings(theme, bubbleStyle);
        var store = new SettingsStore(settings);
        var localizer = new Localizer(() => store.Current);
        using var pet = new PetForm(store, new PositionStore(null), localizer);
        pet.ApplySnapshot(snapshot);
        using var petBitmap = pet.RenderPreview(PreviewBackground);
        using var output = CreateCanvas(new Size(520, 660));
        using var graphics = Graphics.FromImage(output);
        ConfigureGraphics(graphics);
        var x = (output.Width - petBitmap.Width) / 2;
        var y = (output.Height - petBitmap.Height) / 2;
        graphics.DrawImageUnscaled(petBitmap, x, y);
        output.Save(Path.Combine(outputDirectory, fileName), ImageFormat.Png);
    }

    private static void CaptureMenuPreview(string outputDirectory, DevSpaceSnapshot snapshot)
    {
        var settings = CreatePreviewSettings(PetTheme.Classic, BubbleVisualStyle.MonitorCardClean);
        var store = new SettingsStore(settings);
        var localizer = new Localizer(() => store.Current);
        using var pet = new PetForm(store, new PositionStore(null), localizer);
        pet.ApplySnapshot(snapshot);
        using var petBitmap = pet.RenderPreview(PreviewBackground);

        var menu = pet.ContextMenuStrip
            ?? throw new InvalidOperationException("The pet context menu is unavailable.");
        menu.Show(new Point(-10000, -10000));
        PumpUi();
        menu.PerformLayout();
        var menuSize = menu.GetPreferredSize(Size.Empty);
        if (menuSize.Width > 0 && menuSize.Height > 0)
        {
            menu.Size = menuSize;
        }
        using var menuBitmap = new Bitmap(
            Math.Max(1, menu.Width),
            Math.Max(1, menu.Height),
            PixelFormat.Format32bppArgb);
        menu.DrawToBitmap(menuBitmap, new Rectangle(Point.Empty, menuBitmap.Size));
        menu.Close();

        using var output = CreateCanvas(new Size(930, 660));
        using var graphics = Graphics.FromImage(output);
        ConfigureGraphics(graphics);
        graphics.DrawImageUnscaled(petBitmap, 34, (output.Height - petBitmap.Height) / 2);
        DrawCardShadow(graphics, new Rectangle(565, 92, menuBitmap.Width, menuBitmap.Height));
        graphics.DrawImageUnscaled(menuBitmap, 565, 92);
        output.Save(Path.Combine(outputDirectory, "preview-menu.png"), ImageFormat.Png);
    }

    private static void CaptureSettingsPreview(string outputDirectory, DevSpaceSnapshot snapshot)
    {
        var settings = CreatePreviewSettings(PetTheme.Classic, BubbleVisualStyle.MonitorCardClean);
        var store = new SettingsStore(settings);
        var localizer = new Localizer(() => store.Current);
        using var form = new SettingsForm(store, localizer, snapshot)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
            ShowInTaskbar = false
        };
        form.SetUpdateStatus("0.2.1", localizer["UpToDate"]);
        using var client = RenderControl(form);
        using var output = RenderWindowFrame(
            client,
            "DevSpace Status Pet - 設定",
            new Size(client.Width + 48, client.Height + 80));
        output.Save(Path.Combine(outputDirectory, "preview-settings.png"), ImageFormat.Png);
    }

    private static void CaptureUpdaterPreview(string outputDirectory)
    {
        var settings = CreatePreviewSettings(PetTheme.Classic);
        var store = new SettingsStore(settings);
        var localizer = new Localizer(() => store.Current);
        using var updateService = new UpdateService("0.2.0");
        var release = new UpdateRelease(
            "0.2.1",
            "v0.2.1",
            "DevSpace Status Pet v0.2.1",
            "https://github.com/n5-5n/devspace-status-pet/releases/tag/v0.2.1",
            "## v0.2.1\r\n\r\n- GitHubから最新版を確認\r\n- ZIPとSHA-256を検証して安全に更新\r\n- Stable／Prereleaseを選択可能\r\n- GitHubプレビューを刷新",
            false,
            DateTimeOffset.Now,
            "https://example.invalid/update.zip",
            "https://example.invalid/update.zip.sha256",
            70 * 1024 * 1024);
        using var form = new UpdateForm(updateService, release, localizer)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
            ShowInTaskbar = false
        };
        using var client = RenderControl(form);
        using var output = RenderWindowFrame(
            client,
            localizer["UpdateAvailableTitle"],
            new Size(client.Width + 48, client.Height + 80));
        output.Save(Path.Combine(outputDirectory, "preview-updater.png"), ImageFormat.Png);
    }

    private static Bitmap RenderControl(Form form)
    {
        form.Show();
        PumpUi();
        form.PerformLayout();
        var bitmap = new Bitmap(
            Math.Max(1, form.ClientSize.Width),
            Math.Max(1, form.ClientSize.Height),
            PixelFormat.Format32bppArgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        form.Close();
        return bitmap;
    }

    private static Bitmap RenderWindowFrame(Bitmap client, string title, Size outputSize)
    {
        var output = CreateCanvas(outputSize);
        using var graphics = Graphics.FromImage(output);
        ConfigureGraphics(graphics);
        var frame = new Rectangle(20, 18, client.Width + 8, client.Height + 46);
        DrawCardShadow(graphics, frame);
        using var frameBrush = new SolidBrush(PreviewFrame);
        using var borderPen = new Pen(PreviewBorder, 1f);
        graphics.FillRectangle(frameBrush, frame);
        graphics.DrawRectangle(borderPen, frame);

        using var titleFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(DarkUiTheme.Foreground);
        using var mutedBrush = new SolidBrush(DarkUiTheme.MutedForeground);
        graphics.FillEllipse(Brushes.IndianRed, frame.X + 13, frame.Y + 15, 8, 8);
        graphics.FillEllipse(Brushes.Goldenrod, frame.X + 28, frame.Y + 15, 8, 8);
        graphics.FillEllipse(Brushes.MediumSeaGreen, frame.X + 43, frame.Y + 15, 8, 8);
        graphics.DrawString(title, titleFont, titleBrush, frame.X + 62, frame.Y + 10);
        graphics.DrawString("Windows 10 / 11", titleFont, mutedBrush, frame.Right - 116, frame.Y + 10);
        graphics.DrawImageUnscaled(client, frame.X + 4, frame.Y + 38);
        return output;
    }

    private static Bitmap CreateCanvas(Size size)
    {
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(PreviewBackground);
        return bitmap;
    }

    private static void DrawCardShadow(Graphics graphics, Rectangle rectangle)
    {
        using var shadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
        graphics.FillRectangle(shadow, rectangle.X + 8, rectangle.Y + 10, rectangle.Width, rectangle.Height);
    }

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    private static AppSettings CreatePreviewSettings(
        PetTheme theme,
        BubbleVisualStyle bubbleStyle = BubbleVisualStyle.Speech) => new()
    {
        Theme = theme.ToString(),
        BubbleTheme = BubbleColorTheme.Dark.ToString(),
        BubbleStyle = bubbleStyle.ToString(),
        Language = UiLanguagePreference.Japanese.ToString(),
        ShowBubble = true,
        Scale = 1.25,
        Opacity = 1.0,
        CompletionQuietSeconds = 45,
        StallMinutes = 30,
        MaxBubbles = 4,
        NotificationsEnabled = true,
        CheckUpdatesOnStartup = true,
        IncludePrereleaseUpdates = false
    };

    private static DevSpaceSnapshot CreateSampleSnapshot()
    {
        var now = DateTimeOffset.Now;
        var activities = new[]
        {
            new DevSpaceActivity(
                "preview:videoshrink",
                "VideoShrink",
                ActivityState.Working,
                OperationKind.Dotnet,
                "dotnet test",
                now.AddMinutes(-4).AddSeconds(-32),
                TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(32)),
                false,
                true,
                "ws_preview_video"),
            new DevSpaceActivity(
                "preview:status-pet",
                "devspace-status",
                ActivityState.Waiting,
                OperationKind.Edit,
                "UpdateService.cs",
                now.AddSeconds(-18),
                TimeSpan.FromSeconds(18),
                false,
                true,
                "ws_preview_pet"),
            new DevSpaceActivity(
                "preview:hub",
                "personal-hub",
                ActivityState.Working,
                OperationKind.Git,
                "git push",
                now.AddMinutes(-1).AddSeconds(-9),
                TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(9)),
                false,
                true,
                "ws_preview_hub")
        };
        return new DevSpaceSnapshot(
            ActivityState.Working,
            activities,
            12345,
            7676,
            @"C:\Users\user\.devspace\config.json",
            @"C:\Users\user\.devspace\serve.log",
            now,
            now.AddSeconds(-2),
            true);
    }

    private static void PumpUi()
    {
        Application.DoEvents();
        Thread.Sleep(120);
        Application.DoEvents();
    }
}
