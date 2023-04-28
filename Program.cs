using MultiplayerARPG.MMO;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// User login manager
if (!int.TryParse(builder.Configuration["UserLoginManager"], out int userLoginManager))
    userLoginManager = 0;
switch (userLoginManager)
{
    default:
        DefaultDatabaseUserLoginConfig defaultUserLoginConfig = builder.Configuration.GetValue<DefaultDatabaseUserLoginConfig>("UserLoginManagerConfig");
        builder.Services.AddSingleton<IDatabaseUserLogin>(provider => new DefaultDatabaseUserLogin(defaultUserLoginConfig));
        break;
}

// Cache manager
if (!int.TryParse(builder.Configuration["CacheManager"], out int cacheManager))
    cacheManager = 0;
switch (cacheManager)
{
    default:
        builder.Services.AddSingleton<IDatabaseCache, LocalDatabaseCache>();
        break;
}

// Database server
if (!int.TryParse(builder.Configuration["DatabaseServer"], out int databaseServer))
    databaseServer = 0;
switch (databaseServer)
{
    case 1:
        builder.Services.AddSingleton<IDatabase, SQLiteDatabase>();
        break;
    default:
        builder.Services.AddSingleton<IDatabase, MySQLDatabase>();
        break;
}

// Api
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    // Use the default property (Pascal) casing
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
    options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
});

// App secret
string? apiSecretKey = builder.Configuration["ApiSecretKey"];
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
