namespace MultiplayerARPG.MMO
{
    public partial class SQLiteDatabase
    {
        public SQLiteDatabase(ILogger logger) : base(logger)
        {
            Initialize();
        }
    }
}
