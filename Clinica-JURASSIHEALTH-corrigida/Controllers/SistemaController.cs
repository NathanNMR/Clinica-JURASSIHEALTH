using System.Globalization;
using ClinicaJurassica.Data;
using ClinicaJurassica.Models;
using ClinicaJurassica.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaJurassica.Controllers
{
    [Route("Sistema")]
    [ApiController]
    public class SistemaController : ControllerBase
    {
        private readonly ClinicaContext _db;
        private readonly TokenService _tokens;
        private readonly IConfiguration _config;

        public SistemaController(ClinicaContext db, TokenService tokens, IConfiguration config)
        {
            _db = db;
            _tokens = tokens;
            _config = config;
        }

        // Helpers para ler a identidade colocada pelo TokenAuthAttribute.
        private int Uid => (int)HttpContext.Items["uid"]!;
        private string Utipo => (string)HttpContext.Items["utipo"]!;

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginReq l)
        {
            var email = l.Email.Trim().ToLowerInvariant();

            // BUG/RISCO CORRIGIDO: antes, o e-mail e a senha do administrador ficavam
            // gravados em texto puro diretamente no código-fonte (visível a qualquer pessoa
            // com acesso ao repositório). Agora vêm da configuração, e a senha fica como
            // hash BCrypt — nunca em texto puro.
            var admEmail = _config["AdminPadrao:Email"]?.Trim().ToLowerInvariant();
            var admHash = _config["AdminPadrao:SenhaHash"];
            if (!string.IsNullOrEmpty(admEmail) && email == admEmail && !string.IsNullOrEmpty(admHash)
                && VerificarSenha(l.Senha, admHash))
            {
                var token = _tokens.GerarToken(0, "adm");
                return Ok(new LoginResp { Tipo = "adm", Id = 0, Nome = "Administrador", Token = token });
            }

            var s = await _db.Secretarios.FirstOrDefaultAsync(x => x.Email == email);
            if (s != null && VerificarSenha(l.Senha, s.Senha))
            {
                var token = _tokens.GerarToken(s.Id, "secretaria");
                return Ok(new LoginResp { Tipo = "secretaria", Id = s.Id, Nome = s.Nome, Token = token });
            }

            var m = await _db.Medicos.Include(x => x.Especialidade).FirstOrDefaultAsync(x => x.Email == email);
            if (m != null && VerificarSenha(l.Senha, m.Senha))
            {
                var token = _tokens.GerarToken(m.Id, "medico");
                return Ok(new LoginResp { Tipo = "medico", Id = m.Id, Nome = m.Nome, Crm = m.CRM, EspId = m.EspecialidadeId, EspNome = m.Especialidade?.Nome, Token = token });
            }

            var p = await _db.Pacientes.FirstOrDefaultAsync(x => x.Email == email);
            if (p != null && VerificarSenha(l.Senha, p.Senha))
            {
                var token = _tokens.GerarToken(p.Id, "paciente");
                return Ok(new LoginResp { Tipo = "paciente", Id = p.Id, Nome = p.NomeCompleto, Token = token });
            }

            // Mesma mensagem para "não existe" e "senha errada", propositalmente,
            // para não revelar quais e-mails estão cadastrados no sistema.
            return Unauthorized(new { mensagem = "E-mail ou senha inválidos." });
        }

        private static bool VerificarSenha(string senhaDigitada, string hashArmazenado)
        {
            // Guarda de segurança: só tenta verificar como BCrypt se o formato bater.
            // Evita uma exceção (e um erro 500) caso algum registro antigo/malformado
            // não esteja no formato esperado.
            if (!hashArmazenado.StartsWith("$2")) return false;
            try { return BCrypt.Net.BCrypt.Verify(senhaDigitada, hashArmazenado); }
            catch (BCrypt.Net.SaltParseException) { return false; }
        }

        [HttpPost("LancarDocumento")]
        [TokenAuth("medico")]
        public async Task<IActionResult> LancarDoc([FromBody] DocumentoMedico d)
        {
            // BUG/RISCO CORRIGIDO: o endpoint confiava cegamente no MedicoId e EspecialidadeId
            // enviados pelo cliente — um usuário mal-intencionado podia forjar o request e
            // emitir um documento "assinado" por outro médico. Agora o médico é sempre o que
            // está autenticado no token, nunca o que veio no corpo da requisição.
            var medico = await _db.Medicos.FindAsync(Uid);
            if (medico == null) return Unauthorized();

            if (d.AgendamentoId is null) return BadRequest(new { mensagem = "Agendamento não informado." });
            var agendamento = await _db.Agendamentos.FindAsync(d.AgendamentoId.Value);
            if (agendamento == null) return NotFound(new { mensagem = "Agendamento não encontrado." });
            if (agendamento.MedicoId != Uid)
                return Forbid();

            d.MedicoId = medico.Id;
            d.EspecialidadeId = medico.EspecialidadeId ?? d.EspecialidadeId;
            d.PacienteId = agendamento.PacienteId;
            d.DataEmissao = DateTime.Now;
            d.Visualizado = false;

            _db.DocumentosMedicos.Add(d);
            agendamento.Status = "Finalizado";
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("VisualizarDocumento/{id}")]
        [TokenAuth("paciente")]
        public async Task<IActionResult> VisDoc(int id)
        {
            var doc = await _db.DocumentosMedicos.FindAsync(id);
            if (doc == null) return NotFound();
            // BUG/RISCO CORRIGIDO: faltava checar se o documento pertence ao paciente logado —
            // qualquer paciente podia marcar (e, antes disso, inferir a existência de)
            // documentos de terceiros só adivinhando o id.
            if (doc.PacienteId != Uid) return Forbid();

            doc.Visualizado = true;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("DocumentosPaciente/{pacienteId}")]
        [TokenAuth("paciente", "medico", "secretaria", "adm")]
        public async Task<IActionResult> GetDocs(int pacienteId, [FromQuery] int? espId)
        {
            // Paciente só pode ver os PRÓPRIOS documentos. Médico/secretaria/adm podem
            // consultar o histórico de qualquer paciente (necessário para o atendimento).
            if (Utipo == "paciente" && Uid != pacienteId) return Forbid();

            var q = _db.DocumentosMedicos.Include(x => x.Medico).Include(x => x.Especialidade).Include(x => x.Paciente).Where(x => x.PacienteId == pacienteId);
            if (espId.HasValue) q = q.Where(x => x.EspecialidadeId == espId.Value);
            return Ok(await q.OrderByDescending(x => x.DataEmissao).ToListAsync());
        }

        [HttpGet("BuscarPacienteCpf/{cpf}")]
        [TokenAuth("secretaria", "adm")] // BUG/RISCO CORRIGIDO: busca de paciente por CPF era pública, sem exigir login da secretaria/adm.
        public async Task<IActionResult> GetPacCpf(string cpf) =>
            Ok(await _db.Pacientes.FirstOrDefaultAsync(x => x.CPF == cpf));

        [HttpGet("Ocupados")]
        public async Task<IActionResult> GetOcupados([FromQuery] string data, [FromQuery] int medicoId)
        {
            // BUG CORRIGIDO: DateTime.Parse(data) sem cultura fixa podia interpretar
            // "01/02/2026" como 1º de fevereiro em um servidor e 2 de janeiro em outro,
            // dependendo da cultura do sistema operacional. Agora o formato é sempre
            // "yyyy-MM-dd" (o que o <input type="date"> do navegador envia) e é validado
            // explicitamente em vez de deixar uma exceção não tratada virar erro 500.
            if (medicoId <= 0 || !DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return BadRequest(new { mensagem = "Data ou médico inválido." });

            return Ok(await _db.Agendamentos
                .Where(x => x.DataHora.Date == dt.Date && x.MedicoId == medicoId)
                .Select(x => x.DataHora.ToString("HH:mm"))
                .ToListAsync());
        }

        [HttpGet("ListarMedicos")]
        public async Task<IActionResult> ListMed() =>
            Ok(await _db.Medicos.Include(x => x.Especialidade).ToListAsync());

        [HttpGet("ListarEspecialidades")]
        public async Task<IActionResult> ListEsp() =>
            Ok(await _db.Especialidades.ToListAsync());

        [HttpPost("Agendar")]
        [TokenAuth("paciente", "secretaria")]
        public async Task<IActionResult> Agendar([FromBody] Agendamento a)
        {
            // Paciente só pode agendar para si mesmo; secretária pode agendar para qualquer paciente.
            if (Utipo == "paciente" && a.PacienteId != Uid) return Forbid();

            if (a.DataHora <= DateTime.Now)
                return BadRequest(new { mensagem = "Não é possível agendar em uma data/hora no passado." });

            var medicoExiste = await _db.Medicos.AnyAsync(x => x.Id == a.MedicoId);
            if (!medicoExiste) return BadRequest(new { mensagem = "Médico inválido." });

            a.Id = 0;
            a.Status = "Agendado";
            _db.Agendamentos.Add(a);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Acionado pelo índice único (MedicoId, DataHora) — corrige o bug de
                // "duplo agendamento" no mesmo horário por concorrência (duplo clique,
                // ou paciente e secretária agendando ao mesmo tempo).
                return Conflict(new { mensagem = "Esse horário acabou de ser preenchido. Escolha outro." });
            }
            return Ok();
        }

        [HttpGet("MinhasConsultas/{id}")]
        [TokenAuth("paciente", "secretaria", "adm")]
        public async Task<IActionResult> GetConsultas(int id)
        {
            if (Utipo == "paciente" && Uid != id) return Forbid();
            return Ok(await _db.Agendamentos.Include(x => x.Medico).ThenInclude(m => m!.Especialidade)
                .Where(x => x.PacienteId == id).OrderByDescending(x => x.DataHora).ToListAsync());
        }

        [HttpPost("CadastrarPaciente")]
        public async Task<IActionResult> CadPac([FromBody] Paciente p)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensagem = "Preencha todos os campos corretamente." });

            p.Email = p.Email.Trim().ToLowerInvariant();

            if (await _db.Pacientes.AnyAsync(x => x.Email == p.Email))
                return Conflict(new { mensagem = "O e-mail digitado já está cadastrado no sistema." });
            if (await _db.Pacientes.AnyAsync(x => x.CPF == p.CPF))
                return Conflict(new { mensagem = "O CPF digitado já está cadastrado no sistema." });

            p.Id = 0;
            p.Senha = BCrypt.Net.BCrypt.HashPassword(p.Senha);
            _db.Pacientes.Add(p);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("CadastrarMedico")]
        [TokenAuth("adm")] // BUG/RISCO CORRIGIDO: qualquer pessoa podia chamar essa rota diretamente (sem passar pela tela de admin) e criar um médico.
        public async Task<IActionResult> CadMed([FromBody] Medico m)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensagem = "Preencha todos os campos corretamente." });

            m.Email = m.Email.Trim().ToLowerInvariant();

            if (await _db.Medicos.AnyAsync(x => x.Email == m.Email))
                return Conflict(new { mensagem = "O e-mail digitado já está cadastrado para outro médico." });
            if (await _db.Medicos.AnyAsync(x => x.CRM == m.CRM))
                return Conflict(new { mensagem = "O CRM digitado já está cadastrado no sistema." });

            m.Id = 0;
            m.Senha = BCrypt.Net.BCrypt.HashPassword(m.Senha);
            _db.Medicos.Add(m);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("CadastrarSecretario")]
        [TokenAuth("adm")] // BUG/RISCO CORRIGIDO: idem acima, agora restrito ao administrador.
        public async Task<IActionResult> CadSec([FromBody] Secretario s)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensagem = "Preencha todos os campos corretamente." });

            s.Email = s.Email.Trim().ToLowerInvariant();

            if (await _db.Secretarios.AnyAsync(x => x.Email == s.Email))
                return Conflict(new { mensagem = "O e-mail digitado já está cadastrado para outro secretário." });

            s.Id = 0;
            s.Senha = BCrypt.Net.BCrypt.HashPassword(s.Senha);
            _db.Secretarios.Add(s);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("Cancelar/{id}")]
        [TokenAuth("paciente", "secretaria", "adm")]
        public async Task<IActionResult> Cancelar(int id)
        {
            var a = await _db.Agendamentos.FindAsync(id);
            if (a == null) return NotFound();
            // BUG/RISCO CORRIGIDO: qualquer pessoa logada podia cancelar a consulta de
            // qualquer outra, bastando trocar o id na URL.
            if (Utipo == "paciente" && a.PacienteId != Uid) return Forbid();

            // O agendamento é removido (como no projeto original), mas agora
            // documentos_medicos.agendamento_id usa ON DELETE SET NULL em vez de CASCADE
            // (ver database.sql), então cancelar a consulta não apaga mais, como efeito
            // colateral, uma receita/laudo/atestado que já tenha sido emitido a partir dela.
            _db.Agendamentos.Remove(a);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("AgendaMedico/{id}")]
        [TokenAuth("medico")]
        public async Task<IActionResult> GetAgenda(int id)
        {
            if (Uid != id) return Forbid();
            return Ok(await _db.Agendamentos.Include(x => x.Paciente)
                .Where(x => x.MedicoId == id).Where(x => x.Status == "Agendado")
                .OrderBy(x => x.DataHora).ToListAsync());
        }
    }
}
