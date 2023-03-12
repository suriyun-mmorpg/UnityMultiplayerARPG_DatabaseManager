namespace MultiplayerARPG.MMO
{
    public partial class BaseDatabase
    {
        private ILogger logger;

        public void LogInformation(string tag, string msg)
        {
            logger.LogInformation(msg);
        }

        public void LogWarning(string tag, string msg)
        {
            logger.LogWarning(msg);
        }

        public void LogError(string tag, string msg)
        {
            logger.LogError(msg);
        }

        public void LogException(string tag, System.Exception ex)
        {
            logger.LogCritical(ex, string.Empty);
        }
    }
}
