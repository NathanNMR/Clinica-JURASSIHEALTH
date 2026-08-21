using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClinicaJurassica.Services
{
    /// <summary>
    /// Exige um token válido (Authorization: Bearer &lt;token&gt;) emitido pelo endpoint de Login.
    /// Se "tiposPermitidos" for informado, apenas esses tipos de usuário podem acessar a rota
    /// (ex.: [TokenAuth("adm")] para rotas exclusivas do administrador).
    ///
    /// Após validar, o tipo e o id do usuário autenticado ficam disponíveis em
    /// HttpContext.Items["utipo"] / HttpContext.Items["uid"], para que o controller confira
    /// se o usuário está tentando acessar apenas os PRÓPRIOS dados (ver SistemaController).
    /// </summary>
    public class TokenAuthAttribute : ActionFilterAttribute
    {
        private readonly string[] _tiposPermitidos;

        public TokenAuthAttribute(params string[] tiposPermitidos)
        {
            _tiposPermitidos = tiposPermitidos;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var tokenService = context.HttpContext.RequestServices.GetRequiredService<TokenService>();

            string? header = context.HttpContext.Request.Headers.Authorization;
            string? token = header?.StartsWith("Bearer ") == true ? header["Bearer ".Length..] : null;

            var payload = tokenService.ValidarToken(token);
            if (payload == null)
            {
                context.Result = new UnauthorizedObjectResult(new { mensagem = "Sessão inválida ou expirada. Faça login novamente." });
                return;
            }

            if (_tiposPermitidos.Length > 0 && !_tiposPermitidos.Contains(payload.Tipo))
            {
                context.Result = new ObjectResult(new { mensagem = "Você não tem permissão para acessar este recurso." }) { StatusCode = 403 };
                return;
            }

            context.HttpContext.Items["uid"] = payload.Id;
            context.HttpContext.Items["utipo"] = payload.Tipo;
            base.OnActionExecuting(context);
        }
    }
}
