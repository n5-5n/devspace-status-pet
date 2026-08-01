using System.Drawing.Drawing2D;
using System.Drawing.Text;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;

namespace DevSpaceStatusPet.UI;

public sealed class PetForm : Form
{
    private const int LogicalWidth = 304;
    private const int BubbleTop = 8;
    private const int BubbleWidth = 286;
    private const int MonitorCardWidth = 296;
    private const int SpeechBubbleHeight = 76;
    private const int SpeechBubbleGap = 8;
    private const int SpeechBubbleTailHeight = 16;
    private const int MonitorCardHeight = 88;
    private const int MonitorCardGap = 9;
    private const int MonitorConnectorHeight = 18;
    private const int RobotLogicalHeight = 224;
    private const float RobotDesignScale = 1.48f;

    private readonly SettingsStore _settingsStore;
    private readonly PositionStore _positionStore;
    private readonly Localizer _localizer;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _bubbleItem;
    private readonly ToolStripMenuItem _classicItem;
    private readonly ToolStripMenuItem _neonItem;
    private readonly ToolStripMenuItem _lightBubbleItem;
    private readonly ToolStripMenuItem _darkBubbleItem;
    private readonly ToolStripMenuItem _speechBubbleStyleItem;
    private readonly ToolStripMenuItem _monitorCardNeonStyleItem;
    private readonly ToolStripMenuItem _monitorCardCleanStyleItem;
    private readonly ToolStripMenuItem _autoLanguageItem;
    private readonly ToolStripMenuItem _japaneseLanguageItem;
    private readonly ToolStripMenuItem _englishLanguageItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _resetItem;
    private readonly ToolStripMenuItem _exitItem;

    private DevSpaceSnapshot _snapshot = DevSpaceSnapshot.Initial(AppPaths.ConfigPath, AppPaths.ServeLogPath, 7676);
    private int _frame;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragOffset;

