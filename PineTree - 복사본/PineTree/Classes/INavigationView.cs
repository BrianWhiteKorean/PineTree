using System;

namespace PineTree.Classes
{
    public interface INavigationView
    {
        event EventHandler<NavigationRequestedEventArgs> NavigationRequested;
    }
}
