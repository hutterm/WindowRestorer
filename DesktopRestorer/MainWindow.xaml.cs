using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DesktopRestorer
{
    public class MonitorVM
    {
        public MonitorVM()
        {
            var enumMonitors = ExternalMethods.EnumMonitors();
            var minX = enumMonitors.Min(monitorinfoex => monitorinfoex.Monitor.Left);
            var minY = enumMonitors.Min(monitorinfoex => monitorinfoex.Monitor.Top);
            var maxX = enumMonitors.Max(monitorinfoex => monitorinfoex.Monitor.Right);
            var maxY = enumMonitors.Max(monitorinfoex => monitorinfoex.Monitor.Bottom);
            Width = maxX - minX;
            Height = maxY - minY;
            Monitors = enumMonitors.Select(monitorinfoex => new MonVM()
            {
                Left = monitorinfoex.Monitor.Left+minX,
                Top=monitorinfoex.Monitor.Top+minY,
                Width = monitorinfoex.Monitor.Right - monitorinfoex.Monitor.Left,
                Height =   monitorinfoex.Monitor.Bottom - monitorinfoex.Monitor.Top
            }).ToList();
            
            /*foreach (var monitorinfoex in enumMonitors)
            {
                var rectangle = new Rectangle(){StrokeThickness = 10,Stroke = Brushes.Black};
                rectangle.SetValue(Canvas.LeftProperty,(double)monitorinfoex.Monitor.Left+minX);
                rectangle.SetValue(Canvas.TopProperty,(double)monitorinfoex.Monitor.Top+minY);
                rectangle.Width = monitorinfoex.Monitor.Right - monitorinfoex.Monitor.Left;
                rectangle.Height = monitorinfoex.Monitor.Bottom - monitorinfoex.Monitor.Top;
                DisplayCanvas.Children.Add(rectangle);
            }*/
        }

        public List<MonVM> Monitors { get; }
        public int Width { get; set; }

        public int Height  { get; set; }
    }

    public class MonVM : INotifyPropertyChanged
    {
        public int Left { get; set; }

        public int Top { get; set; }
        public int Width { get; set; }

        public int Height { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();
            var vx = ExternalMethods.GetSystemMetrics(ExternalMethods.SystemMetric.SM_CXVIRTUALSCREEN);
            var vy = ExternalMethods.GetSystemMetrics(ExternalMethods.SystemMetric.SM_CYVIRTUALSCREEN);

DisplayItems.DataContext = new MonitorVM();
            var processes = Process.GetProcesses();

            foreach (var handle in
                processes.Where(p => p.ProcessName == "chrome")
                    .SelectMany(process => ExternalMethods.EnumerateProcessWindowHandles(process.Id)))

            {
                var message = new StringBuilder(1000);
                ExternalMethods.SendMessage(handle, ExternalMethods.WM_GETTEXT, message.Capacity, message);
                var rect = new ExternalMethods.RECT();
                ExternalMethods.GetWindowRect(handle, ref rect);
                ExternalMethods.WINDOWPLACEMENT plcacement = new ExternalMethods.WINDOWPLACEMENT();
                ExternalMethods.GetWindowPlacement(handle, ref plcacement);
//                if (rect.left == 0 && rect.top == 0 && rect.right == 0 && rect.bottom == 0)
//                {
//                    if (message.Length == 0)
//                        continue;
//                }

                if (!ExternalMethods.IsWindowVisible(handle))
                    continue;
                TB.Text += $"{message} {rect.left} {rect.top} {rect.right} {rect.bottom} \n";

            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var processes = Process.GetProcessesByName("notepad");

            foreach (var p in processes)
            {
                var handle = p.MainWindowHandle;
                ExternalMethods.RECT Rect = new ExternalMethods.RECT();
                if (ExternalMethods.GetWindowRect(handle, ref Rect))
                    ExternalMethods.MoveWindow(handle, Rect.left - 5, Rect.top - 5, 1280, 720, true);
                Task.Delay(5000).ContinueWith(task => { ExternalMethods.ShowWindow(handle, ExternalMethods.ShowWindowCommands.Maximize); });
            }
        }
    }
}