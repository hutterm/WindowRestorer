using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Win32;

namespace DesktopRestorer
{
    public class MainVM : INotifyPropertyChanged
    {
        private readonly SourceCache<ProcessWindow, IntPtr> _windows =
            new SourceCache<ProcessWindow, IntPtr>(window => window.Handle);

        private readonly SourceList<DisplaySetup> _setups = new SourceList<DisplaySetup>();


        private readonly Subject<(AutomationElement, AutomationPropertyChangedEventArgs)> _boundingRectSubject =
            new Subject<(AutomationElement, AutomationPropertyChangedEventArgs)>();

        private Rect _selectedWindowRectangle;
        private ProcessWindow _selectedWindow;

        private void AutomationEventHandler(object sender, AutomationPropertyChangedEventArgs e) =>
            _boundingRectSubject.OnNext((sender as AutomationElement, e));

        public MainVM()
        {
            
            var displaySetup = InitMonitor();
            CurrentSetup = SelectedSetup = displaySetup;
            _setups.Add(displaySetup);


            var i = 0;
            /*
            foreach (var process in
                Process.GetProcesses())
            {
                foreach (var handle in
                    ExternalMethods.EnumerateProcessWindowHandles(process.Id))
            }*/
            ExternalMethods.EnumWindows((handle, param) =>
            {
                ExternalMethods.GetWindowThreadProcessId(handle, out var id);
                var process = Process.GetProcessById((int) id);

                var message = new StringBuilder(1000);
                ExternalMethods.GetWindowText(handle, message, message.Capacity);
                var rect = new ExternalMethods.RECT();
                ExternalMethods.GetWindowRect(handle, ref rect);
                var placement = new ExternalMethods.WINDOWPLACEMENT();
                ExternalMethods.GetWindowPlacement(handle, ref placement);
                if (!ExternalMethods.IsWindowVisible(handle))
                    return true;
                var processWindow = new ProcessWindow()
                {
                    Parent = CurrentSetup,
                    Handle = handle,
                    Name = message.ToString(), ProcName = process.ProcessName, Rect = rect, Placement = placement,
                    ZOrder = i
                };
                _windows.AddOrUpdate(processWindow);
                i++;
                return true;
            }, IntPtr.Zero);

            Automation.AddAutomationPropertyChangedEventHandler(AutomationElement.RootElement, TreeScope.Descendants,
                AutomationEventHandler, AutomationElement.BoundingRectangleProperty);
            _boundingRectSubject
                .GroupByUntil(tuple => tuple.Item1.Current.NativeWindowHandle, tuple => tuple,
                    g => Observable.Timer(TimeSpan.FromMilliseconds(100))).SelectMany(g => g.LastAsync()).Subscribe(
                    observable =>
                    {
                        try
                        {
                            var optional = _windows.Lookup((IntPtr) observable.Item1.Current.NativeWindowHandle);
                            if (optional.HasValue)
                            {
                                optional.Value.Rect = new ExternalMethods.RECT((Rect) observable.Item2.NewValue);
//                                _windows.AddOrUpdate(optional.Value);
                            }

//                            else
                            return;

                            {
                                var handle = (IntPtr) observable.Item1.Current.NativeWindowHandle;

                                ExternalMethods.GetWindowThreadProcessId(handle, out var id);
                                var process = Process.GetProcessById((int) id);

                                var message = new StringBuilder(1000);
                                ExternalMethods.SendMessage(handle, ExternalMethods.WM_GETTEXT, message.Capacity,
                                    message);
                                ExternalMethods.WINDOWPLACEMENT placement = new ExternalMethods.WINDOWPLACEMENT();
                                ExternalMethods.GetWindowPlacement(handle, ref placement);
                                if (ExternalMethods.IsWindowVisible(handle))
                                {
                                    //TB.Text += $"{message} {rect.left} {rect.top} {rect.right} {rect.bottom} \n";
                                    var processWindow = new ProcessWindow()
                                    {
                                        Handle = handle,
                                        Name = message.ToString(), ProcName = process.ProcessName,
                                        Rect = new ExternalMethods.RECT((Rect) observable.Item2.NewValue),
                                        Placement = placement,
                                        ZOrder = i
                                    };
                                    _windows.AddOrUpdate(processWindow);
                                }
                            }

//                            Debug.WriteLine(
//                                $"Bounding rect of {observable.Item1.Current.Name} changed to {observable.Item2.NewValue}");
                        }
                        catch (ElementNotAvailableException) { }
                    });


            _windows.Connect()
                .Sort(SortExpressionComparer<ProcessWindow>.Descending(window => window.ZOrder))
                .ObserveOnDispatcher()
                .Bind(out var rooc).Subscribe();
            Windows = rooc;

            SystemEvents.DisplaySettingsChanged += (sender, args) =>
            {
                var ds = InitMonitor();
                //todo: check if setup already exists
                if (CurrentSetup != null) CurrentSetup.IsCurrent = false;
                var foundSetup = _setups.Items.FirstOrDefault(setup =>
                {
                    if (setup.Monitors.Count != ds.Monitors.Count)
                        return false;

                    for (int j = 0; j < setup.Monitors.Count; j++)
                    {
                        if (setup.Monitors[j].Left != ds.Monitors[j].Left)
                            return false;
                        if (setup.Monitors[j].Top != ds.Monitors[j].Top)
                            return false;
                        if (setup.Monitors[j].Width != ds.Monitors[j].Width)
                            return false;
                        if (setup.Monitors[j].Height != ds.Monitors[j].Height)
                            return false;
                    }

                    return true;
                });
                if (foundSetup != null)
                {
                    //todo: compare windows and move them to the correct location!
                }
                else { }

                CurrentSetup = ds;
                _setups.Add(ds);
                Debug.WriteLine("display changed");
            };
        }

