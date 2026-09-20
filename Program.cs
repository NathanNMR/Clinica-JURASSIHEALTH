using ClinicaJurassica.Data;
using ClinicaJurassica.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default não configurada. Em produção use ConnectionStrings__Default.");

builder.Services.AddDbContext<ClinicaContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<LoginAttemptService>();
builder.Services.AddSingleton<VerificationCodeService>();
builder.Services.AddTransient<IEmailService, SmtpEmailService>();

var origens = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>()
              ?? Array.Empty<string>();

builder.Services.AddCors(options => options.AddPolicy("Padrao", p =>
{
    if (builder.Environment.IsDevelopment() && origens.Length == 0)
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else if (origens.Length > 0)
        p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JURASSIHEALTH API",
        Version = "v1",
        Description = "API REST da solução tecnológica integrada JURASSIHEALTH."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Bearer {token}"
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("Padrao");

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    app.Run($"http://0.0.0.0:{port}");
else
    app.Run(builder.Configuration["Urls"] ?? "http://localhost:5000");
