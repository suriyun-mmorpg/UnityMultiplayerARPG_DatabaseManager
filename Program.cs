using MultiplayerARPG.MMO;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// User login manager
if (!int.TryParse(builder.Configuration["UserLoginManager"], out int userLoginManager))
    userLoginManager = 0;
switch (userLoginManager)
{
    default:
        DefaultUserLoginManagerConfig defaultUserLoginManagerConfig = builder.Configuration.GetValue<DefaultUserLoginManagerConfig>("UserLoginManagerConfig");

        builder.Services.AddSingleton<IDatabaseUserLoginManager>(provider => new DefaultUserLoginManager(defaultUserLoginManagerConfig));
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
string? apiSecretKey = builder.Configuration["ApiSecretKey"];
if (apiSecretKey == null)
    apiSecretKey = string.Empty;
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    // Use the default property (Pascal) casing
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
    options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
