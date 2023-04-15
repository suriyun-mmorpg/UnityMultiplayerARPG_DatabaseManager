namespace MultiplayerARPG.MMO
{
    public partial class BaseDatabase
    {
        protected ILogger _logger;

        public BaseDatabase(ILogger<BaseDatabase> logger, IDatabaseUserLogin userLoginManager)
        {
            _logger = logger;
            _userLoginManager = userLoginManager;
            Initialize();
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

        public void LogException(string tag, Exception ex)
        {
            _logger.LogCritical(ex, string.Empty);
        }
    }
}
