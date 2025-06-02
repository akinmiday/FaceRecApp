using System.Text.Json;
using OpenCvSharp;
using OpenCvSharp.Face;

namespace FaceRecApp
{
    public class FaceTrainer
    {
        private readonly CascadeClassifier _cascade;
        private readonly AppConfig        _cfg;

        public FaceTrainer(CascadeClassifier cascade, AppConfig cfg)
        {
            _cascade = cascade;
            _cfg     = cfg;
        }

        public void Train()
        {
            var faceMats    = new List<Mat>();
            var labels      = new List<int>();
            var labelToName = new List<(int id, string name)>();

            var dirs = Directory.GetDirectories(_cfg.DataFolder);
            if (dirs.Length == 0)
            {
                Console.WriteLine($"❌ No subfolders found in '{_cfg.DataFolder}'.");
                return;
            }

            int labelIdx = 0;
            foreach (var personDir in dirs)
            {
                string personName = Path.GetFileName(personDir);
                labelToName.Add((labelIdx, personName));

                var images = Directory
                    .GetFiles(personDir, "*.jpg")
                    .Concat(Directory.GetFiles(personDir, "*.png"))
                    .ToArray();

                if (images.Length == 0)
                {
                    Console.WriteLine($"⚠️  No images in '{personDir}', skipping.");
                    labelIdx++;
                    continue;
                }

                foreach (var imgPath in images)
                {
                    using var imgGray = Cv2.ImRead(imgPath, ImreadModes.Grayscale);
                    if (imgGray.Empty())
                    {
                        Console.WriteLine($"⚠️  Could not load '{imgPath}', skipping.");
                        continue;
                    }

                    var faces = _cascade.DetectMultiScale(
                        imgGray,
                        1.1,
                        5,
                        0,
                        new Size(_cfg.MinFaceSize, _cfg.MinFaceSize)
                    );

                    if (faces.Length == 0)
                    {
                        Console.WriteLine($"⚠️  No face in '{imgPath}', skipping.");
                        continue;
                    }

                    var r = faces[0];
                    using var faceROI = new Mat(imgGray, r).Resize(new Size(200, 200));
                    faceMats.Add(faceROI.Clone());
                    labels.Add(labelIdx);
                }

                labelIdx++;
            }

            if (faceMats.Count == 0)
            {
                Console.WriteLine("❌ No valid faces found. Aborting training.");
                return;
            }

            var recognizer = LBPHFaceRecognizer.Create();
            recognizer.Train(faceMats, labels);
            recognizer.Write(_cfg.ModelFile);
            Console.WriteLine($"✅ Model saved to '{_cfg.ModelFile}' (faces: {faceMats.Count}).");

            var labelsList = new LabelsFile
            {
                Labels = labelToName
                         .Select(t => new LabelEntry { Id = t.id, Name = t.name })
                         .ToList()
            };
            var json = JsonSerializer.Serialize(labelsList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cfg.LabelsFile, json);
            Console.WriteLine($"✅ Labels saved to '{_cfg.LabelsFile}'.");
        }

        public class LabelsFile
        {
            public List<LabelEntry> Labels { get; set; } = new();
        }

        public class LabelEntry
        {
            public int    Id   { get; set; }
            public string Name { get; set; } = "";
        }
    }
}
