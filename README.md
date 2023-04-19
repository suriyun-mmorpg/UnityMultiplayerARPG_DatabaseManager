# UnityMultiplayerARPG_DatabaseManager

A MMORPG KIT's database manager project written by using NET7

## Config in `appsettings.json`

- `CacheManager` - Set which kind of cache manager, now it has only 1 option is `0`, which is a local cache manager (storing cache to dictionary, hashset). In the future it can have Redis cache manager.
- `DatabaseServer` - Set which kind of database server, now it has 2 options are `0` = MySQL, and `1` = SQLite
- `UserLoginManager` - Set which kind of user login manager, now it has only 1 option is `0`, which is a user login manager which use nanoid to generate ID and use MD5 for password encryption.
- `UserLoginManagerConfig:PasswordSaltPrefix` - This is config which will be used while `UserLoginManager` is `0`, it will be prefixed to password before encrypting with MD5, for example if you set to "PRE" and your password is `P@22w0rD` then it will be prepend to the password so the value will be "PREP@22w0rD" before encrypting and store to database.
- `UserLoginManagerConfig:PasswordSaltPostfix` - This is config which will be used while `UserLoginManager` is `0`, it will be prefixed to password before encrypting with MD5, for example if you set to "POST" and your password is `P@22w0rD` then it will be append to the password so the value will be "P@22w0rDPOST" before encrypting and store to database.
- `ApiSecretKey` - Set secret key to allow client to connect, if client connecting with the wrong secret key, it won't be able to do anything with this service.

## Config in `cofing/mySqlConfig.json`

- `mySqlAddress` address to MySQL server.
- `mySqlPort` port to MySQL server.
- `mySqlUsername` username to connect to MySQL server.
- `mySqlPassword` password to connect to MySQL server.
- `mySqlDbName` database name.
- `mySqlConnectionString` if this is set, it will use this as connection string, it won't use options above to create connection string

## Config in `config/sqliteConfig.json`

- `sqliteDbPath` path to where you want to store SQLite database file.
