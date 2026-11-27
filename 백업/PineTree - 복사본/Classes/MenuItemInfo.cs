namespace PineTree.Classes
{
    public class MenuItemInfo
    {
        public MenuItemInfo(AppScreen screen, string title, string iconText, string badgeText)
        {
            Screen = screen;
            Title = title;
            IconText = iconText;
            BadgeText = badgeText;
        }

        public AppScreen Screen { get; private set; }

        public string Title { get; private set; }

        public string IconText { get; private set; }

        public string BadgeText { get; set; }
    }
}
