using System.Runtime.InteropServices;

namespace DevSpaceStatusPet.UI;

internal static class DarkUiTheme
{
    public static Color WindowBackground { get; } = Color.FromArgb(24, 27, 34);
    public static Color PanelBackground { get; } = Color.FromArgb(24, 27, 34);
    public static Color InputBackground { get; } = Color.FromArgb(36, 41, 51);
    public static Color ButtonBackground { get; } = Color.FromArgb(43, 49, 61);
    public static Color MenuBackground { get; } = Color.FromArgb(30, 34, 43);
    public static Color MenuSelection { get; } = Color.FromArgb(56, 65, 82);
    public static Color MenuPressed { get; } = Color.FromArgb(66, 76, 96);
    public static Color Foreground { get; } = Color.FromArgb(239, 242, 248);
    public static Color MutedForeground { get; } = Color.FromArgb(174, 183, 199);
    public static Color Border { get; } = Color.FromArgb(74, 84, 103);
    public static Color Accent { get; } = Color.FromArgb(88, 166, 255);

    public static ToolStripRenderer MenuRenderer { get; } =
        new ToolStripProfessionalRenderer(new DarkMenuColorTable())
        {
            RoundedEdges = false
        };

    public static void ApplyMenu(ContextMenuStrip menu)
    {
        menu.Renderer = MenuRenderer;
        menu.BackColor = MenuBackground;
        menu.ForeColor = Foreground;
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = true;
        ApplyMenuItems(menu.Items);
        menu.Opening += (_, _) => ApplyMenuItems(menu.Items);
    }

    public static void ApplyWindow(Control root)
    {
        ApplyControl(root);
    }

    public static void ApplyImmersiveDarkTitleBar(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return;
        }

        try
        {
            var enabled = 1;
            if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
        }
        catch
        {
            // Older Windows builds may not expose immersive dark title bars.
        }
    }

    private static void ApplyMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = MenuBackground;
            item.ForeColor = item.Enabled ? Foreground : MutedForeground;

            if (item is ToolStripSeparator separator)
            {
                separator.BackColor = MenuBackground;
                continue;
            }

            if (item is not ToolStripMenuItem menuItem)
            {
                continue;
            }

            menuItem.DropDown.Renderer = MenuRenderer;
            menuItem.DropDown.BackColor = MenuBackground;
            menuItem.DropDown.ForeColor = Foreground;
            ApplyMenuItems(menuItem.DropDownItems);
        }
    }

    private static void ApplyControl(Control control)
    {
        switch (control)
        {
            case Form:
            case TableLayoutPanel:
            case FlowLayoutPanel:
            case Panel:
            case GroupBox:
                control.BackColor = WindowBackground;
                control.ForeColor = Foreground;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = InputBackground;
                comboBox.ForeColor = Foreground;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;

            case NumericUpDown numeric:
                numeric.BackColor = InputBackground;
                numeric.ForeColor = Foreground;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                break;

            case TextBoxBase textBox:
                textBox.BackColor = InputBackground;
                textBox.ForeColor = Foreground;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case Button button:
                button.UseVisualStyleBackColor = false;
                button.BackColor = ButtonBackground;
                button.ForeColor = Foreground;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = MenuSelection;
                button.FlatAppearance.MouseDownBackColor = MenuPressed;
                break;

            case CheckBox checkBox:
                checkBox.UseVisualStyleBackColor = false;
                checkBox.BackColor = WindowBackground;
                checkBox.ForeColor = Foreground;
                checkBox.FlatStyle = FlatStyle.Flat;
                break;

            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = Foreground;
                break;

            default:
                control.BackColor = WindowBackground;
                control.ForeColor = Foreground;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControl(child);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    private sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => MenuBackground;
        public override Color ImageMarginGradientBegin => MenuBackground;
        public override Color ImageMarginGradientMiddle => MenuBackground;
        public override Color ImageMarginGradientEnd => MenuBackground;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => MenuSelection;
        public override Color MenuItemSelectedGradientBegin => MenuSelection;
        public override Color MenuItemSelectedGradientEnd => MenuSelection;
        public override Color MenuItemPressedGradientBegin => MenuPressed;
        public override Color MenuItemPressedGradientMiddle => MenuPressed;
        public override Color MenuItemPressedGradientEnd => MenuPressed;
        public override Color CheckBackground => MenuSelection;
        public override Color CheckSelectedBackground => MenuPressed;
        public override Color CheckPressedBackground => MenuPressed;
        public override Color ButtonSelectedBorder => Border;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => MenuBackground;
    }
}
