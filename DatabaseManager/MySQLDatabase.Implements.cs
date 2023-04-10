namespace MultiplayerARPG.MMO
{
    public partial class MySQLDatabase
    {
        public MySQLDatabase(ILogger<MySQLDatabase> logger)
        {
            _logger = logger;
            Initialize();
            _logger.LogInformation("created");
        }
    }
}
