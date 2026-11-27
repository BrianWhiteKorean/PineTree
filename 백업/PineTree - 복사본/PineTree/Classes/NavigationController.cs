using System;
using System.Windows.Forms;

namespace PineTree.Classes
{
    public class NavigationController : IDisposable
    {
        private readonly Control _target;

        public NavigationController(Control target)
        {
            _target = target;
            _target.MouseClick += Target_MouseClick;
            _target.MouseMove += Target_MouseMove;
            _target.MouseLeave += Target_MouseLeave;
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;

        public void Dispose()
        {
            _target.MouseClick -= Target_MouseClick;
            _target.MouseMove -= Target_MouseMove;
            _target.MouseLeave -= Target_MouseLeave;
        }

        private void Target_MouseClick(object sender, MouseEventArgs e)
        {
            AppScreen screen;
            if (NavigationMap.TryGetScreen(e.Location, out screen))
            {
                NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs(screen));
            }
        }

        private void Target_MouseMove(object sender, MouseEventArgs e)
        {
            AppScreen screen;
            _target.Cursor = NavigationMap.TryGetScreen(e.Location, out screen)
                ? Cursors.Hand
                : Cursors.Default;
        }

        private void Target_MouseLeave(object sender, EventArgs e)
        {
            _target.Cursor = Cursors.Default;
        }
    }
}
