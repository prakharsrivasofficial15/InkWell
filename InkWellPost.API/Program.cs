using System.Text;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using InkWellPost.API.Data;
using InkWellPost.API.Interfaces;
using InkWellPost.API.Repositories;
using InkWellPost.API.Services;
using InkWell.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

//Azure Key Vault
var keyVaultUri = builder.Configuration["KeyVaultUri"]
    ?? throw new InvalidOperationException("KeyVaultUri not configured.");

builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUri),
    new DefaultAzureCredential());

//Azure Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
    options.ConnectionString = builder.Configuration["inkwell-appinsights-connection"]);

//EF Core → Azure SQL
builder.Services.AddDbContext<PostDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration["inkwell-sql-connection"]));

//Azure Redis Cache
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var config = ConfigurationOptions.Parse(
        builder.Configuration["inkwell-redis-connection"]!);
    config.ConnectTimeout = 5000;
    config.SyncTimeout    = 5000;
    config.AbortOnConnectFail = false; // don't crash if Redis is unavailable
    return ConnectionMultiplexer.Connect(config);
});

// Azure Service Bus
builder.Services.AddSingleton(_ =>
    new ServiceBusClient(builder.Configuration["inkwell-servicebus-connection"]));

//JWT Auth (same secret as auth-service)
var jwtSecret = builder.Configuration["inkwell-jwt-secret"]
    ?? throw new InvalidOperationException("JWT secret not found.");

builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret   = jwtSecret;
    options.Issuer   = builder.Configuration["JwtSettings:Issuer"]!;
    options.Audience = builder.Configuration["JwtSettings:Audience"]!;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience            = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

//Session for maintaining view count & deduplication
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

//DI Registrations
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostService, PostServiceImpl>();

//CORS
var allowedOrigins = builder.Configuration["AllowedCorsOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
    options.AddPolicy("InkWellCors", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddAuthorization();
builder.Services.AddControllers();

//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "InkWell Post API",
        Version     = "v1",
        Description = "Manages post lifecycle: create, publish, search, like, view counts."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter: Bearer {your JWT token}",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id   = "Bearer"
            }
        },
        Array.Empty<string>()
    }});
});

var app = builder.Build();

//Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PostDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InkWell Post API v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("InkWellCors");
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();