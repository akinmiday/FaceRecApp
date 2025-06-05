// // MainWindow.xaml.cs
// using Avalonia.Controls;
// using Avalonia.Markup.Xaml;
// using Avalonia.Interactivity;
// using Avalonia.Platform.Storage;
// using Avalonia.Threading;
// using Avalonia.Media.Imaging;
// using OpenCvSharp;

// namespace FaceRecApp
// {
//     public partial class MainWindow : Avalonia.Controls.Window
//     {
//         private readonly AppConfig            _cfg = null!;
//         private readonly string               _dataRoot = null!;
//         private VideoCapture?                 _cameraCapture;
//         private CancellationTokenSource?      _cancellation;
//         private CascadeClassifier             _faceCascade = null!;
//         private FaceRecognizerService         _recognizer    = null!;
//         private AlertService                  _alertService  = null!;
//         private DispatcherTimer               _alertTimer    = null!;

//         public MainWindow()
//         {
//             InitializeComponent();

//             // 1) Load config.json
//             try
//             {
//                 _cfg = AppConfig.Load("config.json");
//             }
//             catch (Exception ex)
//             {
//                 AddStatusLabel.Text   = $"Error loading config.json: {ex.Message}";
//                 TrainStatusLabel.Text = $"Error loading config.json: {ex.Message}";
//                 return;
//             }

//             // 2) Ensure data folder exists
//             _dataRoot = _cfg.DataFolder;
//             if (!Directory.Exists(_dataRoot))
//                 Directory.CreateDirectory(_dataRoot);

//             // 3) Load Haar cascade for detection
//             if (!File.Exists(_cfg.CascadeFile))
//             {
//                 AddStatusLabel.Text   = $"Cannot find cascade '{_cfg.CascadeFile}'.";
//                 TrainStatusLabel.Text = $"Cannot find cascade '{_cfg.CascadeFile}'.";
//                 return;
//             }
//             _faceCascade = new CascadeClassifier(_cfg.CascadeFile);

//             // 4) Load recognizer & labels (will throw if not trained)
//             try
//             {
//                 _recognizer = new FaceRecognizerService(_cfg);
//             }
//             catch (Exception ex)
//             {
//                 AddStatusLabel.Text   = $"Recognizer load error: {ex.Message}";
//                 TrainStatusLabel.Text = $"Recognizer load error: {ex.Message}";
//                 return;
//             }

//             _alertService = new AlertService(_cfg.AlertCooldownSeconds);

//             // 5) Populate “Add Person” page
//             LoadPersons();

//             // 6) Enable “Add Images” only when a folder is selected
//             PersonsList.SelectionChanged += (_, __) =>
//             {
//                 AddImagesBtn.IsEnabled = PersonsList.SelectedItem != null;
//             };

//             // 7) Hook up “Add Person” page buttons
//             AddPersonBtn.Click   += AddPersonBtn_Click;
//             AddImagesBtn.Click   += AddImagesBtn_Click;

//             // 8) Hook up “Train” page button
//             TrainBtnTab.Click    += TrainBtnTab_Click;

//             // 9) Prepare alert timer (3 seconds)
//             _alertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
//             _alertTimer.Tick += (s, e) =>
//             {
//                 AlertOverlay.IsVisible = false;
//                 _alertTimer.Stop();
//             };

//             // 10) Start camera preview + detection/recognition
//             StartCameraPreview();
//         }

//         private void InitializeComponent()
//         {
//             AvaloniaXamlLoader.Load(this);
//         }

//         // ==================== “Add Person” Page ====================

//         private void LoadPersons()
//         {
//             var folderNames = Directory
//                 .GetDirectories(_dataRoot)
//                 .Select(Path.GetFileName)
//                 .Where(name => !string.IsNullOrWhiteSpace(name))
//                 .OrderBy(n => n)
//                 .ToList();

