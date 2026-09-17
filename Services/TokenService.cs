using System.Security.Cryptography;
using System.Text;

namespace ClinicaJurassica.Services;

public record TokenPayload(int Id, string Tipo, long ExpiraEmUnix)
{
    public bool Expirado => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiraEmUnix;
}

public class TokenService
{
    private readonly byte[] _chave;
    private readonly int _expiracaoMinutos;

    public TokenService(IConfiguration config)
    {
        var segredo = config["Auth:SecretKey"];
        if (string.IsNullOrWhiteSpace(segredo) || segredo.Length < 32)
            throw new InvalidOperationException(
                "Auth:SecretKey não configurado ou muito curto. Use Auth__SecretKey com ao menos 32 caracteres.");
        _chave = Encoding.UTF8.GetBytes(segredo);
        _expiracaoMinutos = config.GetValue<int?>("Auth:ExpiracaoMinutos") ?? 120;
    }

    public string GerarToken(int id, string tipo)
    {
        var expira = DateTimeOffset.UtcNow.AddMinutes(_expiracaoMinutos).ToUnixTimeSeconds();
        var payload = $"{id}|{tipo}|{expira}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(_chave);
        return $"{Base64Url(bytes)}.{Base64Url(hmac.ComputeHash(bytes))}";
    }

    public TokenPayload? ValidarToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var partes = token.Split('.');
        if (partes.Length != 2) return null;

        try
        {
            var payloadBytes = FromBase64Url(partes[0]);
            var assinatura = FromBase64Url(partes[1]);
            using var hmac = new HMACSHA256(_chave);
            var esperada = hmac.ComputeHash(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(assinatura, esperada)) return null;

            var campos = Encoding.UTF8.GetString(payloadBytes).Split('|');
            if (campos.Length != 3 || !int.TryParse(campos[0], out var id) ||
                !long.TryParse(campos[2], out var expira)) return null;

            var p = new TokenPayload(id, campos[1], expira);
            return p.Expirado ? null : p;
        }
        catch { return null; }
    }

    private static string Base64Url(byte[] b) =>
        Convert.ToBase64String(b).Replace('+','-').Replace('/','_').TrimEnd('=');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-','+').Replace('_','/');
        if (s.Length % 4 == 2) s += "==";
        else if (s.Length % 4 == 3) s += "=";
        return Convert.FromBase64String(s);
    }
}
