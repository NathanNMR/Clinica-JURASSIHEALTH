using System.Globalization;
using ClinicaJurassica.Data;
using ClinicaJurassica.Models;
using ClinicaJurassica.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaJurassica.Controllers;

[Route("Sistema")]
[ApiController]
public class SistemaController : ControllerBase
{
    private readonly ClinicaContext _db;
    private readonly TokenService _tokens;
    private readonly IConfiguration _config;
    private readonly LoginAttemptService _loginAttempts;

    public SistemaController(ClinicaContext db, TokenService tokens, IConfiguration config,
        LoginAttemptService loginAttempts)
    {
        _db = db; _tokens = tokens; _config = config; _loginAttempts = loginAttempts;
    }

    private int Uid => (int)HttpContext.Items["uid"]!;
    private string Utipo => (string)HttpContext.Items["utipo"]!;

    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginReq l)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem = "Dados inválidos." });
        var email = l.Email.Trim().ToLowerInvariant();

        var bloqueio = _loginAttempts.VerificarBloqueio(email);
        if (bloqueio.Bloqueado)
            return StatusCode(429, new { mensagem = $"Muitas tentativas. Tente novamente em {bloqueio.MinutosParaDesbloquear} minuto(s)." });

        IActionResult Falhou()
        {
            var r = _loginAttempts.RegistrarFalha(email);
            return r.Bloqueado
                ? StatusCode(429, new { mensagem = "Muitas tentativas. Acesso temporariamente bloqueado." })
                : Unauthorized(new { mensagem = "E-mail ou senha inválidos.", tentativasRestantes = r.TentativasRestantes });
        }

        var admEmail = _config["AdminPadrao:Email"]?.Trim().ToLowerInvariant();
        var admHash = _config["AdminPadrao:SenhaHash"];
        if (!string.IsNullOrWhiteSpace(admEmail) && email == admEmail &&
            !string.IsNullOrWhiteSpace(admHash) && VerificarSenha(l.Senha, admHash))
        {
            _loginAttempts.RegistrarSucesso(email);
            return Ok(new LoginResp { Tipo="adm", Id=0, Nome="Administrador", Token=_tokens.GerarToken(0,"adm") });
        }

        var s = await _db.Secretarios.FirstOrDefaultAsync(x => x.Email == email);
        if (s != null && VerificarSenha(l.Senha, s.Senha))
        {
            _loginAttempts.RegistrarSucesso(email);
            return Ok(new LoginResp { Tipo="secretaria", Id=s.Id, Nome=s.Nome, Token=_tokens.GerarToken(s.Id,"secretaria") });
        }

        var m = await _db.Medicos.Include(x => x.Especialidade).FirstOrDefaultAsync(x => x.Email == email);
        if (m != null && VerificarSenha(l.Senha, m.Senha))
        {
            _loginAttempts.RegistrarSucesso(email);
            return Ok(new LoginResp { Tipo="medico", Id=m.Id, Nome=m.Nome, Crm=m.CRM,
                EspId=m.EspecialidadeId, EspNome=m.Especialidade?.Nome, Token=_tokens.GerarToken(m.Id,"medico") });
        }

        var p = await _db.Pacientes.FirstOrDefaultAsync(x => x.Email == email);
        if (p != null && VerificarSenha(l.Senha, p.Senha))
        {
            _loginAttempts.RegistrarSucesso(email);
            return Ok(new LoginResp { Tipo="paciente", Id=p.Id, Nome=p.NomeCompleto, Token=_tokens.GerarToken(p.Id,"paciente") });
        }
        return Falhou();
    }

    static bool VerificarSenha(string senha, string hash)
    {
        if (!hash.StartsWith("$2")) return false;
        try { return BCrypt.Net.BCrypt.Verify(senha, hash); } catch { return false; }
    }

    [HttpPost("CadastrarPaciente")]
    public async Task<IActionResult> CadastrarPaciente(Paciente p)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem="Preencha todos os campos corretamente." });
        p.Email = p.Email.Trim().ToLowerInvariant();
        p.CPF = new string(p.CPF.Where(char.IsDigit).ToArray());
        p.TelefoneCelular = new string(p.TelefoneCelular.Where(char.IsDigit).ToArray());
        if (await _db.Pacientes.AnyAsync(x => x.Email == p.Email))
            return Conflict(new { mensagem="E-mail já cadastrado." });
        if (await _db.Pacientes.AnyAsync(x => x.CPF == p.CPF))
            return Conflict(new { mensagem="CPF já cadastrado." });
        p.Senha = BCrypt.Net.BCrypt.HashPassword(p.Senha);
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return Ok(new { mensagem="Cadastro realizado." });
    }

    [HttpGet("ListarEspecialidades")]
    public async Task<IActionResult> ListarEspecialidades() =>
        Ok(await _db.Especialidades.AsNoTracking().OrderBy(x => x.Nome).ToListAsync());

    [HttpGet("ListarMedicos")]
    public async Task<IActionResult> ListarMedicos() =>
        Ok(await _db.Medicos.AsNoTracking().Include(x=>x.Especialidade).OrderBy(x=>x.Nome).ToListAsync());

    [HttpGet("Ocupados")]
    public async Task<IActionResult> Ocupados(string data, int medicoId)
    {
        if (medicoId <= 0 || !DateTime.TryParseExact(data, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return BadRequest(new { mensagem="Data ou médico inválido." });

        return Ok(await _db.Agendamentos
            .Where(x => x.DataHora.Date == dt.Date && x.MedicoId == medicoId && x.Status=="Agendado")
            .Select(x=>x.DataHora.ToString("HH:mm")).ToListAsync());
    }

    [HttpPost("Agendar"), TokenAuth("paciente","secretaria")]
    public async Task<IActionResult> Agendar(Agendamento a)
    {
        if (Utipo=="paciente") a.PacienteId = Uid;
        if (a.DataHora <= DateTime.Now)
            return BadRequest(new { mensagem="Escolha uma data futura." });
        if (!await _db.Medicos.AnyAsync(x=>x.Id==a.MedicoId))
            return BadRequest(new { mensagem="Médico inválido." });

        a.Id=0; a.Status="Agendado";
        _db.Agendamentos.Add(a);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { mensagem="Horário indisponível." }); }
        return Ok(new { mensagem="Consulta agendada." });
    }

    [HttpGet("MinhasConsultas/{id}"), TokenAuth("paciente","secretaria","adm")]
    public async Task<IActionResult> MinhasConsultas(int id)
    {
        if (Utipo=="paciente" && Uid!=id) return Forbid();
        return Ok(await _db.Agendamentos.AsNoTracking()
            .Include(x=>x.Medico).ThenInclude(x=>x!.Especialidade)
            .Where(x=>x.PacienteId==id).OrderByDescending(x=>x.DataHora).ToListAsync());
    }

    [HttpDelete("Cancelar/{id}"), TokenAuth("paciente","secretaria","adm")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var a=await _db.Agendamentos.FindAsync(id);
        if(a==null) return NotFound();
        if(Utipo=="paciente" && a.PacienteId!=Uid) return Forbid();
        if(a.Status!="Agendado") return BadRequest(new { mensagem="Somente consultas agendadas podem ser canceladas." });
        a.Status="Cancelado";
        await _db.SaveChangesAsync();
        return Ok(new { mensagem="Consulta cancelada." });
    }

    [HttpGet("AgendaMedico/{id}"), TokenAuth("medico")]
    public async Task<IActionResult> AgendaMedico(int id)
    {
        if(Uid!=id) return Forbid();
        return Ok(await _db.Agendamentos.AsNoTracking().Include(x=>x.Paciente)
            .Where(x=>x.MedicoId==id && x.Status=="Agendado").OrderBy(x=>x.DataHora).ToListAsync());
    }

    [HttpPost("LancarDocumento"), TokenAuth("medico")]
    public async Task<IActionResult> LancarDocumento(DocumentoMedico d)
    {
        if(d.AgendamentoId is null) return BadRequest(new { mensagem="Agendamento não informado." });
        var ag=await _db.Agendamentos.FindAsync(d.AgendamentoId.Value);
        var med=await _db.Medicos.FindAsync(Uid);
        if(ag==null || med==null) return NotFound();
        if(ag.MedicoId!=Uid) return Forbid();

        d.MedicoId=Uid; d.PacienteId=ag.PacienteId;
        d.EspecialidadeId=med.EspecialidadeId ?? 4;
        d.DataEmissao=DateTime.Now; d.Visualizado=false;
        _db.DocumentosMedicos.Add(d);
        ag.Status="Finalizado";
        await _db.SaveChangesAsync();
        return Ok(new { mensagem="Documento emitido." });
    }

    [HttpGet("DocumentosPaciente/{pacienteId}"), TokenAuth("paciente","medico","secretaria","adm")]
    public async Task<IActionResult> DocumentosPaciente(int pacienteId, int? espId)
    {
        if(Utipo=="paciente" && Uid!=pacienteId) return Forbid();
        var q=_db.DocumentosMedicos.AsNoTracking().Include(x=>x.Medico)
            .Include(x=>x.Especialidade).Where(x=>x.PacienteId==pacienteId);
        if(espId.HasValue) q=q.Where(x=>x.EspecialidadeId==espId.Value);
        return Ok(await q.OrderByDescending(x=>x.DataEmissao).ToListAsync());
    }

    [HttpPost("VisualizarDocumento/{id}"), TokenAuth("paciente")]
    public async Task<IActionResult> VisualizarDocumento(int id)
    {
        var d=await _db.DocumentosMedicos.FindAsync(id);
        if(d==null) return NotFound();
        if(d.PacienteId!=Uid) return Forbid();
        d.Visualizado=true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("BuscarPacienteCpf/{cpf}"), TokenAuth("secretaria","adm")]
    public async Task<IActionResult> BuscarPacienteCpf(string cpf)
    {
        cpf=new string(cpf.Where(char.IsDigit).ToArray());
        return Ok(await _db.Pacientes.AsNoTracking().FirstOrDefaultAsync(x=>x.CPF==cpf));
    }

    [HttpPost("CadastrarMedico"), TokenAuth("adm")]
    public async Task<IActionResult> CadastrarMedico(Medico m)
    {
        if(!ModelState.IsValid) return BadRequest(new { mensagem="Dados inválidos." });
        m.Email=m.Email.Trim().ToLowerInvariant();
        if(await _db.Medicos.AnyAsync(x=>x.Email==m.Email || x.CRM==m.CRM))
            return Conflict(new { mensagem="E-mail ou CRM já cadastrado." });
        m.Senha=BCrypt.Net.BCrypt.HashPassword(m.Senha);
        _db.Medicos.Add(m); await _db.SaveChangesAsync();
        return Ok(new { mensagem="Médico cadastrado." });
    }

    [HttpPost("CadastrarSecretario"), TokenAuth("adm")]
    public async Task<IActionResult> CadastrarSecretario(Secretario s)
    {
        if(!ModelState.IsValid) return BadRequest(new { mensagem="Dados inválidos." });
        s.Email=s.Email.Trim().ToLowerInvariant();
        if(await _db.Secretarios.AnyAsync(x=>x.Email==s.Email))
            return Conflict(new { mensagem="E-mail já cadastrado." });
        s.Senha=BCrypt.Net.BCrypt.HashPassword(s.Senha);
        _db.Secretarios.Add(s); await _db.SaveChangesAsync();
        return Ok(new { mensagem="Secretário cadastrado." });
    }
}
