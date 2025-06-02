// SnapshotService.cs
using System;
using System.IO;
using OpenCvSharp;

namespace FaceRecApp
{
    public static class SnapshotService
    {
        /// <summary>
        /// Crops the given rect from the full-color frame, overlays the name,
        /// saves a PNG named {name}_{timestamp}.png in snapshotsDir, and logs to CSV.
        /// </summary>
        public static void TakeSnapshot(
            Mat fullFrame,
            Rect faceRect,
            string name,
            string snapshotsDir,
            string logFile)
        {
            // 1) Clip faceRect to frame bounds
            var clamped = new Rect(
                Math.Max(faceRect.X, 0),
                Math.Max(faceRect.Y, 0),
                Math.Min(faceRect.Width, fullFrame.Cols - faceRect.X),
                Math.Min(faceRect.Height, fullFrame.Rows - faceRect.Y)
            );

            Mat faceCrop;
            try
            {
                using var tmp = new Mat(fullFrame, clamped);
                faceCrop = tmp.Clone();
            }
            catch
            {
                // If cropping fails, fall back to saving the entire frame
                faceCrop = fullFrame.Clone();
            }

            // 2) Overlay the name at the top-left of the crop
            Cv2.PutText(
                faceCrop,
                name,
                new Point(5, 25),
                HersheyFonts.HersheySimplex,
                1.0,
                Scalar.Chartreuse,
                2
            );

            // 3) Generate a timestamped filename
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename  = $"{snapshotsDir}/{name}_{timestamp}.png";

            // 4) Save the crop as PNG
            Cv2.ImWrite(filename, faceCrop);

            // 5) Append a log entry: Timestamp,Name,Filename
            string logEntry = $"{timestamp},{name},{filename}\n";
            File.AppendAllText(logFile, logEntry);

            faceCrop.Dispose();
        }
    }
}