        public void InitWindowhandle()
        {
            _windowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
        }

        private DisplaySetup InitMonitor()
        {
            var enumMonitors = ExternalMethods.EnumMonitors();
            var maxX = enumMonitors.Max(monitorinfoex => monitorinfoex.Monitor.Right);
            var maxY = enumMonitors.Max(monitorinfoex => monitorinfoex.Monitor.Bottom);
            var minX = enumMonitors.Min(monitorinfoex => monitorinfoex.Monitor.Left);
            var minY = enumMonitors.Min(monitorinfoex => monitorinfoex.Monitor.Top);
            var ds = new DisplaySetup
            {
                IsCurrent = true,
                Name = $"{enumMonitors.Count} monitor{(enumMonitors.Count > 1 ? "s" : "")}",
                MinX = minX,
                MinY = minY,
                Width = maxX - minX,
                Height = maxY - minY,
                Monitors = enumMonitors.Select(monitorinfoex => new MonVM()
                {
                    Left = monitorinfoex.Monitor.Left - minX,
                    Top = monitorinfoex.Monitor.Top - minY,
                    Width = monitorinfoex.Monitor.Right - monitorinfoex.Monitor.Left,
                    Height = monitorinfoex.Monitor.Bottom - monitorinfoex.Monitor.Top,
                    Name = monitorinfoex.DeviceName
                }).ToList()
            };
            return ds;
        }

        public ReadOnlyObservableCollection<ProcessWindow> Windows { get; }

        private IntPtr _thumb;
        private IntPtr _windowHandle;

        public ProcessWindow SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                if (_selectedWindow != null && _thumb != IntPtr.Zero) ExternalMethods.DwmUnregisterThumbnail(_thumb);
                _selectedWindow = value;
                if (_selectedWindow != null &&
                    ExternalMethods.DwmRegisterThumbnail(_windowHandle, _selectedWindow.Handle, out _thumb) == 0)
                    UpdateThumb();
            }
        }

        private void UpdateThumb()
        {
            if (_thumb == IntPtr.Zero)
                return;
            var props = new ExternalMethods.DWM_THUMBNAIL_PROPERTIES
            {
                fVisible = true,
                dwFlags = ExternalMethods.DWM_TNP_VISIBLE | ExternalMethods.DWM_TNP_RECTDESTINATION |
                          ExternalMethods.DWM_TNP_OPACITY,
                opacity = 0xFF,
                rcDestination = new ExternalMethods.RECT(_selectedWindowRectangle)
            };
            ExternalMethods.DwmUpdateThumbnailProperties(_thumb, ref props);
        }

        public DisplaySetup SelectedSetup { get; set; }
        public DisplaySetup CurrentSetup { get; set; }

        public ReadOnlyObservableCollection<DisplaySetup> Setups { get; }

        public Rect SelectedWindowRectangle
        {
            get => _selectedWindowRectangle;
            set
            {
                _selectedWindowRectangle = value; 
                UpdateThumb();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DisplaySetup
    {
        public List<MonVM> Monitors { get; set; }
        public int Width { get; set; }

        public int Height { get; set; }

        public int MinX { get; set; }
        public int MinY { get; set; }
        public bool IsCurrent { get; set; }
        public string Name { get; set; }
    }
}