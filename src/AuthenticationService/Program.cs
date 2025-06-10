using Authentication_Service.Business.Interfaces;
using Authentication_Service.Business.Services;
using Authentication_Service.Data;
using Authentication_Service.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:8081") // React frontend origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuthDb")));

builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<UserDeletedConsumer>();


var jwtKey = builder.Configuration["Jwt:Key"]?.Trim();
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new Exception("JWT secret key is missing!");
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");

var publisher = app.Services.GetRequiredService<RabbitMqPublisher>();
await publisher.InitAsync();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();


//app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");


app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();
}


app.Run();