//             PersonsList.ItemsSource = folderNames;
//             AddStatusLabel.Text     = $"Status: Loaded {folderNames.Count} person(s)";
//         }

//         private void AddPersonBtn_Click(object? sender, RoutedEventArgs e)
//         {
//             var name = NewPersonBox.Text?.Trim();
//             if (string.IsNullOrWhiteSpace(name))
//             {
//                 AddStatusLabel.Text = "Enter a non‐empty name.";
//                 return;
//             }

//             var personDir = Path.Combine(_dataRoot, name);
//             if (Directory.Exists(personDir))
//             {
//                 AddStatusLabel.Text = $"Person '{name}' already exists.";
//                 return;
//             }

//             try
//             {
//                 Directory.CreateDirectory(personDir);
//                 LoadPersons();
//                 NewPersonBox.Text = "";
//                 AddStatusLabel.Text = $"Created folder for '{name}'.";
//             }
//             catch (Exception ex)
//             {
//                 AddStatusLabel.Text = $"Failed to create folder: {ex.Message}";
//             }
//         }

//         private async void AddImagesBtn_Click(object? sender, RoutedEventArgs e)
//         {
//             if (PersonsList.SelectedItem is not string selectedPerson)
//                 return;

//             var options = new FilePickerOpenOptions
//             {
//                 Title = $"Select images for '{selectedPerson}'",
//                 AllowMultiple = true,
//                 FileTypeFilter = new List<FilePickerFileType>
//                 {
//                     new FilePickerFileType("Images")
//                     {
//                         Patterns = new[] { "*.jpg", "*.png" }
//                     }
//                 }
//             };

//             var storageFiles = await this.StorageProvider.OpenFilePickerAsync(options);
//             if (storageFiles == null || storageFiles.Count == 0)
//             {
//                 AddStatusLabel.Text = "No files selected.";
//                 return;
//             }

//             var personDir = Path.Combine(_dataRoot, selectedPerson);
//             int copied = 0;

//             foreach (var storageFile in storageFiles)
//             {
//                 string? srcPath = storageFile.TryGetLocalPath();
//                 if (srcPath == null)
//                     continue;

//                 try
//                 {
//                     var filename = Path.GetFileName(srcPath);
//                     var destPath = Path.Combine(personDir, filename);

//                     if (File.Exists(destPath))
//                     {
//                         var nameOnly = Path.GetFileNameWithoutExtension(filename);
//                         var ext      = Path.GetExtension(filename);
//                         var newName  = $"{nameOnly}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
//                         destPath     = Path.Combine(personDir, newName);
//                     }

//                     File.Copy(srcPath, destPath);
//                     copied++;
//                 }
//                 catch
//                 {
//                     // ignore individual copy errors
//                 }
//             }

//             AddStatusLabel.Text = $"Copied {copied} image(s) into '{selectedPerson}'.";
//         }

//         // ==================== “Train” Page ====================

//         private void TrainBtnTab_Click(object? sender, RoutedEventArgs e)
//         {
//             TrainBtnTab.IsEnabled = false;
//             TrainStatusLabel.Text = "Training in progress…";

//             Task.Run(() =>
//             {
//                 try
//                 {
//                     var faceCascade = new CascadeClassifier(_cfg.CascadeFile);
//                     var trainer     = new FaceTrainer(faceCascade, _cfg);
//                     trainer.Train();

//                     // Reload recognizer for the live stream
//                     _recognizer = new FaceRecognizerService(_cfg);

//                     Dispatcher.UIThread.InvokeAsync(() =>
//                     {
//                         TrainStatusLabel.Text = "Training completed successfully.";
//                     });
//                 }
//                 catch (Exception ex)
//                 {
//                     Dispatcher.UIThread.InvokeAsync(() =>
//                     {
//                         TrainStatusLabel.Text = $"Training failed: {ex.Message}";
//                     });
//                 }
//                 finally
//                 {
//                     Dispatcher.UIThread.InvokeAsync(() =>
//                     {
//                         TrainBtnTab.IsEnabled = true;
//                     });
//                 }
//             });
//         }

