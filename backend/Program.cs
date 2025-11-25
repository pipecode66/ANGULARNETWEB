using System.Text;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ??
                       "Host=localhost;Port=5432;Database=kanban;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<ExcelExportService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var originsSetting = builder.Configuration.GetValue<string>("Cors:AllowedOrigins") ?? "http://localhost:4200,http://localhost";
        var origins = originsSetting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (origins.Any(o => o == "*"))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT:Key no está configurado");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

await EnsureDatabaseAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task EnsureDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Asegura la columna UserId si la base ya existía sin ella y asigna tarjetas antiguas al admin.
    await db.Database.ExecuteSqlRawAsync("""
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Cards' AND column_name = 'UserId'
            ) THEN
                ALTER TABLE "Cards" ADD COLUMN "UserId" integer;
            END IF;
        END$$;
        """);

    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
        });
    }

    // Asigna cualquier tarjeta previa al usuario admin y fuerza not null.
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE "Cards"
        SET "UserId" = (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' LIMIT 1)
        WHERE "UserId" IS NULL;
        ALTER TABLE "Cards" ALTER COLUMN "UserId" SET NOT NULL;
    """);

    // Elimina las tarjetas de ejemplo iniciales para que cada usuario arranque sin datos.
    await db.Database.ExecuteSqlRawAsync("""
        DELETE FROM "Cards"
        WHERE "Title" IN ('Configurar proyecto','Diseñar API','Preparar UI','Entrega inicial');
    """);

    await db.SaveChangesAsync();
}
