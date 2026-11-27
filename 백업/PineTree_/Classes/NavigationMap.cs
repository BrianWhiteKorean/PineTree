using System.Collections.Generic;
using System.Drawing;

namespace PineTree.Classes
{
    public static class NavigationMap
    {
        private static readonly IReadOnlyDictionary<AppScreen, Rectangle> Areas =
            new Dictionary<AppScreen, Rectangle>
            {
                { AppScreen.Main, new Rectangle(40, 45, 242, 46) },
                { AppScreen.CustInfo, new Rectangle(40, 112, 242, 46) },
                { AppScreen.PriceItems, new Rectangle(40, 179, 242, 46) }
            };

        public static bool TryGetScreen(Point location, out AppScreen screen)
        {
            foreach (KeyValuePair<AppScreen, Rectangle> area in Areas)
            {
                if (area.Value.Contains(location))
                {
                    screen = area.Key;
                    return true;
                }
            }

            screen = AppScreen.Main;
            return false;
        }
    }
}
