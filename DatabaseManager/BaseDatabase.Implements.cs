namespace MultiplayerARPG.MMO
{
    public partial class BaseDatabase
    {
        private ILogger _logger;

        public BaseDatabase(ILogger logger)
        {
            _logger = logger;
        }

        public void LogInformation(string tag, string msg)
        {
            _logger.LogInformation(msg);
        }

        public void LogWarning(string tag, string msg)
        {
            _logger.LogWarning(msg);
        }

        public void LogError(string tag, string msg)
        {
            _logger.LogError(msg);
        }

        public void LogException(string tag, System.Exception ex)
        {
            _logger.LogCritical(ex, string.Empty);
        }
    }
}
