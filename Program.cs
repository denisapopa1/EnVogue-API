using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using WardrobeApi.Data;
using WardrobeApi.Models;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
var authDebugLogPath = Path.Combine(builder.Environment.ContentRootPath, "auth-debug.log");

void WriteAuthDebug(string message)
{
    try
    {
        File.AppendAllText(authDebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
    }
    catch
    {
        // Best-effort local debug logging for development only.
    }
}

if (builder.Environment.IsDevelopment())
{
    WriteAuthDebug("----- Application startup -----");
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient<WardrobeApi.Controllers.OutfitGeneratorController>();
builder.Services.AddScoped<DemoDatasetSeeder>();
builder.Services.AddEndpointsApiExplorer();
// Adaugă serviciul CORS pentru a permite accesul frontend-ului
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorOrigin",
        policy => policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    || System.Net.IPAddress.TryParse(uri.Host, out var ipAddress)
                        && (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                            || ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Nu uita să pui app.UseCors("AllowBlazorOrigin") după app.UseHttpsRedirection()!
// Configure DbContext with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Wardrobe.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Authentication & JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"] ?? "default_secret_key_1234567890123456_is_long_enough";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero // Remove delay for token expiration
    };

    if (builder.Environment.IsDevelopment())
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var hasAuthorization = context.Request.Headers.ContainsKey("Authorization");
                WriteAuthDebug($"JWT message received for {context.Request.Method} {context.Request.Path}. Authorization header present: {hasAuthorization}");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                WriteAuthDebug($"JWT authentication failed for {context.Request.Method} {context.Request.Path}: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                WriteAuthDebug($"JWT challenge for {context.Request.Method} {context.Request.Path}. Error: {context.Error}; Description: {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    }
});

// Configure Swagger to use JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WardrobeApi", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token only.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 1. Migrare și Seed (Baza de date)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        await DbSchemaUpdater.EnsureUserProfilesTableAsync(context);
        await DbSeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Eroare la pornirea bazei de date: {ex.Message}");
    }
}

// 2. Configurare Swagger (Doar în Development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WardrobeApi v1");
        c.RoutePrefix = string.Empty; // Swagger va fi pagina principală a API-ului
    });
}

// --- ORDINEA MIDDLEWARE-URILOR (FOARTE IMPORTANTĂ) ---

// 3. Securitate de bază
// In development, the frontend often calls the HTTP endpoint directly.
// Redirecting to HTTPS changes the origin/port and can drop Authorization on browser redirects.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 4. STATIC FILES - Aceasta este "magia" care repară pozele
app.UseStaticFiles(); // Permite accesul la folderul wwwroot general

// Configurăm accesul special pentru folderul de upload-uri
// Ne asigurăm că serverul vede wwwroot/uploads/images
string uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// 5. Ruting și Securitate Token
app.UseRouting();

app.UseCors("AllowBlazorOrigin");

if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var hasAuthorization = context.Request.Headers.ContainsKey("Authorization");
            WriteAuthDebug($"Incoming {context.Request.Method} {context.Request.Path}. Authorization header present: {hasAuthorization}");
        }

        await next();

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "(null)";
            WriteAuthDebug($"Completed {context.Request.Method} {context.Request.Path} with {context.Response.StatusCode}. UserId: {userId}");
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();

// 6. Mapare Controller-e
app.MapControllers();

app.Run();
