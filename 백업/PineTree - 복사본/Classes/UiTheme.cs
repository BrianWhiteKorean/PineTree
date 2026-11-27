using System.Drawing;

namespace PineTree.Classes
{
    public static class UiTheme
    {
        // Base palette sampled from Resources\BackMain_01.png.
        public static readonly Color AppBackground = Color.FromArgb(15, 17, 33);
        public static readonly Color TopBarBackground = Color.FromArgb(21, 23, 48);
        public static readonly Color MenuBackground = Color.FromArgb(18, 20, 39);
        public static readonly Color MenuSelectedBackground = Color.FromArgb(22, 33, 54);
        public static readonly Color PanelBackground = Color.FromArgb(28, 33, 60);
        public static readonly Color PanelHeaderBackground = Color.FromArgb(30, 35, 64);
        public static readonly Color PanelBorder = Color.FromArgb(38, 49, 81);
        public static readonly Color GridHeaderBackground = Color.FromArgb(31, 36, 64);
        public static readonly Color GridLine = Color.FromArgb(45, 58, 95);
        public static readonly Color TextPrimary = Color.FromArgb(238, 247, 255);
        public static readonly Color TextSecondary = Color.FromArgb(110, 128, 159);
        public static readonly Color Accent = Color.FromArgb(43, 211, 255);
        public static readonly Color ButtonBlue = Color.FromArgb(53, 121, 196);
        public static readonly Color OutlineButtonBackground = Color.FromArgb(24, 43, 79);
        public static readonly Color OutlineButtonBorder = Color.FromArgb(46, 116, 145);

        public const int MenuWidth = 240;
        public const int TopBarHeight = 60;
        public const int ContentPadding = 28;
    }
}
