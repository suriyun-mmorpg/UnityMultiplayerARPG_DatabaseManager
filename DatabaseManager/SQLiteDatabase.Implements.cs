namespace MultiplayerARPG.MMO
{
    public partial class SQLiteDatabase
    {
        public SQLiteDatabase(ILogger<SQLiteDatabase> logger, IDatabaseUserLoginManager userLoginManager)
        {
            _logger = logger;
            _userLoginManager = userLoginManager;
            Initialize();
        }
    }
}
