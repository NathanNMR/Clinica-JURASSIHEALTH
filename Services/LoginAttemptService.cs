using ClinicaJurassica.Data;
using ClinicaJurassica.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaJurassica.Services;

public record ResultadoTentativa(bool Bloqueado, int TentativasRestantes, int MinutosParaDesbloquear);

public sealed class LoginAttemptService
{
    private readonly ClinicaContext _db;
    private readonly int _max;
    private readonly int _janela;
    private readonly int _bloqueio;

    public LoginAttemptService(ClinicaContext db, IConfiguration config)
    {
        _db = db;
        _max = config.GetValue<int?>("LoginLockout:MaxTentativas") ?? 5;
        _janela = config.GetValue<int?>("LoginLockout:JanelaMinutos") ?? 15;
        _bloqueio = config.GetValue<int?>("LoginLockout:BloqueioMinutos") ?? 15;
    }

    public async Task<ResultadoTentativa> VerificarBloqueioAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var registro = await _db.LoginTentativas.FirstOrDefaultAsync(x => x.Email == email);
        if (registro == null) return new(false, _max, 0);

        var agora = DateTime.UtcNow;
        if (registro.BloqueadoAte.HasValue && registro.BloqueadoAte > agora)
        {
            var min = Math.Max(1, (int)Math.Ceiling((registro.BloqueadoAte.Value - agora).TotalMinutes));
            return new(true, 0, min);
        }

        if (registro.BloqueadoAte.HasValue && registro.BloqueadoAte <= agora)
        {
            _db.LoginTentativas.Remove(registro);
            await _db.SaveChangesAsync();
            return new(false, _max, 0);
        }

        if (agora - registro.PrimeiraTentativaEm > TimeSpan.FromMinutes(_janela))
        {
            _db.LoginTentativas.Remove(registro);
            await _db.SaveChangesAsync();
            return new(false, _max, 0);
        }

        return new(false, Math.Max(0, _max - registro.Tentativas), 0);
    }

    public async Task<ResultadoTentativa> RegistrarFalhaAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var agora = DateTime.UtcNow;
        var registro = await _db.LoginTentativas.FirstOrDefaultAsync(x => x.Email == email);

        if (registro == null)
        {
            registro = new LoginTentativa
            {
                Email = email,
                Tentativas = 1,
                PrimeiraTentativaEm = agora
            };
            _db.LoginTentativas.Add(registro);
        }
        else if (agora - registro.PrimeiraTentativaEm > TimeSpan.FromMinutes(_janela))
        {
            registro.Tentativas = 1;
            registro.PrimeiraTentativaEm = agora;
            registro.BloqueadoAte = null;
        }
        else
        {
            registro.Tentativas++;
        }

        if (registro.Tentativas >= _max)
        {
            registro.BloqueadoAte = agora.AddMinutes(_bloqueio);
            await _db.SaveChangesAsync();
            return new(true, 0, _bloqueio);
        }

        await _db.SaveChangesAsync();
        return new(false, _max - registro.Tentativas, 0);
    }

    public async Task RegistrarSucessoAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var registro = await _db.LoginTentativas.FirstOrDefaultAsync(x => x.Email == email);
        if (registro == null) return;
        _db.LoginTentativas.Remove(registro);
        await _db.SaveChangesAsync();
    }
}