//         // ==================== “Live Stream” Page ====================

//         private void StartCameraPreview()
//         {
//             _cancellation = new CancellationTokenSource();

//             // Open camera by index if numeric, else by path/URL
//             if (int.TryParse(_cfg.CameraSourceRaw, out var idx))
//                 _cameraCapture = new VideoCapture(idx);
//             else
//                 _cameraCapture = new VideoCapture(_cfg.CameraSourceRaw);

//             if (_cameraCapture == null || !_cameraCapture.IsOpened())
//             {
//                 _ = Dispatcher.UIThread.InvokeAsync(() =>
//                 {
//                     AlertOverlay.IsVisible = true;
//                     AlertBanner.Text = $"❌ Could not open camera.";
//                 });
//                 return;
//             }

//             Task.Run(async () =>
//             {
//                 var token = _cancellation.Token;
//                 using var colorFrame = new Mat();
//                 using var grayFrame  = new Mat();

//                 while (!token.IsCancellationRequested)
//                 {
//                     // 1) Grab BGR frame
//                     _cameraCapture.Read(colorFrame);
//                     if (colorFrame.Empty())
//                     {
//                         await Task.Delay(30, token);
//                         continue;
//                     }

//                     // 2) Grayscale + equalize
//                     Cv2.CvtColor(colorFrame, grayFrame, ColorConversionCodes.BGR2GRAY);
//                     Cv2.EqualizeHist(grayFrame, grayFrame);

//                     // 3) Detect faces
//                     var faces = _faceCascade.DetectMultiScale(
//                         grayFrame,
//                         1.1,
//                         5,
//                         0,
//                         new OpenCvSharp.Size(_cfg.MinFaceSize, _cfg.MinFaceSize)
//                     );

//                     // 4) For each face: recognize & draw
//                     foreach (var rect in faces)
//                     {
//                         var (name, confidence, label) = _recognizer.Predict(grayFrame, rect);

//                         var rectColor = label >= 0 ? Scalar.LimeGreen : Scalar.Red;
//                         var textColor = label >= 0 ? Scalar.Chartreuse : Scalar.Red;

//                         Cv2.Rectangle(colorFrame, rect, rectColor, 2);
//                         Cv2.PutText(
//                             colorFrame,
//                             name,
//                             new OpenCvSharp.Point(rect.X, rect.Y - 10),
//                             HersheyFonts.HersheySimplex,
//                             0.7,
//                             textColor,
//                             2
//                         );

//                         // 5) If recognized and not in cooldown, show alert overlay
//                         if (label >= 0 && _alertService.ShouldAlert(label))
//                         {
//                             _ = Dispatcher.UIThread.InvokeAsync(() =>
//                             {
//                                 AlertBanner.Text = $"⚠️ Alert: {name} detected!";
//                                 AlertOverlay.IsVisible = true;
//                                 _alertTimer.Start();
//                             });
//                         }
//                     }

//                     // 6) Convert annotated BGR → RGB → JPEG → Bitmap
//                     colorFrame.ConvertTo(colorFrame, MatType.CV_8UC3);
//                     Cv2.CvtColor(colorFrame, colorFrame, ColorConversionCodes.BGR2RGB);
//                     var jpegData = colorFrame.ToBytes(".jpg");
//                     using var ms = new MemoryStream(jpegData);
//                     var avaloniaBitmap = new Bitmap(ms);

//                     // 7) Update VideoView on UI thread
//                     await Dispatcher.UIThread.InvokeAsync(() =>
//                     {
//                         VideoView.Source = avaloniaBitmap;
//                     });

//                     await Task.Delay(33, token);
//                 }
//             });
//         }

//         private void StopCameraPreview()
//         {
//             _cancellation?.Cancel();
//             _cameraCapture?.Release();
//             _cameraCapture?.Dispose();
//             _cameraCapture = null;
//         }

