using System.Net;
using System.Net.Mail;

namespace ClinicaJurassica.Services;

public interface IEmailService
{
    Task EnviarCodigoVerificacaoAsync(string email, string nome, string codigo);
}

public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarCodigoVerificacaoAsync(string email, string nome, string codigo)
    {
        var host = _config["Email:Smtp:Host"];
        var user = _config["Email:Smtp:User"];
        var password = _config["Email:Smtp:Password"];
        var from = _config["Email:Smtp:FromEmail"];
        var fromName = _config["Email:Smtp:FromName"] ?? "Clínica JurassiHealth";
        var port = _config.GetValue<int?>("Email:Smtp:Port") ?? 587;
        var enableSsl = _config.GetValue<bool?>("Email:Smtp:EnableSsl") ?? true;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("SMTP não configurado. Configure Email__Smtp__Host, User, Password e FromEmail.");

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = "Código de verificação - JurassiHealth",
            IsBodyHtml = true,
            Body = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;color:#273437">
                    <div style="background:#53818e;color:white;padding:22px;border-radius:16px 16px 0 0">
                        <h2 style="margin:0">Clínica JurassiHealth</h2>
                    </div>
                    <div style="padding:26px;background:#ffffff;border:1px solid #d9e2e5;border-top:0;border-radius:0 0 16px 16px">
                        <p>Olá, <strong>{WebUtility.HtmlEncode(nome)}</strong>.</p>
                        <p>Use o código abaixo para confirmar seu e-mail:</p>
                        <div style="font-size:34px;font-weight:800;letter-spacing:8px;color:#53818e;text-align:center;padding:20px;background:#f3f7f8;border-radius:14px">{codigo}</div>
                        <p style="margin-top:22px">O código é válido por 15 minutos. Se você não solicitou este cadastro, ignore esta mensagem.</p>
                    </div>
                </div>
                """
        };
        message.To.Add(new MailAddress(email, nome));

        using var smtp = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(user, password),
            Timeout = 15000
        };

        try
        {
            await smtp.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail de verificação para {Email}", email);
            throw;
        }
    }
}