    public PetForm(SettingsStore settingsStore, PositionStore positionStore, Localizer localizer)
    {
        _settingsStore = settingsStore;
        _positionStore = positionStore;
        _localizer = localizer;

        Text = "DevSpace Status Pet";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        DoubleBuffered = false;
        KeyPreview = true;

        _menu = new ContextMenuStrip();
        _bubbleItem = new ToolStripMenuItem { CheckOnClick = true };
        var themeMenu = new ToolStripMenuItem();
        _classicItem = new ToolStripMenuItem { CheckOnClick = true };
        _neonItem = new ToolStripMenuItem { CheckOnClick = true };
        themeMenu.DropDownItems.AddRange([_classicItem, _neonItem]);

        var bubbleThemeMenu = new ToolStripMenuItem();
        _lightBubbleItem = new ToolStripMenuItem { CheckOnClick = true };
        _darkBubbleItem = new ToolStripMenuItem { CheckOnClick = true };
        bubbleThemeMenu.DropDownItems.AddRange([_lightBubbleItem, _darkBubbleItem]);

        var bubbleStyleMenu = new ToolStripMenuItem();
        _speechBubbleStyleItem = new ToolStripMenuItem { CheckOnClick = true };
        _monitorCardNeonStyleItem = new ToolStripMenuItem { CheckOnClick = true };
        _monitorCardCleanStyleItem = new ToolStripMenuItem { CheckOnClick = true };
        bubbleStyleMenu.DropDownItems.AddRange([
            _speechBubbleStyleItem,
            _monitorCardNeonStyleItem,
            _monitorCardCleanStyleItem
        ]);

        var languageMenu = new ToolStripMenuItem();
        _autoLanguageItem = new ToolStripMenuItem { CheckOnClick = true };
        _japaneseLanguageItem = new ToolStripMenuItem { CheckOnClick = true };
        _englishLanguageItem = new ToolStripMenuItem { CheckOnClick = true };
        languageMenu.DropDownItems.AddRange([_autoLanguageItem, _japaneseLanguageItem, _englishLanguageItem]);

        _settingsItem = new ToolStripMenuItem();
        _resetItem = new ToolStripMenuItem();
        _exitItem = new ToolStripMenuItem();
        _menu.Items.AddRange([
            _bubbleItem,
            themeMenu,
            bubbleThemeMenu,
            bubbleStyleMenu,
            languageMenu,
            _settingsItem,
            _resetItem,
            new ToolStripSeparator(),
            _exitItem
        ]);
        DarkUiTheme.ApplyMenu(_menu);
        ContextMenuStrip = _menu;

        _bubbleItem.Click += (_, _) => UpdateSettings(settings => settings.ShowBubble = _bubbleItem.Checked);
        _classicItem.Click += (_, _) => SetTheme(PetTheme.Classic);
        _neonItem.Click += (_, _) => SetTheme(PetTheme.Neon);
        _lightBubbleItem.Click += (_, _) => SetBubbleTheme(BubbleColorTheme.Light);
        _darkBubbleItem.Click += (_, _) => SetBubbleTheme(BubbleColorTheme.Dark);
        _speechBubbleStyleItem.Click += (_, _) => SetBubbleStyle(BubbleVisualStyle.Speech);
        _monitorCardNeonStyleItem.Click += (_, _) => SetBubbleStyle(BubbleVisualStyle.MonitorCardNeon);
        _monitorCardCleanStyleItem.Click += (_, _) => SetBubbleStyle(BubbleVisualStyle.MonitorCardClean);
        _autoLanguageItem.Click += (_, _) => SetLanguage(UiLanguagePreference.Auto);
        _japaneseLanguageItem.Click += (_, _) => SetLanguage(UiLanguagePreference.Japanese);
        _englishLanguageItem.Click += (_, _) => SetLanguage(UiLanguagePreference.English);
        _settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _resetItem.Click += (_, _) => MoveToBottomRight();
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        MouseDown += OnPetMouseDown;
        MouseMove += OnPetMouseMove;
        MouseUp += OnPetMouseUp;

        _animationTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _animationTimer.Tick += (_, _) =>
        {
            _frame++;
            RenderLayeredWindow();
        };

        _settingsStore.Changed += (_, _) => ApplySettings();
        Shown += (_, _) =>
        {
            ApplySettings();
            RestorePosition();
            RenderLayeredWindow();
            _animationTimer.Start();
        };
        FormClosed += (_, _) =>
        {
            _animationTimer.Stop();
            _positionStore.Save(Location);
        };
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= LayeredWindowRenderer.WsExLayered | LayeredWindowRenderer.WsExToolWindow;
            return parameters;
        }
    }

    public void ApplySnapshot(DevSpaceSnapshot snapshot)
    {
        _snapshot = snapshot;
        ApplySettings();
    }

    private void ApplySettings()
    {
        var settings = _settingsStore.Current;
        ResizeForContent(settings);
        UpdateMenu(settings);
        RenderLayeredWindow();
    }

    private void ResizeForContent(AppSettings settings)
    {
        var count = VisibleActivities(settings).Count;
        var target = CalculateClientSize(settings, count);
        if (ClientSize == target)
        {
            return;
        }

        var bottom = Bottom;
        ClientSize = target;
        Top = bottom - Height;
        ClampToScreen();
    }

    internal static Size CalculateClientSize(AppSettings settings, int activityCount)
    {
        var logicalHeight = GetBubbleAreaHeight(settings, activityCount) + RobotLogicalHeight;
        return new Size(
            (int)Math.Ceiling(LogicalWidth * settings.Scale),
            (int)Math.Ceiling(logicalHeight * settings.Scale));
    }

    internal static int GetBubbleAreaHeight(AppSettings settings, int count)
    {
        if (!settings.ShowBubble)
        {
            return 0;
        }

        var visibleCount = Math.Max(1, count);
        var (height, gap, connectorHeight) = GetBubbleMetrics(settings.ResolvedBubbleStyle);
        return BubbleTop + (visibleCount * height) + ((visibleCount - 1) * gap) + connectorHeight;
    }

    private static (int Height, int Gap, int ConnectorHeight) GetBubbleMetrics(BubbleVisualStyle style) => style switch
    {
        BubbleVisualStyle.MonitorCardNeon or BubbleVisualStyle.MonitorCardClean =>
            (MonitorCardHeight, MonitorCardGap, MonitorConnectorHeight),
        _ => (SpeechBubbleHeight, SpeechBubbleGap, SpeechBubbleTailHeight)
    };

    private IReadOnlyList<DevSpaceActivity> VisibleActivities(AppSettings settings)
    {
        var activities = _snapshot.Activities
            .GroupBy(activity => activity.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (activities.Count == 0)
        {
            activities.Add(new DevSpaceActivity(
                "primary",
                _snapshot.State == ActivityState.Stopped ? "DevSpace" : "DevSpace",
                _snapshot.State,
                _snapshot.State == ActivityState.Stopped ? OperationKind.Stopped : OperationKind.Idle,
                null,
                _snapshot.UpdatedAt,
                TimeSpan.Zero));
        }

        if (activities.Count <= settings.MaxBubbles)
        {
            return activities;
        }

        var visible = activities.Take(Math.Max(1, settings.MaxBubbles - 1)).ToList();
        visible.Add(new DevSpaceActivity(
            "more",
            _localizer.Get("ParallelMore", activities.Count - visible.Count),
            _snapshot.State,
            OperationKind.LocalProcess,
            _localizer["OtherTasks"],
            _snapshot.UpdatedAt,
            TimeSpan.Zero));
        return visible;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        RenderLayeredWindow();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The per-pixel alpha bitmap is the complete window surface.
    }

    internal Bitmap RenderPreview(Color background)
    {
        using var layer = RenderLayerBitmap();
        var bitmap = new Bitmap(
            Math.Max(1, ClientSize.Width),
            Math.Max(1, ClientSize.Height),
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(background);
        graphics.DrawImageUnscaled(layer, 0, 0);
        return bitmap;
    }

    internal Bitmap RenderTransparentPreview() => RenderLayerBitmap();

    private Bitmap RenderLayerBitmap()
    {
        var bitmap = LayeredWindowRenderer.CreateLayerBitmap(ClientSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        RenderContent(graphics);
        return bitmap;
    }

    private void RenderLayeredWindow()
    {
        if (!IsHandleCreated || IsDisposed || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        using var bitmap = RenderLayerBitmap();
        var opacity = (byte)Math.Clamp(
            (int)Math.Round(_settingsStore.Current.Opacity * byte.MaxValue),
            byte.MinValue,
            byte.MaxValue);
        LayeredWindowRenderer.Apply(this, bitmap, opacity);
    }

    private void RenderContent(Graphics graphics)
    {
        var settings = _settingsStore.Current;
        var activities = VisibleActivities(settings);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.ScaleTransform((float)settings.Scale, (float)settings.Scale);

        var bubbleAreaHeight = GetBubbleAreaHeight(settings, activities.Count);
        if (settings.ShowBubble)
        {
            DrawBubbles(graphics, activities, settings);
        }

        DrawRobot(graphics, bubbleAreaHeight, settings);
    }

    private void DrawBubbles(Graphics graphics, IReadOnlyList<DevSpaceActivity> activities, AppSettings settings)
    {
        if (settings.ResolvedBubbleStyle is BubbleVisualStyle.MonitorCardNeon or BubbleVisualStyle.MonitorCardClean)
        {
            DrawMonitorCards(graphics, activities, settings, settings.ResolvedBubbleStyle);
            return;
        }

        DrawSpeechBubbles(graphics, activities, settings);
    }

    private void DrawSpeechBubbles(Graphics graphics, IReadOnlyList<DevSpaceActivity> activities, AppSettings settings)
    {
        using var titleFont = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textFont = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleFormat = CreateSingleLineFormat(StringAlignment.Near);
        using var textFormat = CreateSingleLineFormat(StringAlignment.Near);
        var stride = SpeechBubbleHeight + SpeechBubbleGap;

        for (var index = 0; index < activities.Count; index++)
        {
            var activity = activities[index];
            var y = BubbleTop + index * stride;
            var palette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, activity.State);
            var rectangle = new RectangleF(9, y, BubbleWidth, SpeechBubbleHeight);
            using var path = RoundedRectangle(rectangle, 13f);
            using var background = new SolidBrush(palette.BubbleBackground);
            using var glow = new Pen(Color.FromArgb(palette.GlowAlpha, palette.Outline), settings.ResolvedTheme == PetTheme.Neon ? 9f : 5f);
            using var border = new Pen(palette.Outline, 2f);
            using var titleBrush = new SolidBrush(palette.Text);
            using var mutedBrush = new SolidBrush(palette.Muted);
            using var stateBrush = new SolidBrush(palette.StateColor);

            graphics.DrawPath(glow, path);
            graphics.FillPath(background, path);
            graphics.DrawPath(border, path);

            graphics.DrawString(
                activity.ProjectName,
                titleFont,
                titleBrush,
                new RectangleF(18, y + 7, BubbleWidth - 28, 22),
                titleFormat);
            graphics.DrawString(
                _localizer.Operation(activity.Operation, activity.Detail),
                textFont,
                mutedBrush,
                new RectangleF(18, y + 31, BubbleWidth - 28, 19),
                textFormat);
            graphics.FillEllipse(stateBrush, 18, y + 57, 8, 8);
            graphics.DrawString(
                $"{_localizer.State(activity.State)}  {FormatDuration(activity.Elapsed)}",
                smallFont,
                mutedBrush,
                new RectangleF(31, y + 52, BubbleWidth - 41, 18),
                textFormat);
        }

        var tailY = BubbleTop + ((activities.Count - 1) * stride) + SpeechBubbleHeight - 1;
        var tailPalette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, activities[^1].State);
        using var tailBrush = new SolidBrush(tailPalette.BubbleBackground);
        using var tailBorder = new Pen(tailPalette.Outline, 2f);
        var tail = new[]
        {
            new PointF(135, tailY),
            new PointF(157, tailY),
            new PointF(146, tailY + SpeechBubbleTailHeight)
        };
        graphics.FillPolygon(tailBrush, tail);
        graphics.DrawLines(tailBorder, tail);
    }

    private void DrawMonitorCards(
        Graphics graphics,
        IReadOnlyList<DevSpaceActivity> activities,
        AppSettings settings,
        BubbleVisualStyle style)
    {
        var clean = style == BubbleVisualStyle.MonitorCardClean;
        using var titleFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var operationFont = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var timeFont = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var badgeFont = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var titleFormat = CreateSingleLineFormat(StringAlignment.Near);
        using var operationFormat = CreateSingleLineFormat(StringAlignment.Near);
        using var timeFormat = CreateSingleLineFormat(StringAlignment.Far);
        using var badgeFormat = CreateSingleLineFormat(StringAlignment.Center);
        var stride = MonitorCardHeight + MonitorCardGap;

        for (var index = 0; index < activities.Count; index++)
        {
            var activity = activities[index];
            var y = BubbleTop + index * stride;
            var palette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, activity.State);
            var cardBackground = clean
                ? ResolveCleanCardBackground(settings.ResolvedBubbleTheme)
                : palette.BubbleBackground;
            var rectangle = new RectangleF(4, y, MonitorCardWidth, MonitorCardHeight);
            var shadowRectangle = new RectangleF(rectangle.X + 2, rectangle.Y + 4, rectangle.Width, rectangle.Height);
            using var shadowPath = RoundedRectangle(shadowRectangle, 16f);
            using var path = RoundedRectangle(rectangle, clean ? 15f : 16f);
            using var accentPath = RoundedRectangle(
                new RectangleF(rectangle.X + 7, rectangle.Y + 12, clean ? 3f : 4f, rectangle.Height - 24),
                2f);
            using var shadow = new SolidBrush(BlendColor(
                Color.Black,
                cardBackground,
                settings.ResolvedBubbleTheme == BubbleColorTheme.Dark ? 0.52f : 0.18f));
            using var background = new SolidBrush(cardBackground);
            using var border = new Pen(
                clean
                    ? ResolveCleanCardBorder(settings.ResolvedBubbleTheme)
                    : BlendColor(palette.Outline, cardBackground, 0.55f),
                clean ? 1f : 1.25f);
            using var accent = new SolidBrush(palette.StateColor);
            using var titleBrush = new SolidBrush(palette.Text);
            using var mutedBrush = new SolidBrush(palette.Muted);
            using var separator = new Pen(BlendColor(palette.Muted, cardBackground, clean ? 0.16f : 0.24f), 1f);

            if (!clean)
            {
                graphics.FillPath(shadow, shadowPath);
                using var glow = new Pen(
                    Color.FromArgb(settings.ResolvedTheme == PetTheme.Neon ? 55 : 24, palette.Outline),
                    settings.ResolvedTheme == PetTheme.Neon ? 7f : 4f);
                graphics.DrawPath(glow, path);
            }

            graphics.FillPath(background, path);
            graphics.DrawPath(border, path);
            graphics.FillPath(accent, accentPath);

            graphics.DrawString(
                activity.ProjectName,
                titleFont,
                titleBrush,
                new RectangleF(rectangle.X + 18, y + 10, 188, 21),
                titleFormat);
            graphics.DrawString(
                FormatDuration(activity.Elapsed),
                timeFont,
                titleBrush,
                new RectangleF(rectangle.Right - 78, y + 9, 62, 23),
                timeFormat);
            graphics.DrawString(
                _localizer.Operation(activity.Operation, activity.Detail),
                operationFont,
                mutedBrush,
                new RectangleF(rectangle.X + 18, y + 34, rectangle.Width - 36, 18),
                operationFormat);
            graphics.DrawLine(separator, rectangle.X + 18, y + 57, rectangle.Right - 18, y + 57);

            var statusText = _localizer.State(activity.State);
            var statusWidth = Math.Clamp(graphics.MeasureString(statusText, badgeFont).Width + 26f, 78f, 150f);
            var statusRectangle = new RectangleF(rectangle.X + 18, y + 63, statusWidth, 18);
            using var statusPath = RoundedRectangle(statusRectangle, 9f);
            using var statusBackground = new SolidBrush(BlendColor(
                palette.StateColor,
                cardBackground,
                clean ? 0.13f : 0.18f));
            using var statusBorder = new Pen(BlendColor(
                palette.StateColor,
                cardBackground,
                clean ? 0.42f : 0.62f), 1f);
            using var statusBrush = new SolidBrush(BlendColor(palette.StateColor, palette.Text, 0.72f));
            graphics.FillPath(statusBackground, statusPath);
            graphics.DrawPath(statusBorder, statusPath);
            graphics.FillEllipse(accent, statusRectangle.X + 8, statusRectangle.Y + 6, 6, 6);
            graphics.DrawString(
                statusText,
                badgeFont,
                statusBrush,
                new RectangleF(statusRectangle.X + 14, statusRectangle.Y + 1, statusRectangle.Width - 17, 15),
                badgeFormat);

            DrawActivityMeter(graphics, activity.State, palette, cardBackground, rectangle.Right - 66, y + 67);
        }

        var connectorStart = BubbleTop + ((activities.Count - 1) * stride) + MonitorCardHeight;
        var connectorPalette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, activities[^1].State);
        using var connector = new Pen(
            clean ? connectorPalette.StateColor : connectorPalette.Outline,
            clean ? 1.5f : 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var connectorDot = new SolidBrush(connectorPalette.StateColor);
        var centerX = LogicalWidth / 2f;
        graphics.DrawLine(connector, centerX, connectorStart + 1, centerX, connectorStart + MonitorConnectorHeight - 4);
        graphics.FillEllipse(connectorDot, centerX - 4, connectorStart + MonitorConnectorHeight - 8, 8, 8);
    }

    private void DrawActivityMeter(
        Graphics graphics,
        ActivityState state,
        Palette palette,
        Color cardBackground,
        float x,
        float y)
    {
        var activeSegment = state == ActivityState.Working
            ? (_frame / 4) % 3
            : state == ActivityState.Waiting ? 1 : 0;
        for (var index = 0; index < 3; index++)
        {
            var height = 5f + (index * 2f);
            var rectangle = new RectangleF(x + (index * 16f), y + (10f - height), 11f, height);
            var amount = index == activeSegment ? 0.9f : 0.25f;
            using var brush = new SolidBrush(BlendColor(palette.StateColor, cardBackground, amount));
            using var path = RoundedRectangle(rectangle, 2f);
            graphics.FillPath(brush, path);
        }
    }

    internal static Color ResolveCleanCardBackground(BubbleColorTheme theme) => theme switch
    {
        BubbleColorTheme.Light => Color.FromArgb(248, 250, 253),
        _ => Color.FromArgb(23, 27, 35)
    };

    internal static Color ResolveCleanCardBorder(BubbleColorTheme theme) => theme switch
    {
        BubbleColorTheme.Light => Color.FromArgb(205, 211, 222),
        _ => Color.FromArgb(61, 68, 82)
    };

    private static Color BlendColor(Color foreground, Color background, float foregroundAmount)
    {
        var amount = Math.Clamp(foregroundAmount, 0f, 1f);
        return Color.FromArgb(
            255,
            (int)Math.Round((foreground.R * amount) + (background.R * (1f - amount))),
            (int)Math.Round((foreground.G * amount) + (background.G * (1f - amount))),
            (int)Math.Round((foreground.B * amount) + (background.B * (1f - amount))));
    }

    private static StringFormat CreateSingleLineFormat(StringAlignment alignment) => new()
    {
        Alignment = alignment,
        LineAlignment = StringAlignment.Near,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.LineLimit
    };

    private void DrawRobot(Graphics graphics, int bubbleAreaHeight, AppSettings settings)
    {
        var transform = graphics.Save();
        var designWidth = 180f;
        var horizontalOffset = (LogicalWidth - (designWidth * RobotDesignScale)) / 2f;
        graphics.TranslateTransform(horizontalOffset, bubbleAreaHeight + 31f);
        graphics.ScaleTransform(RobotDesignScale, RobotDesignScale);

        var state = _snapshot.State;
        var palette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, state);
        var phase = _frame / 5d;
        var bob = state switch
        {
            ActivityState.Working => -Math.Abs(Math.Sin(phase * 1.8)) * 4,
            ActivityState.Waiting => -Math.Abs(Math.Sin(phase * 1.5)) * 10,
            ActivityState.Failed => 4,
            ActivityState.Stopped => 6,
            _ => Math.Sin(phase * 0.65) * 2.5
        };
        var legSwing = state == ActivityState.Working ? Math.Sin(phase * 2.6) * 7 : 0;
        var armSwing = state == ActivityState.Working ? Math.Sin(phase * 2.6 + 1.2) * 9 : Math.Sin(phase * 0.45) * 3;
        var baseY = (float)bob;

        using var body = new SolidBrush(state == ActivityState.Stopped ? Color.FromArgb(72, 75, 84) : Color.FromArgb(30, 34, 44));
        using var panel = new SolidBrush(Color.FromArgb(13, 17, 24));
        using var signal = new SolidBrush(palette.Signal);
        using var outline = new Pen(palette.Outline, 2f) { LineJoin = LineJoin.Round };
        using var outlineGlow = new Pen(Color.FromArgb(palette.GlowAlpha, palette.Outline), settings.ResolvedTheme == PetTheme.Neon ? 10f : 6f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var joint = new Pen(palette.Signal, 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var jointGlow = new Pen(Color.FromArgb(Math.Max(35, palette.GlowAlpha), palette.Outline), 10f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        using var shadowOuter = new SolidBrush(Color.FromArgb(28, palette.Outline));
        using var shadowInner = new SolidBrush(Color.FromArgb(75, palette.Outline));
        graphics.FillEllipse(shadowOuter, 42, baseY + 96, 104, 20);
        graphics.FillEllipse(shadowInner, 49, baseY + 100, 90, 12);

        var leftFootX = 66 + (float)legSwing;
        var rightFootX = 107 - (float)legSwing;
        var legY = baseY + 82;
        graphics.DrawLine(jointGlow, 76, baseY + 73, leftFootX, legY + 12);
        graphics.DrawLine(jointGlow, 104, baseY + 73, rightFootX, legY + 12);
        graphics.DrawLine(joint, 76, baseY + 73, leftFootX, legY + 12);
        graphics.DrawLine(joint, 104, baseY + 73, rightFootX, legY + 12);
        graphics.FillEllipse(body, leftFootX - 11, legY + 6, 26, 13);
        graphics.FillEllipse(body, rightFootX - 11, legY + 6, 26, 13);
        graphics.DrawEllipse(outline, leftFootX - 11, legY + 6, 26, 13);
        graphics.DrawEllipse(outline, rightFootX - 11, legY + 6, 26, 13);

        var leftArmY = baseY + 49 + (float)armSwing;
        var rightArmY = baseY + 49 - (float)armSwing;
        graphics.DrawLine(jointGlow, 61, baseY + 42, 43, leftArmY);
        graphics.DrawLine(jointGlow, 119, baseY + 42, 137, rightArmY);
        graphics.DrawLine(joint, 61, baseY + 42, 43, leftArmY);
        graphics.DrawLine(joint, 119, baseY + 42, 137, rightArmY);
        graphics.FillEllipse(body, 35, leftArmY - 7, 16, 16);
        graphics.FillEllipse(body, 129, rightArmY - 7, 16, 16);
        graphics.DrawEllipse(outline, 35, leftArmY - 7, 16, 16);
        graphics.DrawEllipse(outline, 129, rightArmY - 7, 16, 16);

        using var bodyPath = RoundedRectangle(new RectangleF(57, baseY + 34, 66, 56), 14);
        graphics.DrawPath(outlineGlow, bodyPath);
        graphics.FillPath(body, bodyPath);
        graphics.DrawPath(outline, bodyPath);
        graphics.FillEllipse(signal, 82, baseY + 56, 16, 16);

        using var headPath = RoundedRectangle(new RectangleF(47, baseY, 86, 56), 18);
        graphics.DrawPath(outlineGlow, headPath);
        graphics.FillPath(body, headPath);
        graphics.DrawPath(outline, headPath);
        using var facePath = RoundedRectangle(new RectangleF(56, baseY + 9, 68, 34), 11);
        graphics.FillPath(panel, facePath);

        var antennaX = 90 + (float)Math.Sin(phase) * 5;
        graphics.DrawLine(jointGlow, 90, baseY, antennaX, baseY - 14);
        graphics.DrawLine(joint, 90, baseY, antennaX, baseY - 14);
        graphics.FillEllipse(signal, antennaX - 5, baseY - 20, 11, 11);

        var blink = _frame % 110 > 102 && state is not ActivityState.Failed and not ActivityState.Stopped;
        if (state == ActivityState.Failed)
        {
            using var error = new Pen(palette.StateColor, 3f);
            graphics.DrawLine(error, 69, baseY + 20, 80, baseY + 29);
            graphics.DrawLine(error, 80, baseY + 20, 69, baseY + 29);
            graphics.DrawLine(error, 100, baseY + 20, 111, baseY + 29);
            graphics.DrawLine(error, 111, baseY + 20, 100, baseY + 29);
        }
        else if (blink || state == ActivityState.Stopped)
        {
            using var eye = new Pen(state == ActivityState.Stopped ? Color.Gray : palette.Signal, 3f);
            graphics.DrawLine(eye, 69, baseY + 25, 80, baseY + 25);
            graphics.DrawLine(eye, 100, baseY + 25, 111, baseY + 25);
        }
        else
        {
            graphics.FillEllipse(signal, 69, baseY + 19, 11, 13);
            graphics.FillEllipse(signal, 100, baseY + 19, 11, 13);
        }

        if (state == ActivityState.Stalled)
        {
            using var font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(palette.StateColor);
            graphics.DrawString("Z", font, brush, 137, baseY - 15);
        }

        graphics.Restore(transform);
    }

    private void UpdateMenu(AppSettings settings)
    {
        _bubbleItem.Text = _localizer["ShowBubble"];
        _bubbleItem.Checked = settings.ShowBubble;
        var themeMenu = (ToolStripMenuItem)_classicItem.OwnerItem!;
        themeMenu.Text = _localizer["Theme"];
        _classicItem.Text = _localizer["Classic"];
        _neonItem.Text = _localizer["Neon"];
        _classicItem.Checked = settings.ResolvedTheme == PetTheme.Classic;
        _neonItem.Checked = settings.ResolvedTheme == PetTheme.Neon;

        var bubbleThemeMenu = (ToolStripMenuItem)_lightBubbleItem.OwnerItem!;
        bubbleThemeMenu.Text = _localizer["BubbleTheme"];
        _lightBubbleItem.Text = _localizer["BubbleLight"];
        _darkBubbleItem.Text = _localizer["BubbleDark"];
        _lightBubbleItem.Checked = settings.ResolvedBubbleTheme == BubbleColorTheme.Light;
        _darkBubbleItem.Checked = settings.ResolvedBubbleTheme == BubbleColorTheme.Dark;

        var bubbleStyleMenu = (ToolStripMenuItem)_speechBubbleStyleItem.OwnerItem!;
        bubbleStyleMenu.Text = _localizer["BubbleStyle"];
        _speechBubbleStyleItem.Text = _localizer["BubbleSpeech"];
        _monitorCardNeonStyleItem.Text = _localizer["BubbleMonitorCardNeon"];
        _monitorCardCleanStyleItem.Text = _localizer["BubbleMonitorCardClean"];
        _speechBubbleStyleItem.Checked = settings.ResolvedBubbleStyle == BubbleVisualStyle.Speech;
        _monitorCardNeonStyleItem.Checked = settings.ResolvedBubbleStyle == BubbleVisualStyle.MonitorCardNeon;
        _monitorCardCleanStyleItem.Checked = settings.ResolvedBubbleStyle == BubbleVisualStyle.MonitorCardClean;

        var languageMenu = (ToolStripMenuItem)_autoLanguageItem.OwnerItem!;
        languageMenu.Text = _localizer["Language"];
        _autoLanguageItem.Text = _localizer["Auto"];
        _japaneseLanguageItem.Text = _localizer["Japanese"];
        _englishLanguageItem.Text = _localizer["English"];
        _autoLanguageItem.Checked = settings.LanguagePreference == UiLanguagePreference.Auto;
        _japaneseLanguageItem.Checked = settings.LanguagePreference == UiLanguagePreference.Japanese;
        _englishLanguageItem.Checked = settings.LanguagePreference == UiLanguagePreference.English;
        _settingsItem.Text = _localizer["Settings"];
        _resetItem.Text = _localizer["ResetPosition"];
        _exitItem.Text = _localizer["Exit"];
    }

    private void SetTheme(PetTheme theme) => UpdateSettings(settings => settings.Theme = theme.ToString());
    private void SetBubbleTheme(BubbleColorTheme theme) => UpdateSettings(settings => settings.BubbleTheme = theme.ToString());
    private void SetBubbleStyle(BubbleVisualStyle style) => UpdateSettings(settings => settings.BubbleStyle = style.ToString());
    private void SetLanguage(UiLanguagePreference language) => UpdateSettings(settings => settings.Language = language.ToString());

    private void UpdateSettings(Action<AppSettings> update)
    {
        var settings = _settingsStore.Current;
        update(settings);
        _settingsStore.Save(settings);
    }

    private void OnPetMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        _dragging = true;
        _dragMoved = false;
        _dragOffset = e.Location;
    }

    private void OnPetMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var cursor = Cursor.Position;
        var next = new Point(cursor.X - _dragOffset.X, cursor.Y - _dragOffset.Y);
        if (Math.Abs(next.X - Left) > 2 || Math.Abs(next.Y - Top) > 2)
        {
            _dragMoved = true;
        }
        Location = next;
    }

    private void OnPetMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        if (_dragMoved)
        {
            _positionStore.Save(Location);
        }
        else
        {
            UpdateSettings(settings => settings.ShowBubble = !settings.ShowBubble);
        }
    }

    private void RestorePosition()
    {
        var saved = _positionStore.Load();
        if (saved.HasValue)
        {
            Location = saved.Value;
            ClampToScreen();
            return;
        }
        MoveToBottomRight();
    }

    private void MoveToBottomRight()
    {
        var area = Screen.FromControl(this).WorkingArea;
        Location = new Point(area.Right - Width - 20, area.Bottom - Height - 12);
        _positionStore.Save(Location);
    }

    private void ClampToScreen()
    {
        var screen = Screen.AllScreens.FirstOrDefault(candidate => candidate.WorkingArea.IntersectsWith(Bounds))
                     ?? Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }
        var area = screen.WorkingArea;
        Location = new Point(
            Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string Trim(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }
        return value.Length <= maximum ? value : value[..Math.Max(1, maximum - 1)] + "…";
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
        : $"{duration.Minutes:00}:{duration.Seconds:00}";

    internal static (Color Background, Color Text, Color Muted) ResolveBubbleColors(BubbleColorTheme theme) => theme switch
    {
        BubbleColorTheme.Dark => (
            Color.FromArgb(24, 28, 38),
            Color.FromArgb(246, 247, 251),
            Color.FromArgb(184, 190, 204)),
        _ => (
            Color.FromArgb(250, 252, 255),
            Color.FromArgb(25, 31, 42),
            Color.FromArgb(76, 88, 107))
    };

    private sealed record Palette(
        Color StateColor,
        Color Outline,
        Color Signal,
        Color BubbleBackground,
        Color Text,
        Color Muted,
        int GlowAlpha)
    {
        public static Palette For(PetTheme theme, BubbleColorTheme bubbleTheme, ActivityState state)
        {
            var stateColor = state switch
            {
                ActivityState.Working => Color.FromArgb(75, 225, 130),
                ActivityState.Waiting => Color.FromArgb(255, 203, 58),
                ActivityState.Failed => Color.FromArgb(255, 83, 74),
                ActivityState.Stalled => Color.FromArgb(177, 108, 255),
                ActivityState.Stopped => Color.FromArgb(130, 135, 145),
                _ => Color.FromArgb(68, 160, 255)
            };
            var bubble = ResolveBubbleColors(bubbleTheme);

            if (theme == PetTheme.Neon)
            {
                var outline = state == ActivityState.Stopped ? Color.FromArgb(105, 108, 118) : Color.FromArgb(222, 0, 238);
                var signal = state == ActivityState.Stopped ? Color.FromArgb(145, 148, 157) : Color.FromArgb(255, 198, 53);
                return new Palette(
                    stateColor,
                    outline,
                    signal,
                    bubble.Background,
                    bubble.Text,
                    bubble.Muted,
                    65);
            }

            return new Palette(
                stateColor,
                stateColor,
                stateColor,
                bubble.Background,
                bubble.Text,
                bubble.Muted,
                28);
        }
    }
}
