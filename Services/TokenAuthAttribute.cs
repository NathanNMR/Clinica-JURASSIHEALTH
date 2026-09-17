using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClinicaJurassica.Services;

public class TokenAuthAttribute : ActionFilterAttribute
{
    private readonly string[] _tiposPermitidos;
    public TokenAuthAttribute(params string[] tiposPermitidos) => _tiposPermitidos = tiposPermitidos;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var svc = context.HttpContext.RequestServices.GetRequiredService<TokenService>();
        string? header = context.HttpContext.Request.Headers.Authorization;
        string? token = header?.StartsWith("Bearer ") == true ? header["Bearer ".Length..] : null;
        var payload = svc.ValidarToken(token);

        if (payload == null)
        {
            context.Result = new UnauthorizedObjectResult(new { mensagem = "Sessão inválida ou expirada." });
            return;
        }

        if (_tiposPermitidos.Length > 0 && !_tiposPermitidos.Contains(payload.Tipo))
        {
            context.Result = new ObjectResult(new { mensagem = "Sem permissão." }) { StatusCode = 403 };
            return;
        }

        context.HttpContext.Items["uid"] = payload.Id;
        context.HttpContext.Items["utipo"] = payload.Tipo;
    }
}
