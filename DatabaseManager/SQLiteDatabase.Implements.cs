namespace MultiplayerARPG.MMO
{
    public partial class SQLiteDatabase
    {
        public SQLiteDatabase(ILogger<SQLiteDatabase> logger)
        {
            _logger = logger;
            Initialize();
        }
    }
}
