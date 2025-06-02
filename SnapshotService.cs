// SnapshotService.cs
using OpenCvSharp;

namespace FaceRecApp
{
    public static class SnapshotService
    {
        /// <summary>
        /// Crops an expanded rectangle (faceRect + padding) from the full-color frame,
        /// overlays the name near the top‐left of that crop, saves a PNG named
        /// {name}_{timestamp}.png in snapshotsDir, and logs to CSV.
        /// 
        /// 'paddingFactor' is the fraction by which to expand the faceRect on each side.
        /// For example, 0.2 means “add 20% of rect width on left and right, and 20% of rect height on top/bottom.”
        /// </summary>
        public static void TakeSnapshot(
            Mat fullFrame,
            Rect faceRect,
            string name,
            string snapshotsDir,
            string logFile,
            double paddingFactor = 0.2)
        {
            // 1) Compute padded rectangle
            int padX = (int)(faceRect.Width * paddingFactor);
            int padY = (int)(faceRect.Height * paddingFactor);

            // Expand faceRect by padX/padY on all sides
            var expanded = new Rect(
                faceRect.X - padX,
                faceRect.Y - padY,
                faceRect.Width + 2 * padX,
                faceRect.Height + 2 * padY
            );

            // 2) Clamp expanded rect to frame bounds
            var clamped = new Rect(
                Math.Max(expanded.X, 0),
                Math.Max(expanded.Y, 0),
                Math.Min(expanded.Width, fullFrame.Cols - Math.Max(expanded.X, 0)),
                Math.Min(expanded.Height, fullFrame.Rows - Math.Max(expanded.Y, 0))
            );

            // Make sure width/height are positive
            if (clamped.Width <= 0 || clamped.Height <= 0)
            {
                // Fallback to the original faceRect if clamped becomes invalid
                clamped = faceRect & new Rect(0, 0, fullFrame.Cols, fullFrame.Rows);
            }

            // 3) Crop that region
            Mat faceCrop;
            try
            {
                using var tmp = new Mat(fullFrame, clamped);
                faceCrop = tmp.Clone();
            }
            catch
            {
                // If cropping fails (unlikely), fallback to full frame
                faceCrop = fullFrame.Clone();
            }

            // 4) Overlay the name near the top‐left inside the crop
            Cv2.PutText(
                faceCrop,
                name,
                new Point(5, 25),                  // fixed offset from top‐left of the crop
                HersheyFonts.HersheySimplex,
                1.0,
                Scalar.Chartreuse,
                2
            );

            // 5) Generate a timestamped filename
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename  = $"{snapshotsDir}/{name}_{timestamp}.png";

            // 6) Save the crop as PNG
            Cv2.ImWrite(filename, faceCrop);

            // 7) Append a log entry: Timestamp,Name,Filename
            string logEntry = $"{timestamp},{name},{filename}\n";
            File.AppendAllText(logFile, logEntry);

            faceCrop.Dispose();
        }
    }
}
