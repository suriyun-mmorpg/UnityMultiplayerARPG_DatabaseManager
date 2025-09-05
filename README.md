# UnityMultiplayerARPG_DatabaseManager

A MMORPG KIT's database manager project written by using .NET9

## Config in `appsettings.json`

- `CacheManager` - Set which kind of cache manager, now it has 2 options are `0`, which is a local cache manager (storing cache to dictionary, hashset), and `1` redis.
- `DatabaseServer` - Set which kind of database server, now it has 2 options are `0` = MySQL, `1` = SQLite, and `2` = PostgreSQL
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

## Config in `config/pgsqlConfig.json`

- `pgAddress` address to PostgreSQL server.
- `pgPort` port to PostgreSQL server.
- `pgUsername` username to connect to PostgreSQL server.
- `pgPassword` password to connect to PostgreSQL server.
- `pgDbName` database name.
- `pgConnectionString` if this is set, it will use this as connection string, it won't use options above to create connection string

## Config in `config/redisConfig.json`

- `redisConnectionConfig` connection config to connect to redis server (ref. https://stackexchange.github.io/StackExchange.Redis/Configuration).
- `redisDbPrefix` prefix for keys which will be stored in Redis