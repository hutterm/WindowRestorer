using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

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
            Activated += (sender, args) => _mainVM.InitWindowhandle();
        }

        private void FrameworkElement_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var size = DWMRectangle.PointToScreen(new Point(DWMRectangle.ActualWidth, DWMRectangle.ActualHeight)) - DWMRectangle.PointToScreen(new Point(0, 0)); 

            _mainVM.SelectedWindowRectangle = new Rect(DWMRectangle.TranslatePoint(new Point(), this),size);
        }
    }
}