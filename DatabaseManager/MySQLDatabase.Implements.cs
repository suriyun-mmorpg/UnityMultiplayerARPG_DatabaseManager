namespace MultiplayerARPG.MMO
{
    public partial class MySQLDatabase
    {
        public MySQLDatabase(ILogger<MySQLDatabase> logger, IDatabaseUserLoginManager userLoginManager)
        {
            _logger = logger;
            _userLoginManager = userLoginManager;
            Initialize();
        }
    }
}
