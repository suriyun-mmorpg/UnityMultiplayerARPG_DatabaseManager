namespace MultiplayerARPG.MMO
{
    public partial class SQLiteDatabase
    {
        public SQLiteDatabase(ILogger<SQLiteDatabase> logger, IDatabaseUserLogin userLoginManager) : base(logger, userLoginManager)
        {
        }
    }
}
