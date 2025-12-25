using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OneDriveFileDownloader.UI.ViewModels
{
	public class ViewModelBase : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		protected void RaisePropertyChanged([CallerMemberName] string name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			RaisePropertyChanged(name);
			return true;
		}
	}
}
