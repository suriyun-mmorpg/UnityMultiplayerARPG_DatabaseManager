namespace MultiplayerARPG.MMO
{
    public partial class PostgreSQLDatabase
    {
        public PostgreSQLDatabase(ILogger<PostgreSQLDatabase> logger, IDatabaseUserLogin userLoginManager) : base(logger, userLoginManager)
        {
        }
    }
}
