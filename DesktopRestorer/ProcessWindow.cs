using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Vanara.PInvoke;

namespace DesktopRestorer {
    public class ProcessWindow : INotifyPropertyChanged
    {
        public ProcessWindow(DisplaySetup parent)
        {
            Parent = parent;
        }
        public int ZOrder { get; set; }
        public DisplaySetup Parent { get; set; }

        public string Name { get; set; }
        public string ProcName { get; set; }
        public Rect Rect { get; set; }
        public double Top => Rect.Top - Parent.MinY;
        public double Left => Rect.Left - Parent.MinX;
        public double Width => Rect.Right - Rect.Left;
        public double Height => Rect.Bottom - Rect.Top;
        public User32_Gdi.WINDOWPLACEMENT Placement { get; set; }
        public IntPtr Handle { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}