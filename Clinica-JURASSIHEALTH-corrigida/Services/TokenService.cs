using System.Security.Cryptography;
using System.Text;

namespace ClinicaJurassica.Services
{
    public record TokenPayload(int Id, string Tipo, long ExpiraEmUnix)
    {
        public bool Expirado => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiraEmUnix;
    }

    /// <summary>
    /// Gera e valida um token de sessão simples assinado com HMAC-SHA256.
    ///
    /// CONTEXTO / BUG CORRIGIDO: no projeto original, o login devolvia um objeto de sessão
    /// que o front-end guardava em localStorage e reenviava "por confiança" (id, tipo, nome).
    /// Nenhum endpoint da API validava essa sessão — bastava o usuário abrir o DevTools e
    /// editar o localStorage (ex.: trocar "tipo":"paciente" por "tipo":"adm", ou trocar o
    /// "id" para o de outro paciente) para acessar dados de qualquer pessoa, já que rotas
    /// como DocumentosPaciente/{id}, MinhasConsultas/{id}, AgendaMedico/{id} e Cancelar/{id}
    /// aceitavam qualquer id sem checar quem estava de fato autenticado (IDOR).
    ///
    /// Este serviço não substitui um esquema de autenticação completo (ex.: ASP.NET Identity
    /// + JWT com refresh tokens), mas resolve o problema central: sem o segredo do servidor
    /// não é possível forjar nem alterar um token válido, e cada requisição sensível passa a
    /// ser validada contra o dono real do recurso (ver TokenAuthAttribute).
    /// </summary>
    public class TokenService
    {
        private readonly byte[] _chave;
        private readonly int _expiracaoMinutos;

        public TokenService(IConfiguration config)
        {
            var segredo = config["Auth:SecretKey"];
            if (string.IsNullOrWhiteSpace(segredo))
                throw new InvalidOperationException("Auth:SecretKey não configurado (appsettings.json ou variável de ambiente Auth__SecretKey).");
            _chave = Encoding.UTF8.GetBytes(segredo);
            _expiracaoMinutos = config.GetValue<int?>("Auth:ExpiracaoMinutos") ?? 480;
        }

        public string GerarToken(int id, string tipo)
        {
            var expira = DateTimeOffset.UtcNow.AddMinutes(_expiracaoMinutos).ToUnixTimeSeconds();
            var payload = $"{id}|{tipo}|{expira}";
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(_chave);
            var assinatura = hmac.ComputeHash(payloadBytes);
            return $"{Base64Url(payloadBytes)}.{Base64Url(assinatura)}";
        }

        public TokenPayload? ValidarToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var partes = token.Split('.');
            if (partes.Length != 2) return null;

            byte[] payloadBytes, assinaturaRecebida;
            try
            {
                payloadBytes = FromBase64Url(partes[0]);
                assinaturaRecebida = FromBase64Url(partes[1]);
            }
            catch (FormatException) { return null; }

            using var hmac = new HMACSHA256(_chave);
            var assinaturaEsperada = hmac.ComputeHash(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(assinaturaRecebida, assinaturaEsperada))
                return null; // assinatura inválida => token forjado ou adulterado

            var texto = Encoding.UTF8.GetString(payloadBytes);
            var campos = texto.Split('|');
            if (campos.Length != 3) return null;
            if (!int.TryParse(campos[0], out var id)) return null;
            if (!long.TryParse(campos[2], out var expira)) return null;

            var payload = new TokenPayload(id, campos[1], expira);
            return payload.Expirado ? null : payload;
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static byte[] FromBase64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
