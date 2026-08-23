using Microsoft.Extensions.Caching.Memory;

namespace ClinicaJurassica.Services
{
    public record ResultadoTentativa(bool Bloqueado, int TentativasRestantes, int MinutosParaDesbloquear);

    /// <summary>
    /// Limita quantas vezes seguidas alguém pode errar a senha de um mesmo e-mail antes de
    /// precisar esperar. Guardado em memória (IMemoryCache) — suficiente para o volume de uma
    /// clínica e evita depender de uma tabela extra no banco só para isso; em uma implantação
    /// com múltiplas instâncias do servidor, o ideal seria mover isso para um cache distribuído
    /// (ex.: Redis), mas para este projeto o cache em memória resolve o problema real: hoje o
    /// login não tinha NENHUM limite de tentativas, o que permitia um ataque de força bruta
    /// (testar senhas repetidamente até acertar) sem qualquer obstáculo.
    /// </summary>
    public class LoginAttemptService
    {
        private readonly IMemoryCache _cache;
        private readonly int _maxTentativas;
        private readonly int _janelaMinutos;
        private readonly int _bloqueioMinutos;

        public LoginAttemptService(IMemoryCache cache, IConfiguration config)
        {
            _cache = cache;
            _maxTentativas = config.GetValue<int?>("LoginLockout:MaxTentativas") ?? 5;
            _janelaMinutos = config.GetValue<int?>("LoginLockout:JanelaMinutos") ?? 15;
            _bloqueioMinutos = config.GetValue<int?>("LoginLockout:BloqueioMinutos") ?? 15;
        }

        private static string ChaveTentativas(string email) => $"login_tentativas_{email}";
        private static string ChaveBloqueio(string email) => $"login_bloqueio_{email}";

        public ResultadoTentativa VerificarBloqueio(string email)
        {
            if (_cache.TryGetValue(ChaveBloqueio(email), out DateTimeOffset expiraEm))
            {
                var minutos = Math.Max(1, (int)Math.Ceiling((expiraEm - DateTimeOffset.UtcNow).TotalMinutes));
                return new ResultadoTentativa(true, 0, minutos);
            }
            return new ResultadoTentativa(false, _maxTentativas, 0);
        }

        public ResultadoTentativa RegistrarFalha(string email)
        {
            var chaveTent = ChaveTentativas(email);
            var tentativas = _cache.GetOrCreate(chaveTent, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_janelaMinutos);
                return 0;
            });
            tentativas++;
            _cache.Set(chaveTent, tentativas, TimeSpan.FromMinutes(_janelaMinutos));

            if (tentativas >= _maxTentativas)
            {
                var expiraEm = DateTimeOffset.UtcNow.AddMinutes(_bloqueioMinutos);
                _cache.Set(ChaveBloqueio(email), expiraEm, TimeSpan.FromMinutes(_bloqueioMinutos));
                _cache.Remove(chaveTent);
                return new ResultadoTentativa(true, 0, _bloqueioMinutos);
            }

            return new ResultadoTentativa(false, _maxTentativas - tentativas, 0);
        }

        public void RegistrarSucesso(string email)
        {
            _cache.Remove(ChaveTentativas(email));
            _cache.Remove(ChaveBloqueio(email));
        }
    }
}
