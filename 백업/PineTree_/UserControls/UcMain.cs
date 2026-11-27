using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;

namespace PineTree.UserControls
{
    public partial class UcMain : UserControl, INavigationView
    {
        private readonly NavigationController _navigationController;

        public UcMain()
        {
            InitializeComponent();

            _navigationController = new NavigationController(picBackground);
            _navigationController.NavigationRequested += NavigationController_NavigationRequested;
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;

        private void NavigationController_NavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }
    }
}
