using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LEDDE.Library.Processors;
using Avalonia.Interactivity;
using System.IO;
using LEDDE.Library.LED;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Logging;

namespace LEDDE.UI.Views
{
    public partial class MainWindow : Window
    {

        private LEDMatrix? _image;
        private List<LEDMatrix>? _videoFrames;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.StorageProvider is null)
            {
                return;
            }

            var result = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select an Image or Video File",
                FileTypeFilter = new[]
                {
            new FilePickerFileType("Image and Video Files")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp","*.avi" }
            }
        },
                AllowMultiple = false
            });

            if (result.Count > 0)
            {
                ProgressBar.Value = 0; // Reset progress bar
                StatusText.Text = "Loading file...";

                string filePath = result[0].Path.LocalPath;
                string fileName = Path.GetFileName(filePath);

                await Task.Run(async () =>
                {
                    if (Path.GetExtension(filePath).Equals(".avi", StringComparison.OrdinalIgnoreCase))
                    {
                        // Start a background task to delay the status update
                        _ = Task.Delay(2000).ContinueWith(_ =>
                        {
                            // Update the status after the delay, ensuring it's on the UI thread
                            Dispatcher.UIThread.InvokeAsync(() => StatusText.Text = "Extracting frames...");
                        });

                        _videoFrames = VideoProcessor.LoadVideo(filePath, progress =>
                        {
                            // Update the progress bar on the UI thread
                            Dispatcher.UIThread.InvokeAsync(() => ProgressBar.Value = progress);
                        });

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            MatrixResolutionText.Text = _videoFrames[0].Width + "x" + _videoFrames[0].Height;
                        });

                        _image = null;
                    }
                    else
                    {
                        _image = ImageProcessor.LoadImage(filePath);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            MatrixResolutionText.Text = _image.Width + "x" + _image.Height;
                            ProgressBar.Value = 75;
                        });

                        _videoFrames = null;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusText.Text = "File loaded. Ready to simulate!";
                        LoadedResourceText.Text = fileName;
                        ProgressBar.Value = 100;
                    });
                });
            }
        }

        private async void StartSimulationButton_Click(object sender, RoutedEventArgs e)
        {
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

            if (_image == null && _videoFrames == null)
            {
                StatusText.Text = "No file to simulate!";
                return;
            }

            if (_image != null)
            {
                // Update status immediately before starting the simulation
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText.Text = "Simulating image...";
                    ProgressBar.Value = 0; // Reset progress bar
                });

                _image = ImageProcessor.ProcessImage(_image, newWidth, newHeight);

                LedDisplayView.Source = ToAvaloniaBitmap(_image);
                LedDisplayView.Width = Math.Min(newWidth, LedDisplayView.MaxWidth);
                LedDisplayView.Height = Math.Min(newHeight, LedDisplayView.MaxHeight);
                LedDisplayView.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                LedDisplayView.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;


                MatrixResolutionText.Text = $"{newWidth}x{newHeight}";

                StatusText.Text = "Image simulation completed!";
            }

            if (_videoFrames != null)
            {
                // Update status immediately before starting the scaling process
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText.Text = "Scaling video...";
                    ProgressBar.Value = 0; // Reset progress bar
                });

                // Perform the scaling operation on a background thread
                await Task.Run(() =>
                {
                    _videoFrames = VideoProcessor.ScaleVideo(_videoFrames, newWidth, newHeight, progress =>
                    {
                        // Update the progress bar on the UI thread
                        Dispatcher.UIThread.InvokeAsync(() => ProgressBar.Value = progress);
                    });
                });

                // Once scaling is done, update the status text and reset progress bar
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText.Text = "Simulating video...";
                    ProgressBar.Value = 0;  // Reset progress bar to 0
                });

                // Run the simulation asynchronously so the UI is updated without blocking
                await Task.Run(async () =>
                {
                    int totalFrames = _videoFrames.Count;
                    int currentFrame = 0;

                    foreach (var frame in _videoFrames)
                    {
                        // Convert the processed LEDMatrix frame to an Avalonia Bitmap
                        var bitmap = ToAvaloniaBitmap(frame);

                        // Update the UI in the main thread
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LedDisplayView.Source = bitmap;
                            LedDisplayView.Width = Math.Min(newWidth, LedDisplayView.MaxWidth);
                            LedDisplayView.Height = Math.Min(newHeight, LedDisplayView.MaxHeight);
                            LedDisplayView.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                            LedDisplayView.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                            MatrixResolutionText.Text = $"{newWidth}x{newHeight}";

                            // update the progress bar
                            int progress = (int)((currentFrame + 1) / (float)totalFrames * 100);
                            ProgressBar.Value = progress;  // Update the progress bar value
                        });

                        currentFrame++;

                        // Optionally, add a small delay to simulate frame rate
                        await Task.Delay(50);  // Adjust this delay to simulate frame rate
                    }

                    // Update status after the video is done
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusText.Text = "Video simulation completed!";
                    });
                });
            }
        }

        private async void ExportLEDMatrix_Click(object sender, RoutedEventArgs e)
        {
            if(_image != null)
            {
                if (this.StorageProvider == null)
                {
                    StatusText.Text = "Storage provider is not available.";
                    return;
                }
                // Step 1: Open folder picker dialog
                var folderPickerResult = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder to Save File",
                    AllowMultiple = false
                });

                if (folderPickerResult.Count == 0)
                {
                    // User canceled folder selection
                    StatusText.Text = "Export canceled.";
                    return;
                }

                string selectedFolder = folderPickerResult[0].Path.LocalPath;

                // Step 2: Generate the file name
                string fileName = $"{LoadedResourceText.Text}_{MatrixResolutionText.Text}.txt";

                // Step 3: Save the LEDMatrix object as an ASCII file
                string filePath = Path.Combine(selectedFolder, fileName);

                await Task.Run(() =>
                {
                    _image.SaveAsASCII(selectedFolder, fileName);
                });

                // Notify the user that the file has been saved
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText.Text = $"File has been exported to: {filePath}";
                });

            }
            else if(_videoFrames != null)
            {
                if (this.StorageProvider == null)
                {
                    StatusText.Text = "Storage provider is not available.";
                    return;
                }

                // Open folder picker dialog
                var folderPickerResult = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder to Save Frames",
                    AllowMultiple = false
                });

                if (folderPickerResult.Count == 0)
                {
                    // User canceled folder selection
                    StatusText.Text = "Export canceled.";
                    return;
                }

                string selectedFolder = folderPickerResult[0].Path.LocalPath;

                // Create a folder with the video's name to store frames
                string videoFolderName = LoadedResourceText.Text+"_"+MatrixResolutionText.Text;
                string framesFolder = Path.Combine(selectedFolder, videoFolderName);

                try
                {
                    // Ensure the folder exists
                    Directory.CreateDirectory(framesFolder);

                    // Save each frame as ASCII in parallel
                    await Task.Run(async () =>
                    {
                        var tasks = new List<Task>();
                        for (int i = 0; i < _videoFrames.Count; i++)
                        {
                            int frameIndex = i; // Avoid closure issues
                            tasks.Add(Task.Run(() =>
                            {
                                string frameFileName = $"Frame_{frameIndex + 1}.txt";
                                _videoFrames[frameIndex].SaveAsASCII(framesFolder, frameFileName);
                            }));
                        }

                        await Task.WhenAll(tasks);
                    });

                    StatusText.Text = $"Frames have been exported to: {framesFolder}";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error exporting frames: {ex.Message}";
                }
            }

        }
        
        #region helper methods
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

                unsafe
                {
                    byte* buffer = (byte*)lockedBuffer.Address;

                    // Parallelize row processing
                    Parallel.For(0, height, y =>
                    {
                        // Pointer to the start of the row in the buffer
                        byte* rowPtr = buffer + y * stride;

                        for (int x = 0; x < width; x++)
                        {
                            var color = ledMatrix.GetPixelColor(x, y);

                            // Calculate the offset for this pixel (BGRA order)
                            int index = x * bytesPerPixel;
                            rowPtr[index + 0] = color.B; // Blue
                            rowPtr[index + 1] = color.G; // Green
                            rowPtr[index + 2] = color.R; // Red
                            rowPtr[index + 3] = color.A; // Alpha
                        }
                    });
                }
            }

            return bitmap;
        }
        public Bitmap ToAvaloniaBitmap_WITHPIXELSIZE(LEDMatrix ledMatrix, int pixelSize = 20)
        {

            int width = ledMatrix.Width * pixelSize;
            int height = ledMatrix.Height * pixelSize;

            // Create a WriteableBitmap with the scaled dimensions
            var bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);

            using (var lockedBuffer = bitmap.Lock())
            {
                int bytesPerPixel = 4; // BGRA8888 format = 4 bytes per pixel
                int stride = width * bytesPerPixel;

                unsafe
                {
                    byte* buffer = (byte*)lockedBuffer.Address;

                    // Parallelize the loop over rows
                    Parallel.For(0, ledMatrix.Height, y =>
                    {
                        for (int x = 0; x < ledMatrix.Width; x++)
                        {
                            var color = ledMatrix.GetPixelColor(x, y);

                            // Fill the larger "pixel block"
                            for (int py = 0; py < pixelSize; py++)
                            {
                                int pixelRowStart = ((y * pixelSize) + py) * stride;
                                for (int px = 0; px < pixelSize; px++)
                                {
                                    int pixelX = (x * pixelSize + px) * bytesPerPixel;
                                    int index = pixelRowStart + pixelX;

                                    buffer[index + 0] = color.B; // Blue
                                    buffer[index + 1] = color.G; // Green
                                    buffer[index + 2] = color.R; // Red
                                    buffer[index + 3] = color.A; // Alpha
                                }
                            }
                        }
                    });
                }
            }
            return bitmap;
        }
    
        private async void ChangeTheme_Click(object sender, RoutedEventArgs e)
        {
            
        }
        #endregion
    }
}