//         protected override void OnClosing(WindowClosingEventArgs e)
//         {
//             base.OnClosing(e);
//             StopCameraPreview();
//         }
//     }
// }




// MainWindow.xaml.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FaceRecApp
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private readonly AppConfig            _cfg = null!;
        private readonly string               _dataRoot = null!;
        private VideoCapture?                 _cameraCapture;
        private CancellationTokenSource?      _cancellation;
        private CascadeClassifier             _faceCascade = null!;
        private FaceRecognizerService         _recognizer    = null!;
        private AlertService                  _alertService  = null!;
        private DispatcherTimer               _alertTimer    = null!;

        public MainWindow()
        {
            InitializeComponent();

            // 1) Load config.json
            try
            {
                _cfg = AppConfig.Load("config.json");
            }
            catch (Exception ex)
            {
                var addStatus = this.FindControl<TextBlock>("AddStatusLabel");
                var trainStatus = this.FindControl<TextBlock>("TrainStatusLabel");
                if (addStatus != null)
                    addStatus.Text = $"Error loading config.json: {ex.Message}";
                if (trainStatus != null)
                    trainStatus.Text = $"Error loading config.json: {ex.Message}";
                return;
            }

            // 2) Ensure data folder exists
            _dataRoot = _cfg.DataFolder;
            if (!Directory.Exists(_dataRoot))
                Directory.CreateDirectory(_dataRoot);

            // 3) Load Haar cascade for detection
            if (!File.Exists(_cfg.CascadeFile))
            {
                var addStatus = this.FindControl<TextBlock>("AddStatusLabel");
                var trainStatus = this.FindControl<TextBlock>("TrainStatusLabel");
                if (addStatus != null)
                    addStatus.Text = $"Cannot find cascade '{_cfg.CascadeFile}'.";
                if (trainStatus != null)
                    trainStatus.Text = $"Cannot find cascade '{_cfg.CascadeFile}'.";
                return;
            }
            _faceCascade = new CascadeClassifier(_cfg.CascadeFile);

            // 4) Load recognizer & labels (will throw if not trained)
            try
            {
                _recognizer = new FaceRecognizerService(_cfg);
            }
            catch (Exception ex)
            {
                var addStatus = this.FindControl<TextBlock>("AddStatusLabel");
                var trainStatus = this.FindControl<TextBlock>("TrainStatusLabel");
                if (addStatus != null)
                    addStatus.Text = $"Recognizer load error: {ex.Message}";
                if (trainStatus != null)
                    trainStatus.Text = $"Recognizer load error: {ex.Message}";
                return;
            }

            _alertService = new AlertService(_cfg.AlertCooldownSeconds);

            // 5) Populate “Add Person” page
            LoadPersons();

            // 6) Enable “Add Images” only when a folder is selected
            var personsList = this.FindControl<ListBox>("PersonsList");
            var addImagesBtn = this.FindControl<Button>("AddImagesBtn");
            if (personsList != null && addImagesBtn != null)
            {
                personsList.SelectionChanged += (_, __) =>
                {
                    addImagesBtn.IsEnabled = personsList.SelectedItem != null;
                };
            }

            // 7) Hook up “Add Person” page buttons
            var addPersonBtn = this.FindControl<Button>("AddPersonBtn");
            if (addPersonBtn != null)
                addPersonBtn.Click += AddPersonBtn_Click;

            if (addImagesBtn != null)
                addImagesBtn.Click += AddImagesBtn_Click;

            // 8) Hook up “Train” page button
            var trainBtnTab = this.FindControl<Button>("TrainBtnTab");
            if (trainBtnTab != null)
                trainBtnTab.Click += TrainBtnTab_Click;

            // 9) Prepare alert timer (3 seconds)
            _alertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _alertTimer.Tick += (s, e) =>
            {
                var overlay = this.FindControl<Border>("AlertOverlay");
                if (overlay != null)
                    overlay.IsVisible = false;
                _alertTimer.Stop();
            };

            // 10) Start camera preview + detection/recognition
            StartCameraPreview();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ==================== “Add Person” Page ====================

        private void LoadPersons()
        {
            var personsList = this.FindControl<ListBox>("PersonsList");
            var addStatus = this.FindControl<TextBlock>("AddStatusLabel");
            if (personsList == null || addStatus == null)
                return;

            var folderNames = Directory
                .GetDirectories(_dataRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(n => n)
                .ToList();

            personsList.ItemsSource = folderNames;
            addStatus.Text = $"Status: Loaded {folderNames.Count} person(s)";
        }

        private void AddPersonBtn_Click(object? sender, RoutedEventArgs e)
        {
            var personsList = this.FindControl<ListBox>("PersonsList");
            var newPersonBox = this.FindControl<TextBox>("NewPersonBox");
            var addStatus = this.FindControl<TextBlock>("AddStatusLabel");
            if (personsList == null || newPersonBox == null || addStatus == null)
                return;

            var name = newPersonBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                addStatus.Text = "Enter a non‐empty name.";
                return;
            }

            var personDir = Path.Combine(_dataRoot, name);
            if (Directory.Exists(personDir))
            {
                addStatus.Text = $"Person '{name}' already exists.";
                return;
            }

            try
            {
                Directory.CreateDirectory(personDir);
                LoadPersons();
                newPersonBox.Text = "";
                addStatus.Text = $"Created folder for '{name}'.";
            }
            catch (Exception ex)
            {
                addStatus.Text = $"Failed to create folder: {ex.Message}";
            }
        }

        private async void AddImagesBtn_Click(object? sender, RoutedEventArgs e)
        {
            var personsList = this.FindControl<ListBox>("PersonsList");
            var addStatus = this.FindControl<TextBlock>("AddStatusLabel");
            if (personsList == null || addStatus == null)
                return;

            if (personsList.SelectedItem is not string selectedPerson)
                return;

            var options = new FilePickerOpenOptions
            {
                Title = $"Select images for '{selectedPerson}'",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.jpg", "*.png" }
                    }
                }
            };

            var storageFiles = await this.StorageProvider.OpenFilePickerAsync(options);
            if (storageFiles == null || storageFiles.Count == 0)
            {
                addStatus.Text = "No files selected.";
                return;
            }

            var personDir = Path.Combine(_dataRoot, selectedPerson);
            int copied = 0;

            foreach (var storageFile in storageFiles)
            {
                string? srcPath = storageFile.TryGetLocalPath();
                if (srcPath == null)
                    continue;

                try
                {
                    var filename = Path.GetFileName(srcPath);
                    var destPath = Path.Combine(personDir, filename);

                    if (File.Exists(destPath))
                    {
                        var nameOnly = Path.GetFileNameWithoutExtension(filename);
                        var ext      = Path.GetExtension(filename);
                        var newName  = $"{nameOnly}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                        destPath     = Path.Combine(personDir, newName);
                    }

                    File.Copy(srcPath, destPath);
                    copied++;
                }
                catch
                {
                    // ignore individual copy errors
                }
            }

            addStatus.Text = $"Copied {copied} image(s) into '{selectedPerson}'.";
        }

        // ==================== “Train” Page ====================

        private void TrainBtnTab_Click(object? sender, RoutedEventArgs e)
        {
            var trainBtnTab = this.FindControl<Button>("TrainBtnTab");
            var trainStatus = this.FindControl<TextBlock>("TrainStatusLabel");
            if (trainBtnTab == null || trainStatus == null)
                return;

            trainBtnTab.IsEnabled = false;
            trainStatus.Text = "Training in progress…";

            Task.Run(() =>
            {
                try
                {
                    var faceCascade = new CascadeClassifier(_cfg.CascadeFile);
                    var trainer = new FaceTrainer(faceCascade, _cfg);
                    trainer.Train();

                    _recognizer = new FaceRecognizerService(_cfg);

                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        trainStatus.Text = "Training completed successfully.";
                    });
                }
                catch (Exception ex)
                {
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        trainStatus.Text = $"Training failed: {ex.Message}";
                    });
                }
                finally
                {
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        trainBtnTab.IsEnabled = true;
                    });
                }
            });
        }

        // ==================== “Live Stream” Page ====================

        private void StartCameraPreview()
        {
            _cancellation = new CancellationTokenSource();

            if (int.TryParse(_cfg.CameraSourceRaw, out var idx))
                _cameraCapture = new VideoCapture(idx);
            else
                _cameraCapture = new VideoCapture(_cfg.CameraSourceRaw);

            if (_cameraCapture == null || !_cameraCapture.IsOpened())
            {
                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var alertOverlay = this.FindControl<Border>("AlertOverlay");
                    var alertBanner = this.FindControl<TextBlock>("AlertBanner");
                    if (alertOverlay != null && alertBanner != null)
                    {
                        alertBanner.Text = $"❌ Could not open camera.";
                        alertOverlay.IsVisible = true;
                    }
                });
                return;
            }

            Task.Run(async () =>
            {
                var token = _cancellation.Token;
                using var colorFrame = new Mat();
                using var grayFrame  = new Mat();

                while (!token.IsCancellationRequested)
                {
                    _cameraCapture.Read(colorFrame);
                    if (colorFrame.Empty())
                    {
                        await Task.Delay(30, token);
                        continue;
                    }

                    Cv2.CvtColor(colorFrame, grayFrame, ColorConversionCodes.BGR2GRAY);
                    Cv2.EqualizeHist(grayFrame, grayFrame);

                    var faces = _faceCascade.DetectMultiScale(
                        grayFrame,
                        1.1,
                        5,
                        0,
                        new OpenCvSharp.Size(_cfg.MinFaceSize, _cfg.MinFaceSize)
                    );

                    foreach (var rect in faces)
                    {
                        var (name, confidence, label) = _recognizer.Predict(grayFrame, rect);

                        var rectColor = label >= 0 ? Scalar.LimeGreen : Scalar.Red;
                        var textColor = label >= 0 ? Scalar.Chartreuse : Scalar.Red;

                        Cv2.Rectangle(colorFrame, rect, rectColor, 2);
                        Cv2.PutText(
                            colorFrame,
                            name,
                            new OpenCvSharp.Point(rect.X, rect.Y - 10),
                            HersheyFonts.HersheySimplex,
                            0.7,
                            textColor,
                            2
                        );

                        if (label >= 0 && _alertService.ShouldAlert(label))
                        {
                            _ = Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                var alertOverlay = this.FindControl<Border>("AlertOverlay");
                                var alertBanner = this.FindControl<TextBlock>("AlertBanner");
                                if (alertOverlay != null && alertBanner != null)
                                {
                                    alertBanner.Text = $"⚠️ Alert: {name} detected!";
                                    alertOverlay.IsVisible = true;
                                    _alertTimer.Start();
                                }
                            });
                        }
                    }

                    colorFrame.ConvertTo(colorFrame, MatType.CV_8UC3);
                    Cv2.CvtColor(colorFrame, colorFrame, ColorConversionCodes.BGR2RGB);
                    var jpegData = colorFrame.ToBytes(".jpg");
                    using var ms = new MemoryStream(jpegData);
                    var avaloniaBitmap = new Bitmap(ms);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var videoView = this.FindControl<Image>("VideoView");
                        if (videoView != null)
                            videoView.Source = avaloniaBitmap;
                    });

                    await Task.Delay(33, token);
                }
            });
        }

        private void StopCameraPreview()
        {
            _cancellation?.Cancel();
            _cameraCapture?.Release();
            _cameraCapture?.Dispose();
            _cameraCapture = null;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            StopCameraPreview();
        }
    }
}
