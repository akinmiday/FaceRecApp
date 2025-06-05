// Program.cs
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using OpenCvSharp;
using OpenCvSharp.Tracking;

namespace FaceRecApp
{
    class Program
    {
        // Holds a CSRT tracker with its label and current bounding box
        private struct TrackerData
        {
            public int Label;
            public TrackerCSRT Tracker;
            public OpenCvSharp.Rect Bbox;

            public TrackerData(int label, TrackerCSRT tracker, OpenCvSharp.Rect bbox)
            {
                Label   = label;
                Tracker = tracker;
                Bbox    = bbox;
            }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "gui":
                    BuildAvaloniaApp()
                        .StartWithClassicDesktopLifetime(args);
                    break;

                case "train":
                    RunTrainerConsole();
                    break;

                case "camera":
                    RunCameraConsole();
                    break;

                default:
                    ShowUsage();
                    break;
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- gui      # Launch the Avalonia GUI");
            Console.WriteLine("  dotnet run -- train    # Train the LBPH face recognizer in console mode");
            Console.WriteLine("  dotnet run -- camera   # Run console‐only camera loop");
        }

        // Configure and return the Avalonia AppBuilder
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .UsePlatformDetect();

        // Console mode: "dotnet run -- train"
        private static void RunTrainerConsole()
        {
            Console.WriteLine("🔄 Training mode selected.");

            // 1) Load config.json
            AppConfig cfg;
            try
            {
                cfg = AppConfig.Load("config.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load config.json: {ex.Message}");
                return;
            }

            // 2) Load Haar cascade
            if (!File.Exists(cfg.CascadeFile))
            {
                Console.WriteLine($"❌ Cannot find cascade '{cfg.CascadeFile}'.");
                return;
            }
            var faceCascade = new CascadeClassifier(cfg.CascadeFile);

            // 3) Run the trainer
            var trainer = new FaceTrainer(faceCascade, cfg);
            trainer.Train();
        }

        // Console mode: "dotnet run -- camera"
        private static void RunCameraConsole()
        {
            // 1) Load config.json
            AppConfig cfg;
            try
            {
                cfg = AppConfig.Load("config.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load config.json: {ex.Message}");
                return;
            }

            // 2) Load Haar cascade
            if (!File.Exists(cfg.CascadeFile))
            {
                Console.WriteLine($"❌ Cannot find cascade '{cfg.CascadeFile}'.");
                return;
            }
            var faceCascade = new CascadeClassifier(cfg.CascadeFile);

            // 3) Load recognizer & labels
            FaceRecognizerService recognizer;
            try
            {
                recognizer = new FaceRecognizerService(cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {ex.Message}");
                Console.WriteLine("⚠️  Run 'dotnet run -- train' first.");
                return;
            }

            // 4) Open camera
            VideoCapture camera;
            if (int.TryParse(cfg.CameraSourceRaw, out var idx))
                camera = new VideoCapture(idx);
            else
                camera = new VideoCapture(cfg.CameraSourceRaw);

            if (!camera.IsOpened())
            {
                Console.WriteLine($"❌ Could not open camera '{cfg.CameraSourceRaw}'.");
                return;
            }

            var alertService = new AlertService(cfg.AlertCooldownSeconds);
            Console.WriteLine("▶️  Press ESC in the display window to quit.");

            // Ensure snapshots directory exists
            const string snapshotsDir = "snapshots";
            if (!Directory.Exists(snapshotsDir))
                Directory.CreateDirectory(snapshotsDir);

            // Prepare log CSV if missing
            const string logFile = snapshotsDir + "/log.csv";
            if (!File.Exists(logFile))
                File.WriteAllText(logFile, "Timestamp,Name,Filename\n");

            using var cvWindow = new OpenCvSharp.Window("Face Detection & Recognition");
            var frame       = new Mat();
            var gray        = new Mat();
            var trackers    = new System.Collections.Generic.List<TrackerData>();
            int frameCount  = 0;

            while (true)
            {
                frameCount++;
                camera.Read(frame);
                if (frame.Empty())
                    break;

                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                // 5) Update existing trackers first
                var lostIndices = new System.Collections.Generic.List<int>();
                for (int i = 0; i < trackers.Count; i++)
                {
                    var data       = trackers[i];
                    var updatedBox = new OpenCvSharp.Rect();

                    if (data.Tracker.Update(frame, ref updatedBox))
                    {
                        Cv2.Rectangle(frame, updatedBox, Scalar.Green, 2);
                        var (name, confidence, lab) = recognizer.Predict(gray, updatedBox);
                        Cv2.PutText(
                            frame,
                            name,
                            new OpenCvSharp.Point(updatedBox.X, updatedBox.Y - 10),
                            HersheyFonts.HersheySimplex,
                            0.7,
                            Scalar.Chartreuse,
                            2
                        );

                        data.Bbox = updatedBox;
                        trackers[i] = data;

                        if (lab >= 0 && alertService.ShouldAlert(lab))
                            Console.WriteLine($"⚠️ Alert: {name} detected at {DateTime.Now:T}");
                    }
                    else
                    {
                        lostIndices.Add(i);
                    }
                }

                for (int i = lostIndices.Count - 1; i >= 0; i--)
                    trackers.RemoveAt(lostIndices[i]);

                // 6) Every cfg.FrameSkip frames, run detection + recognition
                if (frameCount % cfg.FrameSkip == 0)
                {
                    var faces = faceCascade.DetectMultiScale(
                        gray,
                        1.1,
                        5,
                        0,
                        new OpenCvSharp.Size(cfg.MinFaceSize, cfg.MinFaceSize)
                    );

                    foreach (var rect in faces)
                    {
                        bool alreadyTracked = trackers.Any(td => td.Bbox.IntersectsWith(rect));
                        if (alreadyTracked)
                            continue;

                        var (name, confidence, label) = recognizer.Predict(gray, rect);
                        if (label >= 0)
                        {
                            Cv2.Rectangle(frame, rect, Scalar.LimeGreen, 2);
                            Cv2.PutText(
                                frame,
                                name,
                                new OpenCvSharp.Point(rect.X, rect.Y - 10),
                                HersheyFonts.HersheySimplex,
                                0.7,
                                Scalar.Chartreuse,
                                2
                            );

                            var tracker = TrackerCSRT.Create();
                            tracker.Init(frame, rect);
                            trackers.Add(new TrackerData(label, tracker, rect));

                            SnapshotService.TakeSnapshot(
                                frame,
                                rect,
                                name,
                                snapshotsDir,
                                logFile
                            );

                            if (alertService.ShouldAlert(label))
                                Console.WriteLine($"⚠️ Alert: {name} detected at {DateTime.Now:T}");
                        }
                        else
                        {
                            Cv2.Rectangle(frame, rect, Scalar.Red, 2);
                            Cv2.PutText(
                                frame,
                                "Unknown",
                                new OpenCvSharp.Point(rect.X, rect.Y - 10),
                                HersheyFonts.HersheySimplex,
                                0.7,
                                Scalar.Red,
                                2
                            );
                        }
                    }
                }

                cvWindow.ShowImage(frame);
                if (Cv2.WaitKey(1) == 27) // ESC key
                    break;
            }

            Cv2.DestroyAllWindows();
            camera.Release();
        }
    }
}
