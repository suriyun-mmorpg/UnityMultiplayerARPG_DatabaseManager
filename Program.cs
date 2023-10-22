using MultiplayerARPG.MMO;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Prepare logger
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});
builder.Services.AddSingleton(loggerFactory);

var programLogger = loggerFactory.CreateLogger<Program>();

// Config Manager
if (!int.TryParse(builder.Configuration["ConfigManager"], out int configManager))
    configManager = 0;
switch (configManager)
{
    default:
        builder.Services.AddSingleton<IConfigManager>(new DefaultConfigManager(loggerFactory.CreateLogger<DefaultConfigManager>()));
        break;
}

// User login manager
if (!int.TryParse(builder.Configuration["UserLoginManager"], out int userLoginManager))
    userLoginManager = 0;
IDatabaseUserLogin databaseUserLogin = null;
switch (userLoginManager)
{
    default:
        DefaultDatabaseUserLoginConfig defaultUserLoginConfig = builder.Configuration.GetValue<DefaultDatabaseUserLoginConfig>("UserLoginManagerConfig");
        databaseUserLogin = new DefaultDatabaseUserLogin(defaultUserLoginConfig);
        builder.Services.AddSingleton(databaseUserLogin);
        break;
}

// Cache manager
if (!int.TryParse(builder.Configuration["CacheManager"], out int cacheManager))
    cacheManager = 0;
switch (cacheManager)
{
    default:
        builder.Services.AddSingleton<IDatabaseCache>(new LocalDatabaseCache());
        break;
}

// Database server
if (!int.TryParse(builder.Configuration["DatabaseServer"], out int databaseServer))
    databaseServer = 0;
IDatabase database = null;
switch (databaseServer)
{
    case 1:
        database = new SQLiteDatabase(loggerFactory.CreateLogger<SQLiteDatabase>(), databaseUserLogin);
        builder.Services.AddSingleton(database);
        break;
    default:
        database = new MySQLDatabase(loggerFactory.CreateLogger<MySQLDatabase>(), databaseUserLogin);
        builder.Services.AddSingleton(database);
        break;
}

programLogger.LogInformation("Start migration...");
await database.DoMigration();
programLogger.LogInformation("Migration done.");

// Api
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    // Use the default property (Pascal) casing
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
    options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
});

// App secret
string apiSecretKey = builder.Configuration["ApiSecretKey"];
if (apiSecretKey == null)
    apiSecretKey = string.Empty;
builder.Services.AddAuthentication(AppSecretAuthenticationHandler.SCHEME)
    .AddScheme<AppSecretAuthenticationSchemeOptions, AppSecretAuthenticationHandler>(AppSecretAuthenticationHandler.SCHEME, o =>
    {
        o.AppSecret = apiSecretKey;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
