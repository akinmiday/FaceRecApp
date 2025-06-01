// Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Tracking;

namespace FaceRecApp
{
    class Program
    {
        // Simple struct to hold a CSRT tracker + label + current bounding box
        private struct TrackerData
        {
            public int Label;
            public TrackerCSRT Tracker;
            public Rect Bbox;

            public TrackerData(int label, TrackerCSRT tracker, Rect bbox)
            {
                Label   = label;
                Tracker = tracker;
                Bbox    = bbox;
            }
        }

        static void Main(string[] args)
        {
            // 1. Load configuration
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

            // 2. Load Haar cascade
            if (!File.Exists(cfg.CascadeFile))
            {
                Console.WriteLine($"❌ Cannot find cascade '{cfg.CascadeFile}'.");
                return;
            }
            var faceCascade = new CascadeClassifier(cfg.CascadeFile);

            // 3. Training mode?
            if (args.Length > 0 &&
                args[0].Equals("train", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("🔄 Training mode selected.");
                var trainer = new FaceTrainer(faceCascade, cfg);
                trainer.Train();
                return;
            }

            // 4. Load recognizer & labels
            FaceRecognizerService recognizer;
            try
            {
                recognizer = new FaceRecognizerService(cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {ex.Message}");
                Console.WriteLine("⚠️  Run 'dotnet run train' first.");
                return;
            }

            // 5. Open camera (index or RTSP)
            CameraService camera;
            try
            {
                camera = new CameraService(cfg.CameraSourceRaw);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {ex.Message}");
                return;
            }

            var alertService = new AlertService(cfg.AlertCooldownSeconds);
            Console.WriteLine("▶️  Press ESC in the display window to quit.");

            using var window = new Window("Face Detection & Recognition");
            var frame     = new Mat();
            var gray      = new Mat();
            var trackers  = new List<TrackerData>();
            int frameCount = 0;

            while (true)
            {
                frameCount++;
                frame = camera.GrabFrame();
                if (frame.Empty())
                    break;

                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                // 6. Update existing trackers first
                var lostIndices = new List<int>();
                for (int i = 0; i < trackers.Count; i++)
                {
                    var data = trackers[i];
                    // Prepare a Rect to receive the updated bounding box
                    Rect updatedBox = new Rect();

                    // Use 'ref updatedBox' overload
                    if (data.Tracker.Update(frame, ref updatedBox))
                    {
                        // Draw the tracked box
                        Cv2.Rectangle(frame, updatedBox, Scalar.Green, 2);

                        // Recognize on this updated rectangle
                        var (name, _, lab) = recognizer.Predict(gray, updatedBox);
                        Cv2.PutText(
                            frame,
                            name,
                            new Point(updatedBox.X, updatedBox.Y - 10),
                            HersheyFonts.HersheySimplex,
                            0.7,
                            Scalar.Chartreuse,
                            2
                        );

                        // Update stored bbox so we can later check intersection
                        data.Bbox = updatedBox;
                        trackers[i] = data; 

                        if (lab >= 0 && alertService.ShouldAlert(lab))
                            Console.WriteLine($"⚠️ Alert: {name} detected at {DateTime.Now:T}");
                    }
                    else
                    {
                        // Tracker lost its target
                        lostIndices.Add(i);
                    }
                }

                // Remove lost trackers in reverse order
                for (int i = lostIndices.Count - 1; i >= 0; i--)
                {
                    trackers.RemoveAt(lostIndices[i]);
                }

                // 7. Every cfg.FrameSkip frames, run detection + recognition
                if (frameCount % cfg.FrameSkip == 0)
                {
                    var faces = faceCascade.DetectMultiScale(
                        gray,
                        1.1,
                        5,
                        0,
                        new Size(cfg.MinFaceSize, cfg.MinFaceSize)
                    );

                    foreach (var rect in faces)
                    {
                        // Check if this rect intersects any currently tracked bbox
                        bool alreadyTracked = trackers
                            .Any(td => td.Bbox.IntersectsWith(rect));
                        if (alreadyTracked)
                            continue;

                        var (name, confidence, label) = recognizer.Predict(gray, rect);
                        if (label >= 0)
                        {
                            Cv2.Rectangle(frame, rect, Scalar.LimeGreen, 2);
                            Cv2.PutText(
                                frame,
                                name,
                                new Point(rect.X, rect.Y - 10),
                                HersheyFonts.HersheySimplex,
                                0.7,
                                Scalar.Chartreuse,
                                2
                            );

                            // Initialize a CSRT tracker with this rect
                            var tracker = TrackerCSRT.Create();
                            tracker.Init(frame, rect);
                            trackers.Add(new TrackerData(label, tracker, rect));

                            if (alertService.ShouldAlert(label))
                                Console.WriteLine($"⚠️ Alert: {name} detected at {DateTime.Now:T}");
                        }
                        else
                        {
                            Cv2.Rectangle(frame, rect, Scalar.Red, 2);
                            Cv2.PutText(
                                frame,
                                "Unknown",
                                new Point(rect.X, rect.Y - 10),
                                HersheyFonts.HersheySimplex,
                                0.7,
                                Scalar.Red,
                                2
                            );
                        }
                    }
                }

                window.ShowImage(frame);
                if (Cv2.WaitKey(1) == 27) // ESC
                    break;
            }

            Cv2.DestroyAllWindows();
            camera.Dispose();
        }
    }
}
