using Microsoft.Extensions.Caching.Memory;

namespace ClinicaJurassica.Services;

public record ResultadoTentativa(bool Bloqueado, int TentativasRestantes, int MinutosParaDesbloquear);

public class LoginAttemptService
{
    private readonly IMemoryCache _cache;
    private readonly int _max, _janela, _bloqueio;

    public LoginAttemptService(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _max = config.GetValue<int?>("LoginLockout:MaxTentativas") ?? 5;
        _janela = config.GetValue<int?>("LoginLockout:JanelaMinutos") ?? 15;
        _bloqueio = config.GetValue<int?>("LoginLockout:BloqueioMinutos") ?? 15;
    }

    static string T(string e) => $"login_tentativas_{e}";
    static string B(string e) => $"login_bloqueio_{e}";

    public ResultadoTentativa VerificarBloqueio(string email)
    {
        if (_cache.TryGetValue(B(email), out DateTimeOffset expira))
        {
            var min = Math.Max(1, (int)Math.Ceiling((expira - DateTimeOffset.UtcNow).TotalMinutes));
            return new(true, 0, min);
        }
        return new(false, _max, 0);
    }

    public ResultadoTentativa RegistrarFalha(string email)
    {
        var tentativas = _cache.Get<int?>(T(email)) ?? 0;
        tentativas++;
        _cache.Set(T(email), tentativas, TimeSpan.FromMinutes(_janela));

        if (tentativas >= _max)
        {
            _cache.Set(B(email), DateTimeOffset.UtcNow.AddMinutes(_bloqueio),
                TimeSpan.FromMinutes(_bloqueio));
            _cache.Remove(T(email));
            return new(true, 0, _bloqueio);
        }

        return new(false, _max - tentativas, 0);
    }

    public void RegistrarSucesso(string email)
    {
        _cache.Remove(T(email));
        _cache.Remove(B(email));
    }
}
