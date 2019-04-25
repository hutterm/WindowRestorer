using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopRestorer {
    public class ProcessWindow : INotifyPropertyChanged
    {
        public int ZOrder { get; set; }
        public DisplaySetup Parent { get; set; }

        public string Name { get; set; }
        public string ProcName { get; set; }
        public ExternalMethods.RECT Rect { get; set; }
        public double Top => Rect.top - Parent.MinY;
        public double Left => Rect.left - Parent.MinX;
        public double Width => Rect.right - Rect.left;
        public double Height => Rect.bottom - Rect.top;
        public ExternalMethods.WINDOWPLACEMENT Placement { get; set; }
        public IntPtr Handle { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}