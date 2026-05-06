using System.Text;
using Azure.Communication.Email;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using InkWellNewsletter.API.BackgroundServices;
using InkWellNewsletter.API.Data;
using InkWellNewsletter.API.Interfaces;
using InkWellNewsletter.API.Repositories;
using InkWellNewsletter.API.Services;
using InkWell.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Azure Key Vault
var keyVaultUri = builder.Configuration["KeyVaultUri"]
    ?? throw new InvalidOperationException("KeyVaultUri not configured.");

builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUri),
    new DefaultAzureCredential());

// Azure Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
    options.ConnectionString = builder.Configuration["inkwell-appinsights-connection"]);

// EF Core → Azure SQL
builder.Services.AddDbContext<NewsletterDbContext>(opts =>
    opts.UseSqlServer(
        builder.Configuration["inkwell-sql-connection"],
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// Azure Communication Services Email
builder.Services.AddSingleton(_ =>
    new EmailClient(builder.Configuration["inkwell-acs-connection"]));

// Azure Service Bus
var serviceBusConnection = builder.Configuration["inkwell-servicebus-connection"];
if (string.IsNullOrEmpty(serviceBusConnection))
{
    throw new InvalidOperationException("Service Bus connection string not found in configuration!");
}

Console.WriteLine($"Service Bus connection string loaded: {serviceBusConnection.Substring(0, Math.Min(50, serviceBusConnection.Length))}...");

builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));

// JWT Auth
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
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// DI Registrations
builder.Services.AddScoped<ISubscriberRepository, SubscriberRepository>();
builder.Services.AddScoped<INewsletterService, NewsletterServiceImpl>();

// Background Service (Service Bus Consumer)
builder.Services.AddHostedService<PostPublishedConsumer>();

// CORS
builder.Services.AddCors(options =>
    options.AddPolicy("InkWellCors", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "InkWell Newsletter API",
        Version     = "v1",
        Description = "Manages newsletter subscriptions, double opt-in, campaigns via ACS Email and Service Bus."
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

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NewsletterDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InkWell Newsletter API v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("InkWellCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();