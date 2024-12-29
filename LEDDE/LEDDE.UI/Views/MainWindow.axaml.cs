using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LEDDE.Library.Processors;
using Avalonia.Interactivity;
using LEDDE.UI.ViewModels;
using System.IO;

namespace LEDDE.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

        }
        private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            if(this.StorageProvider is null)
            {
                return;
            }
            var result = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select an Image File",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" }
                    }
                },
                AllowMultiple = false
            });
            if (result.Count > 0)
            {
                string filePath = result[0].Path.LocalPath;
                string fileName = Path.GetFileName(filePath);
                ImageProcessor.LoadImage(filePath);
                LoadedResourceText.Text = fileName;
            }
        }
    }
}