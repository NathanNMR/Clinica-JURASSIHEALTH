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
    private readonly IEmailService _email;
    private readonly VerificationCodeService _verificationCodes;

    public SistemaController(
        ClinicaContext db,
        TokenService tokens,
        IConfiguration config,
        LoginAttemptService loginAttempts,
        IEmailService email,
        VerificationCodeService verificationCodes)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
        _loginAttempts = loginAttempts;
        _email = email;
        _verificationCodes = verificationCodes;
    }

    private int Uid => (int)HttpContext.Items["uid"]!;
    private string Utipo => (string)HttpContext.Items["utipo"]!;

    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginReq l)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem = "Dados inválidos." });
        var email = l.Email.Trim().ToLowerInvariant();

        var bloqueio = await _loginAttempts.VerificarBloqueioAsync(email);
        if (bloqueio.Bloqueado)
            return StatusCode(429, new
            {
                mensagem = $"Limite de tentativas atingido. Tente novamente em {bloqueio.MinutosParaDesbloquear} minuto(s).",
                bloqueado = true,
                minutos = bloqueio.MinutosParaDesbloquear
            });

        async Task<IActionResult> Falhou()
        {
            var r = await _loginAttempts.RegistrarFalhaAsync(email);
            return r.Bloqueado
                ? StatusCode(429, new
                {
                    mensagem = $"Limite de tentativas atingido. Login bloqueado por {_config.GetValue<int?>("LoginLockout:BloqueioMinutos") ?? 15} minuto(s).",
                    bloqueado = true,
                    minutos = r.MinutosParaDesbloquear
                })
                : Unauthorized(new
                {
                    mensagem = $"E-mail ou senha inválidos. Restam {r.TentativasRestantes} tentativa(s).",
                    tentativasRestantes = r.TentativasRestantes
                });
        }

        var admEmail = _config["AdminPadrao:Email"]?.Trim().ToLowerInvariant();
        var admHash = _config["AdminPadrao:SenhaHash"];
        if (!string.IsNullOrWhiteSpace(admEmail) && email == admEmail &&
            !string.IsNullOrWhiteSpace(admHash) && VerificarSenha(l.Senha, admHash))
        {
            await _loginAttempts.RegistrarSucessoAsync(email);
            return Ok(new LoginResp { Tipo = "adm", Id = 0, Nome = "Administrador", Token = _tokens.GerarToken(0, "adm") });
        }

        var s = await _db.Secretarios.FirstOrDefaultAsync(x => x.Email == email);
        if (s != null && VerificarSenha(l.Senha, s.Senha))
        {
            await _loginAttempts.RegistrarSucessoAsync(email);
            return Ok(new LoginResp { Tipo = "secretaria", Id = s.Id, Nome = s.Nome, Token = _tokens.GerarToken(s.Id, "secretaria") });
        }

        var m = await _db.Medicos.Include(x => x.Especialidade).FirstOrDefaultAsync(x => x.Email == email);
        if (m != null && VerificarSenha(l.Senha, m.Senha))
        {
            await _loginAttempts.RegistrarSucessoAsync(email);
            return Ok(new LoginResp
            {
                Tipo = "medico",
                Id = m.Id,
                Nome = m.Nome,
                Crm = m.CRM,
                EspId = m.EspecialidadeId,
                EspNome = m.Especialidade?.Nome,
                Token = _tokens.GerarToken(m.Id, "medico")
            });
        }

        var p = await _db.Pacientes.FirstOrDefaultAsync(x => x.Email == email);
        if (p != null && VerificarSenha(l.Senha, p.Senha))
        {
            await _loginAttempts.RegistrarSucessoAsync(email);
            if (!p.EmailVerificado)
            {
                return StatusCode(403, new
                {
                    mensagem = "Seu e-mail ainda não foi verificado. Informe o código recebido por e-mail.",
                    requerVerificacao = true,
                    email = p.Email
                });
            }

            return Ok(new LoginResp { Tipo = "paciente", Id = p.Id, Nome = p.NomeCompleto, Token = _tokens.GerarToken(p.Id, "paciente") });
        }

        return await Falhou();
    }

    private static bool VerificarSenha(string senha, string hash)
    {
        if (!hash.StartsWith("$2")) return false;
        try { return BCrypt.Net.BCrypt.Verify(senha, hash); }
        catch { return false; }
    }

    [HttpPost("CadastrarPaciente")]
    public async Task<IActionResult> CadastrarPaciente(CadastrarPacienteReq req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { mensagem = "Preencha todos os campos corretamente." });

        var email = req.Email.Trim().ToLowerInvariant();
        var cpf = new string(req.CPF.Where(char.IsDigit).ToArray());
        var celular = new string(req.TelefoneCelular.Where(char.IsDigit).ToArray());
        var telefoneSec = string.IsNullOrWhiteSpace(req.TelefoneSecundario)
            ? null
            : new string(req.TelefoneSecundario.Where(char.IsDigit).ToArray());

        if (await _db.Pacientes.AnyAsync(x => x.Email == email))
            return Conflict(new { mensagem = "E-mail já cadastrado." });
        if (await _db.Pacientes.AnyAsync(x => x.CPF == cpf))
            return Conflict(new { mensagem = "CPF já cadastrado." });

        var codigo = _verificationCodes.GerarCodigo();
        var paciente = new Paciente
        {
            NomeCompleto = req.NomeCompleto.Trim(),
            Email = email,
            CPF = cpf,
            DataNascimento = req.DataNascimento,
            SexoGenero = req.SexoGenero.Trim(),
            TelefoneCelular = celular,
            TelefoneSecundario = telefoneSec,
            Senha = BCrypt.Net.BCrypt.HashPassword(req.Senha),
            EmailVerificado = false,
            CodigoVerificacaoHash = _verificationCodes.Hash(codigo),
            CodigoVerificacaoExpiraEm = DateTime.UtcNow.AddMinutes(15),
            TentativasVerificacao = 0,
            UltimoEnvioVerificacao = DateTime.UtcNow
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();

        try
        {
            await _email.EnviarCodigoVerificacaoAsync(paciente.Email, paciente.NomeCompleto, codigo);
        }
        catch
        {
            _db.Pacientes.Remove(paciente);
            await _db.SaveChangesAsync();
            return StatusCode(503, new
            {
                mensagem = "Não foi possível enviar o e-mail de verificação. Confira a configuração SMTP e tente novamente."
            });
        }

        return Ok(new
        {
            mensagem = "Cadastro realizado. Enviamos um código de 6 dígitos para confirmar seu e-mail.",
            requerVerificacao = true,
            email = paciente.Email
        });
    }

    [HttpPost("VerificarEmail")]
    public async Task<IActionResult> VerificarEmail(VerificarEmailReq req)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem = "Código inválido." });
        var email = req.Email.Trim().ToLowerInvariant();
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(x => x.Email == email);
        if (paciente == null) return BadRequest(new { mensagem = "Não foi possível validar o código." });
        if (paciente.EmailVerificado) return Ok(new { mensagem = "E-mail já verificado." });

        if (paciente.CodigoVerificacaoExpiraEm == null || paciente.CodigoVerificacaoExpiraEm < DateTime.UtcNow)
            return BadRequest(new { mensagem = "O código expirou. Solicite um novo código." });

        if (paciente.TentativasVerificacao >= 5)
            return StatusCode(429, new { mensagem = "Muitas tentativas de código. Solicite um novo código." });

        if (!_verificationCodes.Confere(req.Codigo, paciente.CodigoVerificacaoHash))
        {
            paciente.TentativasVerificacao++;
            await _db.SaveChangesAsync();
            var restantes = Math.Max(0, 5 - paciente.TentativasVerificacao);
            return BadRequest(new { mensagem = $"Código incorreto. Restam {restantes} tentativa(s)." });
        }

        paciente.EmailVerificado = true;
        paciente.CodigoVerificacaoHash = null;
        paciente.CodigoVerificacaoExpiraEm = null;
        paciente.TentativasVerificacao = 0;
        await _db.SaveChangesAsync();

        return Ok(new { mensagem = "E-mail verificado com sucesso. Agora você já pode fazer login." });
    }

    [HttpPost("ReenviarCodigoVerificacao")]
    public async Task<IActionResult> ReenviarCodigoVerificacao(ReenviarVerificacaoReq req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(x => x.Email == email);

        // Resposta genérica reduz enumeração de contas.
        if (paciente == null)
            return Ok(new { mensagem = "Se existir um cadastro pendente para esse e-mail, um novo código será enviado." });
        if (paciente.EmailVerificado)
            return Ok(new { mensagem = "Este e-mail já está verificado." });

        if (paciente.UltimoEnvioVerificacao.HasValue &&
            DateTime.UtcNow - paciente.UltimoEnvioVerificacao.Value < TimeSpan.FromSeconds(60))
        {
            var faltam = 60 - (int)(DateTime.UtcNow - paciente.UltimoEnvioVerificacao.Value).TotalSeconds;
            return StatusCode(429, new { mensagem = $"Aguarde {Math.Max(1, faltam)} segundo(s) antes de reenviar." });
        }

        var codigo = _verificationCodes.GerarCodigo();
        await _email.EnviarCodigoVerificacaoAsync(paciente.Email, paciente.NomeCompleto, codigo);

        paciente.CodigoVerificacaoHash = _verificationCodes.Hash(codigo);
        paciente.CodigoVerificacaoExpiraEm = DateTime.UtcNow.AddMinutes(15);
        paciente.TentativasVerificacao = 0;
        paciente.UltimoEnvioVerificacao = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { mensagem = "Novo código enviado. Ele é válido por 15 minutos." });
    }

    [HttpGet("ListarEspecialidades")]
    public async Task<IActionResult> ListarEspecialidades() =>
        Ok(await _db.Especialidades.AsNoTracking().OrderBy(x => x.Nome).ToListAsync());

    [HttpPost("CadastrarEspecialidade"), TokenAuth("adm")]
    public async Task<IActionResult> CadastrarEspecialidade(CadastrarEspecialidadeReq req)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem = "Informe um nome de especialidade válido." });
        var nome = req.Nome.Trim();
        if (await _db.Especialidades.AnyAsync(x => x.Nome == nome))
            return Conflict(new { mensagem = "Essa especialidade já está cadastrada." });

        var especialidade = new Especialidade { Nome = nome };
        _db.Especialidades.Add(especialidade);
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Especialidade cadastrada com sucesso.", especialidade });
    }

    [HttpGet("ListarMedicos")]
    public async Task<IActionResult> ListarMedicos() =>
        Ok(await _db.Medicos.AsNoTracking().Include(x => x.Especialidade).OrderBy(x => x.Nome).ToListAsync());

    [HttpGet("Ocupados")]
    public async Task<IActionResult> Ocupados(string data, int medicoId)
    {
        if (medicoId <= 0 || !DateTime.TryParseExact(data, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return BadRequest(new { mensagem = "Data ou médico inválido." });

        return Ok(await _db.Agendamentos
            .Where(x => x.DataHora.Date == dt.Date && x.MedicoId == medicoId && x.Status == "Agendado")
            .Select(x => x.DataHora.ToString("HH:mm")).ToListAsync());
    }

    [HttpPost("Agendar"), TokenAuth("paciente", "secretaria")]
    public async Task<IActionResult> Agendar(Agendamento a)
    {
        if (Utipo == "paciente") a.PacienteId = Uid;
        if (a.DataHora <= DateTime.Now)
            return BadRequest(new { mensagem = "Escolha uma data futura." });
        if (!await _db.Medicos.AnyAsync(x => x.Id == a.MedicoId))
            return BadRequest(new { mensagem = "Médico inválido." });

        a.Id = 0;
        a.Status = "Agendado";
        _db.Agendamentos.Add(a);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { mensagem = "Horário indisponível." }); }
        return Ok(new { mensagem = "Consulta agendada." });
    }

    [HttpGet("MinhasConsultas/{id}"), TokenAuth("paciente", "secretaria", "adm")]
    public async Task<IActionResult> MinhasConsultas(int id)
    {
        if (Utipo == "paciente" && Uid != id) return Forbid();
        return Ok(await _db.Agendamentos.AsNoTracking()
            .Include(x => x.Medico).ThenInclude(x => x!.Especialidade)
            .Where(x => x.PacienteId == id).OrderByDescending(x => x.DataHora).ToListAsync());
    }

    [HttpDelete("Cancelar/{id}"), TokenAuth("paciente", "secretaria", "adm")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var a = await _db.Agendamentos.FindAsync(id);
        if (a == null) return NotFound();
        if (Utipo == "paciente" && a.PacienteId != Uid) return Forbid();
        if (a.Status != "Agendado") return BadRequest(new { mensagem = "Somente consultas agendadas podem ser canceladas." });
        a.Status = "Cancelado";
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Consulta cancelada." });
    }

    [HttpGet("AgendaMedico/{id}"), TokenAuth("medico")]
    public async Task<IActionResult> AgendaMedico(int id)
    {
        if (Uid != id) return Forbid();
        return Ok(await _db.Agendamentos.AsNoTracking().Include(x => x.Paciente)
            .Where(x => x.MedicoId == id && x.Status == "Agendado").OrderBy(x => x.DataHora).ToListAsync());
    }

    [HttpPost("LancarDocumento"), TokenAuth("medico")]
    public async Task<IActionResult> LancarDocumento(DocumentoMedico d)
    {
        if (d.AgendamentoId is null) return BadRequest(new { mensagem = "Agendamento não informado." });
        var ag = await _db.Agendamentos.FindAsync(d.AgendamentoId.Value);
        var med = await _db.Medicos.FindAsync(Uid);
        if (ag == null || med == null) return NotFound();
        if (ag.MedicoId != Uid) return Forbid();

        d.MedicoId = Uid;
        d.PacienteId = ag.PacienteId;
        d.EspecialidadeId = med.EspecialidadeId ?? 4;
        d.DataEmissao = DateTime.Now;
        d.Visualizado = false;
        _db.DocumentosMedicos.Add(d);
        ag.Status = "Finalizado";
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Documento emitido." });
    }

    [HttpGet("DocumentosPaciente/{pacienteId}"), TokenAuth("paciente", "medico", "secretaria", "adm")]
    public async Task<IActionResult> DocumentosPaciente(int pacienteId, int? espId)
    {
        if (Utipo == "paciente" && Uid != pacienteId) return Forbid();
        var q = _db.DocumentosMedicos.AsNoTracking().Include(x => x.Medico)
            .Include(x => x.Especialidade).Where(x => x.PacienteId == pacienteId);
        if (espId.HasValue) q = q.Where(x => x.EspecialidadeId == espId.Value);
        return Ok(await q.OrderByDescending(x => x.DataEmissao).ToListAsync());
    }

    [HttpPost("VisualizarDocumento/{id}"), TokenAuth("paciente")]
    public async Task<IActionResult> VisualizarDocumento(int id)
    {
        var d = await _db.DocumentosMedicos.FindAsync(id);
        if (d == null) return NotFound();
        if (d.PacienteId != Uid) return Forbid();
        d.Visualizado = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("BuscarPacienteCpf/{cpf}"), TokenAuth("secretaria", "adm")]
    public async Task<IActionResult> BuscarPacienteCpf(string cpf)
    {
        cpf = new string(cpf.Where(char.IsDigit).ToArray());
        return Ok(await _db.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.CPF == cpf));
    }

    [HttpPost("CadastrarMedico"), TokenAuth("adm")]
    public async Task<IActionResult> CadastrarMedico(CadastrarMedicoReq req)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem = "Dados inválidos." });
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Medicos.AnyAsync(x => x.Email == email || x.CRM == req.CRM))
            return Conflict(new { mensagem = "E-mail ou CRM já cadastrado." });
        if (!await _db.Especialidades.AnyAsync(x => x.Id == req.EspecialidadeId))
            return BadRequest(new { mensagem = "Especialidade inválida." });

        var medico = new Medico
        {
            Nome = req.Nome.Trim(),
            CRM = req.CRM.Trim(),
            Email = email,
            EspecialidadeId = req.EspecialidadeId,
            Senha = BCrypt.Net.BCrypt.HashPassword(req.Senha)
        };
        _db.Medicos.Add(medico);
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Médico cadastrado." });
    }

    [HttpPost("CadastrarSecretario"), TokenAuth("adm")]
    public async Task<IActionResult> CadastrarSecretario(CadastrarSecretarioReq req)
    {
        if (!ModelState.IsValid) return BadRequest(new { mensagem = "Dados inválidos." });
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Secretarios.AnyAsync(x => x.Email == email))
            return Conflict(new { mensagem = "E-mail já cadastrado." });

        var secretario = new Secretario
        {
            Nome = req.Nome.Trim(),
            Email = email,
            Senha = BCrypt.Net.BCrypt.HashPassword(req.Senha)
        };
        _db.Secretarios.Add(secretario);
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Secretário cadastrado." });
    }
}
