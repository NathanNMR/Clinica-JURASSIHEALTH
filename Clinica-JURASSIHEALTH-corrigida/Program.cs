using ClinicaJurassica.Data;
using ClinicaJurassica.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// BUG CORRIGIDO: a connection string (com usuário/senha do banco) estava escrita direto
// no código-fonte. Agora vem de appsettings.json / appsettings.Development.json, com a
// possibilidade de ser sobrescrita por variável de ambiente (ConnectionStrings__Default)
// ou por `dotnet user-secrets`, sem precisar tocar no código nem no controle de versão.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' não configurada.");

builder.Services.AddDbContext<ClinicaContext>(options =>
    // BUG CORRIGIDO (build): o método correto do pacote Pomelo é "UseMySql" (não
    // "UseMySQL" — C# é case-sensitive). A versão do pacote usada aqui também exige que a
    // versão do servidor seja informada; ServerVersion.AutoDetect faz uma conexão rápida
    // para descobrir automaticamente se é MySQL ou MariaDB e qual versão, sem precisar
    // escrever isso à mão.
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddMemoryCache(); // usado pelo LoginAttemptService para o bloqueio de tentativas
builder.Services.AddSingleton<LoginAttemptService>();

// BUG/RISCO CORRIGIDO: a política de CORS original liberava QUALQUER origem, cabeçalho e
// método — inclusive em produção. Em desenvolvimento isso é conveniente, mas continua
// arriscado publicar assim. Agora a lista de origens permitidas vem de configuração
// (Cors:OrigensPermitidas em appsettings.json) e cai para "qualquer origem" apenas quando
// a aplicação está rodando em ambiente de Desenvolvimento.
var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("Padrao", p =>
{
    if (builder.Environment.IsDevelopment() && origensPermitidas.Length == 0)
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        p.WithOrigins(origensPermitidas).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// NOVO: cabeçalhos de segurança básicos. Nenhum deles existia antes. Reduzem a superfície de
// ataque para clickjacking (X-Frame-Options), MIME-sniffing (X-Content-Type-Options) e
// vazamento de URLs completas para terceiros ao seguir links (Referrer-Policy).
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// BUG CORRIGIDO: o index.html (e as imagens) ficavam na raiz do repositório, mas
// UseStaticFiles() por padrão só serve o conteúdo de wwwroot/ — ou seja, a página nunca
// era servida pela própria aplicação. Agora os arquivos estáticos ficam em wwwroot/, e
// UseDefaultFiles() faz "/" carregar automaticamente o index.html.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("Padrao");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

var url = builder.Configuration["Urls"] ?? "http://localhost:5000";
app.Run(url);
