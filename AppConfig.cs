// AppConfig.cs
using System.Text.Json;

namespace FaceRecApp
{
    public class AppConfig
    {
        public string CascadeFile { get; set; } = "";
        public string DataFolder   { get; set; } = "";
        public string ModelFile    { get; set; } = "";
        public string LabelsFile   { get; set; } = "";
        public int    RecognitionThreshold   { get; set; }
        public int    MinFaceSize            { get; set; }
        public int    FrameSkip              { get; set; }
        public string CameraSourceRaw        { get; set; } = "";
        public int    AlertCooldownSeconds   { get; set; }

        public static AppConfig Load(string path = "config.json")
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Could not find configuration file '{path}'.");

            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AppConfig>(json, opts)
                   ?? throw new Exception("Failed to deserialize config.json");
        }
    }
}
