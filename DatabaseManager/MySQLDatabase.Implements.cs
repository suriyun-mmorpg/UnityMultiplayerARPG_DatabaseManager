namespace MultiplayerARPG.MMO
{
    public partial class MySQLDatabase
    {
        public MySQLDatabase(ILogger logger) : base(logger)
        {
            Initialize();
        }
    }
}
