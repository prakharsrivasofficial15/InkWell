using System.Text;
using Azure.Identity;
using InkWellAuth.API.Data;
using InkWellAuth.API.Interfaces;
using InkWellAuth.API.Repositories;
using InkWellAuth.API.Services;
using InkWell.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Azure Key Vault
// Load Key Vault URI from appsettings, then pull all secrets from vault
var keyVaultUri = builder.Configuration["KeyVaultUri"]
    ?? throw new InvalidOperationException("KeyVaultUri not configured.");

builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUri),
    new DefaultAzureCredential());

// Azure Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["inkwell-appinsights-connection"];
});

// EF Core → Azure SQL
builder.Services.AddDbContext<AuthDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration["inkwell-sql-connection"]));

// JWT Settings
builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret    = builder.Configuration["inkwell-jwt-secret"]!;
    options.Issuer    = builder.Configuration["JwtSettings:Issuer"]!;
    options.Audience  = builder.Configuration["JwtSettings:Audience"]!;
    options.ExpiryMinutes = int.Parse(
        builder.Configuration["JwtSettings:ExpiryMinutes"] ?? "60");
    options.RefreshTokenExpiryDays = int.Parse(
        builder.Configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7");
});

// JWT Authentication
var jwtSecret = builder.Configuration["inkwell-jwt-secret"]
    ?? throw new InvalidOperationException("JWT secret not found in Key Vault.");

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
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// DI Registrations
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthServiceImpl>();

// CORS (for Angular frontend)
var allowedOrigins = builder.Configuration["AllowedCorsOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("InkWellCors", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "InkWell Auth API",
        Version     = "v1",
        Description = "Handles registration, login, JWT, OAuth2, and user management."
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
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Auto-apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InkWell Auth API v1");
    c.RoutePrefix = string.Empty; 
});

app.UseCors("InkWellCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();