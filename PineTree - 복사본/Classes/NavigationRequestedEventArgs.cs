using System;

namespace PineTree.Classes
{
    public class NavigationRequestedEventArgs : EventArgs
    {
        public NavigationRequestedEventArgs(AppScreen screen)
        {
            Screen = screen;
        }

        public AppScreen Screen { get; }
    }
}
