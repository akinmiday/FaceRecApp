// FaceRecognizerService.cs
using System.Text.Json;
using OpenCvSharp;
using OpenCvSharp.Face;

namespace FaceRecApp
{
    public class FaceRecognizerService
    {
        private readonly FaceRecognizer        _recognizer;
        private readonly Dictionary<int, string> _idToName;
        private readonly int                     _threshold;

        public FaceRecognizerService(AppConfig cfg)
        {
            _threshold = cfg.RecognitionThreshold;

            if (!File.Exists(cfg.ModelFile) || !File.Exists(cfg.LabelsFile))
                throw new FileNotFoundException("Model file or labels file missing; run training first.");

            _recognizer = LBPHFaceRecognizer.Create();
            _recognizer.Read(cfg.ModelFile);
            Console.WriteLine($"✅ Loaded recognizer model '{cfg.ModelFile}'.");

            var json = File.ReadAllText(cfg.LabelsFile);
            var labelsFile = JsonSerializer.Deserialize<FaceTrainer.LabelsFile>(json)
                             ?? throw new Exception($"Failed to parse '{cfg.LabelsFile}'.");
            _idToName = labelsFile.Labels.ToDictionary(le => le.Id, le => le.Name);
        }

        public (string name, double confidence, int label) Predict(Mat grayFrame, OpenCvSharp.Rect faceRect)
        {
            var frameWidth  = grayFrame.Cols;
            var frameHeight = grayFrame.Rows;
            var imageBounds = new OpenCvSharp.Rect(0, 0, frameWidth, frameHeight);
            var clippedRect = faceRect & imageBounds;
            if (clippedRect.Width <= 0 || clippedRect.Height <= 0)
                return ("Unknown", Double.MaxValue, -1);

            Mat faceROI;
            try
            {
                using var tmp = new Mat(grayFrame, clippedRect);
                faceROI = tmp.Resize(new OpenCvSharp.Size(200, 200));
                Cv2.EqualizeHist(faceROI, faceROI);
            }
            catch (OpenCvSharp.OpenCVException)
            {
                return ("Unknown", Double.MaxValue, -1);
            }

            _recognizer.Predict(faceROI, out int label, out double confidence);

            if (confidence < _threshold && _idToName.ContainsKey(label))
            {
                return (_idToName[label], confidence, label);
            }
            else
            {
                return ("Unknown", confidence, -1);
            }
        }
    }
}
