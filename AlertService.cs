namespace FaceRecApp
{
    public class AlertService
    {
        private readonly Dictionary<int, DateTime> _lastAlert = new();
        private readonly TimeSpan                 _cooldown;

        public AlertService(int cooldownSeconds)
        {
            _cooldown = TimeSpan.FromSeconds(cooldownSeconds);
        }

        public bool ShouldAlert(int label)
        {
            if (!_lastAlert.ContainsKey(label)
             || (DateTime.Now - _lastAlert[label]) > _cooldown)
            {
                _lastAlert[label] = DateTime.Now;
                return true;
            }
            return false;
        }
    }
}
