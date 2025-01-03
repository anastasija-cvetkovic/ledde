using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LEDDE.Library.Processors;
using Avalonia.Interactivity;
using System.IO;
using LEDDE.Library.LED;
using Avalonia.Media.Imaging;
using System;

namespace LEDDE.UI.Views
{
    public partial class MainWindow : Window
    {

        private LEDMatrix _ledMatrix;

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

                _ledMatrix = ImageProcessor.LoadImage(filePath);

                MatrixResolutionText.Text =_ledMatrix.Width+"x"+_ledMatrix.Height;
                LoadedResourceText.Text = fileName;
            }
        }
    
        private void StartSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ledMatrix == null)
            {
                StatusText.Text = "No image loaded to simulate!";
                return;
            }
            // Retrieve the new width and height from the text boxes
            if (!int.TryParse(WidthInput.Text, out int newWidth))
            {
                StatusText.Text = "Invalid width input!";
                return;
            }

            if (!int.TryParse(HeightInput.Text, out int newHeight))
            {
                StatusText.Text = "Invalid height input!";
                return;
            }

            _ledMatrix = ImageProcessor.ProcessImage(_ledMatrix,newWidth,newHeight);

            Bitmap bitmap = ToAvaloniaBitmap(_ledMatrix);

            LedDisplayView.Source = bitmap;
            MatrixResolutionText.Text = WidthInput.Text + "x" + HeightInput.Text;
            StatusText.Text = "Simulation completed!";
        }

        public Bitmap ToAvaloniaBitmap(LEDMatrix ledMatrix)
        {

            int width = ledMatrix.Width;
            int height = ledMatrix.Height;

            // Create a WriteableBitmap with the matrix dimensions
            var bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);

            

            using (var lockedBuffer = bitmap.Lock())
            {
                int bytesPerPixel = 4; // BGRA8888 format = 4 bytes per pixel
                int stride = width * bytesPerPixel;
                byte[] pixelData = new byte[height * stride];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = ledMatrix.GetPixelColor(x, y);

                        int index = ledMatrix.GetIndex(x,y) * bytesPerPixel;

                        // BGRA order for Avalonia
                        pixelData[index + 0] = color.B; // Blue
                        pixelData[index + 1] = color.G; // Green
                        pixelData[index + 2] = color.R; // Red
                        pixelData[index + 3] = color.A; // Alpha
                    }
                }

                // Copy the pixel data into the WriteableBitmap
                unsafe
                {
                    fixed (byte* srcPtr = pixelData)
                    {
                        Buffer.MemoryCopy(srcPtr, (void*)lockedBuffer.Address, pixelData.Length, pixelData.Length);
                    }
                }
            }
            return bitmap;
        }
    }
}