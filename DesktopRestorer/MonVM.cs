using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopRestorer {
    public class MonVM : INotifyPropertyChanged
    {
        public int Left { get; set; }

        public int Top { get; set; }
        public int Width { get; set; }

        public int Height { get; set; }
        public string Name { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}