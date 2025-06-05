// CameraService.cs
using OpenCvSharp;

namespace FaceRecApp
{
    public class CameraService : IDisposable
    {
        private readonly VideoCapture _capture;

        public CameraService(string source)
        {
            if (int.TryParse(source, out int idx))
                _capture = new VideoCapture(idx);
            else
                _capture = new VideoCapture(source);

            if (!_capture.IsOpened())
                throw new Exception($"Could not open camera source '{source}'.");
        }

        public Mat GrabFrame()
        {
            var frame = new Mat();
            _capture.Read(frame);
            return frame;
        }

        public void Dispose()
        {
            _capture.Release();
            _capture.Dispose();
        }
    }
}
