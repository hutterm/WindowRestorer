using System;
using System.Diagnostics;
using System.Drawing;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using DynamicData.Binding;
using Console = System.Console;
using Point = System.Windows.Point;

namespace DesktopRestorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainVM _mainVM;

        public MainWindow()
        {
            InitializeComponent();
            _mainVM = new MainVM();

            DataContext = _mainVM;
            Closing += (sender, args) => { Automation.RemoveAllEventHandlers(); };
            Loaded += (sender, args) => _mainVM.InitWindowhandle();
            
        }

        private void FrameworkElement_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var size = DWMRectangle.PointToScreen(new Point(DWMRectangle.ActualWidth, DWMRectangle.ActualHeight)) - DWMRectangle.PointToScreen(new Point(0, 0));

            var translatePoint = DWMRectangle.TranslatePoint(new Point(), this);
            _mainVM.SelectedWindowRectangle = new Rectangle((int) translatePoint.X,(int) translatePoint.Y, (int) size.X,(int) size.Y);
        }
    }
}