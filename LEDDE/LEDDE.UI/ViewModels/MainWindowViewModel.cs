using LEDDE.Library;
using LEDDE.Library.Processors;
using Avalonia.Controls;
using ReactiveUI;
using System.Windows.Input;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;

namespace LEDDE.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        //public string Greeting { get; } = "Welcome to Avalonia!";

        public ICommand LoadFileCommand { get; }
        public Window? ParentWindow { get; set;  }

        public MainWindowViewModel()
        {

            //LoadFileCommand = ReactiveCommand.Create(LoadFile);
        }

      /*  private async Task LoadFile()
        {
            if(ParentWindow?.StorageProvider is null)
            {
                return;
            }

            var result = await ParentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select an Image File",
                FileTypeFilter = new[] {
                    new FilePickerFileType("ImageFiles")
                    {
                    Patterns = new[] {"*.png","*.jpg", "*.jpeg", "*.bmp" }
                    }
                },
                AllowMultiple = false
            });

            if (result.Count > 0)
            {
                string filePath = result[0].Path.LocalPath;
                ImageProcessor.LoadImage(filePath);
            }
            //var dialog = new OpenFileDialog();

            //string filePath = "";
           //ImageProcessor.LoadImage(filePath);
        }*/
    }
}
