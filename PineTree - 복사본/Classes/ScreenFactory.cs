using System.Collections.Generic;
using System.Windows.Forms;
using PineTree.UserControls;

namespace PineTree.Classes
{
    public class ScreenFactory
    {
        private readonly Dictionary<AppScreen, UserControl> _cache = new Dictionary<AppScreen, UserControl>();

        public UserControl GetScreen(AppScreen screen)
        {
            UserControl control;
            if (_cache.TryGetValue(screen, out control))
            {
                return control;
            }

            control = CreateScreen(screen);
            control.Dock = DockStyle.Fill;
            _cache.Add(screen, control);
            return control;
        }

        private UserControl CreateScreen(AppScreen screen)
        {
            switch (screen)
            {
                case AppScreen.Main:
                    return new UcDashboardHome();
                case AppScreen.CustInfo:
                    return new UcCustInfo();
                case AppScreen.PriceItems:
                    return new UcPriceItems();
                case AppScreen.Order:
                    return new UcOrderInfo();
                case AppScreen.Print:
                    return new UcPlaceholderPage("출력물관리", showNotice: false);
                case AppScreen.Process:
                    return new UcPlaceholderPage("공정관리", showNotice: false);
                case AppScreen.Settlement:
                    return new UcPlaceholderPage("정산관리", showNotice: false);
                case AppScreen.Settings:
                    return new UcConfig();
                default:
                    return new UcPlaceholderPage("준비중");
            }
        }
    }
}
