using MultiplayerARPG.MMO;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (!int.TryParse(builder.Configuration["DatabaseType"], out int databaseType))
    databaseType = 0;
// Add services to the container.
switch (databaseType)
{
    case 1:
        builder.Services.AddSingleton<IDatabase, SQLiteDatabase>();
        break;
    default:
        builder.Services.AddSingleton<IDatabase, MySQLDatabase>();
        break;
}
builder.Services.AddSingleton<IDatabaseCache, LocalDatabaseCache>();
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
