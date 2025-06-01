// FaceRecognizerService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenCvSharp;
using OpenCvSharp.Face;

namespace FaceRecApp
{
    public class FaceRecognizerService
    {
        private readonly LBPHFaceRecognizer      _recognizer;
        private readonly Dictionary<int, string> _idToName;
        private readonly int                     _threshold;

        public FaceRecognizerService(AppConfig cfg)
        {
            _threshold = cfg.RecognitionThreshold;

            if (!File.Exists(cfg.ModelFile) || !File.Exists(cfg.LabelsFile))
                throw new FileNotFoundException("Model file or labels file missing; run training first.");

            // Load the LBPH model
            _recognizer = LBPHFaceRecognizer.Create();
            _recognizer.Read(cfg.ModelFile);
            Console.WriteLine($"✅ Loaded recognizer model '{cfg.ModelFile}'.");

            // Load labels.json
            var json = File.ReadAllText(cfg.LabelsFile);
            var labelsFile = JsonSerializer.Deserialize<FaceTrainer.LabelsFile>(json)
                             ?? throw new Exception($"Failed to parse '{cfg.LabelsFile}'.");
            _idToName = labelsFile.Labels.ToDictionary(le => le.Id, le => le.Name);
        }

        /// <summary>
        /// Given a grayscale Mat and a face rectangle, returns (matchedName, confidence, label).
        /// If confidence >= threshold, returns ("Unknown", confidence, -1).
        /// If faceRect lies outside the image, it is first clipped. If the clipped region is invalid,
        /// immediately returns ("Unknown", Double.MaxValue, -1).
        /// </summary>
        public (string name, double confidence, int label) Predict(Mat grayFrame, Rect faceRect)
        {
            // 1. Clip the faceRect to lie within [0..cols) x [0..rows)
            var frameWidth  = grayFrame.Cols;
            var frameHeight = grayFrame.Rows;

            // Compute intersection with the full image bounds
            var imageBounds = new Rect(0, 0, frameWidth, frameHeight);
            var clippedRect = faceRect & imageBounds;

            // If the clipped rectangle is empty or too small, skip recognition
            if (clippedRect.Width <= 0 || clippedRect.Height <= 0)
            {
                return ("Unknown", Double.MaxValue, -1);
            }

            // 2. Crop & resize to 200×200
            Mat faceROI;
            try
            {
                using var tmp = new Mat(grayFrame, clippedRect);
                faceROI = tmp.Resize(new Size(200, 200));
            }
            catch (OpenCvSharp.OpenCVException)
            {
                // In the unlikely event clip still fails, fall back to unknown
                return ("Unknown", Double.MaxValue, -1);
            }

            // 3. Run LBPH prediction
            _recognizer.Predict(faceROI, out int label, out double confidence);

            // 4. Decide based on confidence
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
