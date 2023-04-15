namespace MultiplayerARPG.MMO
{
    public partial class MySQLDatabase
    {
        public MySQLDatabase(ILogger<MySQLDatabase> logger, IDatabaseUserLogin userLoginManager) : base(logger, userLoginManager)
        {
        }
    }
}
