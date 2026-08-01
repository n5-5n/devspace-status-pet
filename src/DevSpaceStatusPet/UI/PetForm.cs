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
    private const int BubbleHeight = 76;
    private const int BubbleGap = 8;
    private const int BubbleStride = BubbleHeight + BubbleGap;
    private const int BubbleTailHeight = 16;
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
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        DoubleBuffered = true;
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
            languageMenu,
            _settingsItem,
            _resetItem,
            new ToolStripSeparator(),
            _exitItem
        ]);
        ContextMenuStrip = _menu;

        _bubbleItem.Click += (_, _) => UpdateSettings(settings => settings.ShowBubble = _bubbleItem.Checked);
        _classicItem.Click += (_, _) => SetTheme(PetTheme.Classic);
        _neonItem.Click += (_, _) => SetTheme(PetTheme.Neon);
        _lightBubbleItem.Click += (_, _) => SetBubbleTheme(BubbleColorTheme.Light);
        _darkBubbleItem.Click += (_, _) => SetBubbleTheme(BubbleColorTheme.Dark);
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
            Invalidate();
        };

        _settingsStore.Changed += (_, _) => ApplySettings();
        Shown += (_, _) =>
        {
            ApplySettings();
            RestorePosition();
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

    public void ApplySnapshot(DevSpaceSnapshot snapshot)
    {
        _snapshot = snapshot;
        ApplySettings();
        Invalidate();
    }

    private void ApplySettings()
    {
        var settings = _settingsStore.Current;
        Opacity = settings.Opacity;
        ResizeForContent(settings);
        UpdateMenu(settings);
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
        Invalidate();
    }

    internal static Size CalculateClientSize(AppSettings settings, int activityCount)
    {
        var logicalHeight = GetBubbleAreaHeight(settings.ShowBubble, activityCount) + RobotLogicalHeight;
        return new Size(
            (int)Math.Ceiling(LogicalWidth * settings.Scale),
            (int)Math.Ceiling(logicalHeight * settings.Scale));
    }

    internal static int GetBubbleAreaHeight(bool showBubble, int count)
    {
        if (!showBubble)
        {
            return 0;
        }

        var visibleCount = Math.Max(1, count);
        return BubbleTop + (visibleCount * BubbleHeight) + ((visibleCount - 1) * BubbleGap) + BubbleTailHeight;
    }

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
        var settings = _settingsStore.Current;
        var activities = VisibleActivities(settings);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.ScaleTransform((float)settings.Scale, (float)settings.Scale);

        var bubbleAreaHeight = GetBubbleAreaHeight(settings.ShowBubble, activities.Count);
        if (settings.ShowBubble)
        {
            DrawBubbles(graphics, activities, settings);
        }

        DrawRobot(graphics, bubbleAreaHeight, settings);
    }

    private void DrawBubbles(Graphics graphics, IReadOnlyList<DevSpaceActivity> activities, AppSettings settings)
    {
        using var titleFont = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textFont = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleFormat = CreateSingleLineFormat(StringAlignment.Near);
        using var textFormat = CreateSingleLineFormat(StringAlignment.Near);

        for (var index = 0; index < activities.Count; index++)
        {
            var activity = activities[index];
            var y = BubbleTop + index * BubbleStride;
            var palette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, activity.State);
            var rectangle = new RectangleF(9, y, BubbleWidth, BubbleHeight);
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

        var tailY = BubbleTop + ((activities.Count - 1) * BubbleStride) + BubbleHeight - 1;
        var tailPalette = Palette.For(settings.ResolvedTheme, settings.ResolvedBubbleTheme, activities[^1].State);
        using var tailBrush = new SolidBrush(tailPalette.BubbleBackground);
        using var tailBorder = new Pen(tailPalette.Outline, 2f);
        var tail = new[]
        {
            new PointF(135, tailY),
            new PointF(157, tailY),
            new PointF(146, tailY + BubbleTailHeight)
        };
        graphics.FillPolygon(tailBrush, tail);
        graphics.DrawLines(tailBorder, tail);
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
