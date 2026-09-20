using System.Security.Cryptography;
using System.Text;

namespace ClinicaJurassica.Services;

public sealed class VerificationCodeService
{
    private readonly byte[] _pepper;

    public VerificationCodeService(IConfiguration config)
    {
        var segredo = config["Auth:SecretKey"];
        if (string.IsNullOrWhiteSpace(segredo))
            throw new InvalidOperationException("Auth:SecretKey não configurado.");
        _pepper = Encoding.UTF8.GetBytes(segredo);
    }

    public string GerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string Hash(string codigo)
    {
        using var hmac = new HMACSHA256(_pepper);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(codigo)));
    }

    public bool Confere(string codigo, string? hashArmazenado)
    {
        if (string.IsNullOrWhiteSpace(hashArmazenado)) return false;
        var atual = Hash(codigo);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(atual), Convert.FromHexString(hashArmazenado));
    }
}
