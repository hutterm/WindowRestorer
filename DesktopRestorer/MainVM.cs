using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using WindowsDisplayAPI;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Win32;
using Vanara.PInvoke;

namespace DesktopRestorer
{
    public class MainVM : INotifyPropertyChanged
    {
        private readonly SourceCache<ProcessWindow, IntPtr> _windows =
            new SourceCache<ProcessWindow, IntPtr>(window => window.Handle);

        private readonly SourceList<DisplaySetup> _setups = new SourceList<DisplaySetup>();


        private readonly Subject<(AutomationElement, AutomationPropertyChangedEventArgs)> _boundingRectSubject =
            new Subject<(AutomationElement, AutomationPropertyChangedEventArgs)>();

        private Rectangle _selectedWindowRectangle;
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
//            ExternalMethods.EnumWindows((handle, param) =>
//            {
//                ExternalMethods.GetWindowThreadProcessId(handle, out var id);
//                var process = Process.GetProcessById((int) id);
//
//                var message = new StringBuilder(1000);
//                ExternalMethods.GetWindowText(handle, message, message.Capacity);
//                var rect = new ExternalMethods.RECT();
//                ExternalMethods.GetWindowRect(handle, ref rect);
//                var placement = new ExternalMethods.WINDOWPLACEMENT();
//                ExternalMethods.GetWindowPlacement(handle, ref placement);
//                if (!ExternalMethods.IsWindowVisible(handle))
//                    return true;
//                var processWindow = new ProcessWindow()
//                {
//                    Parent = CurrentSetup,
//                    Handle = handle,
//                    Name = message.ToString(), ProcName = process.ProcessName, Rect = rect, Placement = placement,
//                    ZOrder = i
//                };
//                _windows.AddOrUpdate(processWindow);
//                i++;
//                return true;
//            }, IntPtr.Zero);

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
                                optional.Value.Rect = ((Rect) observable.Item2.NewValue);
                            }
                            else
                            {
                                var handle = (IntPtr) observable.Item1.Current.NativeWindowHandle;

                                if (((User32_Gdi.WindowStyles)User32_Gdi.GetWindowLong(handle, User32_Gdi.WindowLongFlags.GWL_STYLE) &
                                      TARGETWINDOW) !=  TARGETWINDOW)
                                    return;
                                User32_Gdi.GetWindowThreadProcessId(handle, out var id);
                                var process = Process.GetProcessById((int) id);

                                var message = new StringBuilder(1000);
                                User32_Gdi.GetWindowText(handle, message, message.Capacity);
                                var placement = new User32_Gdi.WINDOWPLACEMENT();
                                User32_Gdi.GetWindowPlacement(handle, ref placement);
                                if (User32_Gdi.IsWindowVisible(handle))
                                {
                                    //TB.Text += $"{message} {rect.left} {rect.top} {rect.right} {rect.bottom} \n";
                                    var processWindow = new ProcessWindow(CurrentSetup)
                                    {
                                        Handle = handle,
                                        Name = message.ToString(), ProcName = process.ProcessName,
                                        Rect = ((Rect) observable.Item2.NewValue),
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

            _setups.Connect().ObserveOnDispatcher().Bind(out var soc).Subscribe();
            Setups = soc;
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
            var displays = Display.GetDisplays().ToList();
            var maxX = displays.Max(d => d.CurrentSetting.Position.X+d.CurrentSetting.Resolution.Width);
            var maxY = displays.Max(d => d.CurrentSetting.Position.Y+d.CurrentSetting.Resolution.Height);
            var minX = displays.Min(d => d.CurrentSetting.Position.X);
            var minY = displays.Min(d => d.CurrentSetting.Position.Y);
            var ds = new DisplaySetup
            {
                IsCurrent = true,
                Name = $"{displays.Count} monitor{(displays.Count > 1 ? "s" : "")}",
                MinX = minX,
                MinY = minY,
                Width = maxX - minX,
                Height = maxY - minY,
                Monitors = displays.Select(display => new MonVM()
                {
                    Left =display.CurrentSetting.Position.X - minX,
                    Top = display.CurrentSetting.Position.Y - minY,
                    Width= display.CurrentSetting.Resolution.Width,
                    Height= display.CurrentSetting.Resolution.Height,
                    Name = display.DeviceName
                }).ToList()
            };
            return ds;
        }

        public ReadOnlyObservableCollection<ProcessWindow> Windows { get; }

        private HTHUMBNAIL _thumb;
        private IntPtr _windowHandle;

        public ProcessWindow SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                if (_selectedWindow != null && _thumb != IntPtr.Zero) DwmApi.DwmUnregisterThumbnail(_thumb);
                _selectedWindow = value;
                if (_selectedWindow != null &&
                    DwmApi.DwmRegisterThumbnail(_windowHandle, _selectedWindow.Handle, out _thumb) == 0)
                    UpdateThumb();
            }
        }

        private void UpdateThumb()
        {
            if (_thumb == IntPtr.Zero)
                return;
            var props = new DwmApi.DWM_THUMBNAIL_PROPERTIES
            {
                fVisible = true,
                dwFlags = DwmApi.DWM_TNP.DWM_TNP_VISIBLE | DwmApi.DWM_TNP.DWM_TNP_RECTDESTINATION |
                          DwmApi.DWM_TNP.DWM_TNP_OPACITY,
                opacity = 0xFF,
                rcDestination = new RECT(_selectedWindowRectangle)
            };
            DwmApi.DwmUpdateThumbnailProperties(_thumb, props);
        }

        public DisplaySetup SelectedSetup { get; set; }
        public DisplaySetup CurrentSetup { get; set; }

        public ReadOnlyObservableCollection<DisplaySetup> Setups { get; }

        public Rectangle SelectedWindowRectangle
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

        public static readonly User32_Gdi.WindowStyles TARGETWINDOW = User32_Gdi.WindowStyles.WS_BORDER | User32_Gdi.WindowStyles.WS_VISIBLE;
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