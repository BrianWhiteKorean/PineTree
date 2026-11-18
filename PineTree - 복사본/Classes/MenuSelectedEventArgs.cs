using System;

namespace PineTree.Classes
{
    public class MenuSelectedEventArgs : EventArgs
    {
        public MenuSelectedEventArgs(MenuItemInfo item)
        {
            Item = item;
        }

        public MenuItemInfo Item { get; private set; }
    }
}
