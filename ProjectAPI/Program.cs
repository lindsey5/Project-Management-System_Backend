using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ProjectAPI.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Load secret key from appsettings.json
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT Secret is not configured. Please add it to appsettings.json under Jwt:Secret.");
}

DotNetEnv.Env.Load();

// Add services to the container
builder.Services.AddControllers().AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddLogging(options =>
{
    options.AddConsole();  // Log to console
    options.AddDebug();    // Log to debug output
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMyOrigin", policy =>
    {
        policy.WithOrigins("https://projex-ph.onrender.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION");
Console.WriteLine(connectionString);
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("MYSQL_CONNECTION environment variable is not set.");
}

// Add MySQL connection with DbContext
builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 21))
    )
);
builder.Services.AddHttpClient();
// Register AuthService with the secret key from config
builder.Services.AddSingleton(new AuthService(jwtSecret));
// Register 
builder.Services.AddScoped<NotificationHub>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<AssigneeService>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<UserConnectionService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowMyOrigin");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/notifhub");
app.MapControllers();

app.Run();
