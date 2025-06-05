// SnapshotService.cs
using System.Globalization;
using OpenCvSharp;

namespace FaceRecApp
{
    public static class SnapshotService
    {
        public static void TakeSnapshot(Mat frame, OpenCvSharp.Rect faceRect, string name, string snapshotsDir, string logFile)
        {
            try
            {
                var frameWidth  = frame.Cols;
                var frameHeight = frame.Rows;
                var imageBounds = new OpenCvSharp.Rect(0, 0, frameWidth, frameHeight);
                var clippedRect = faceRect & imageBounds;
                if (clippedRect.Width <= 0 || clippedRect.Height <= 0)
                    return;

                using var faceROI = new Mat(frame, clippedRect);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture);
                var filename  = $"{name}_{timestamp}.png";
                var filepath  = Path.Combine(snapshotsDir, filename);

                Cv2.ImWrite(filepath, faceROI);

                var csvLine = $"{DateTime.Now:O},{name},{filename}";
                File.AppendAllText(logFile, csvLine + Environment.NewLine);
            }
            catch
            {
                // Errors here should not crash the loop
            }
        }
    }
}
