using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ClinicaJurassica.Models;

[Table("pacientes")]
public class Paciente
{
    [Key] public int Id { get; set; }

    [Column("nome_completo"), Required, StringLength(255, MinimumLength = 3)]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{11}$")]
    public string CPF { get; set; } = string.Empty;

    [Column("data_nascimento"), Required]
    public DateTime DataNascimento { get; set; }

    [Column("sexo_genero"), Required, StringLength(40)]
    public string SexoGenero { get; set; } = string.Empty;

    [Column("telefone_celular"), Required, RegularExpression(@"^\d{10,11}$")]
    public string TelefoneCelular { get; set; } = string.Empty;

    [Column("telefone_secundario")]
    public string? TelefoneSecundario { get; set; }

    // O hash nunca é devolvido pela API. DTOs separados recebem a senha em texto no cadastro/login.
    [Required, JsonIgnore]
    public string Senha { get; set; } = string.Empty;

    [Column("data_cadastro"), DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime DataCadastro { get; set; }
}

[Table("secretarios")]
public class Secretario
{
    [Key] public int Id { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Nome { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
    [Required, JsonIgnore] public string Senha { get; set; } = string.Empty;
}

[Table("especialidades")]
public class Especialidade
{
    [Key] public int Id { get; set; }
    [Required, StringLength(100)] public string Nome { get; set; } = string.Empty;
}

[Table("medicos")]
public class Medico
{
    [Key] public int Id { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Nome { get; set; } = string.Empty;

    [Column("especialidade_id")] public int? EspecialidadeId { get; set; }
    [ForeignKey("EspecialidadeId")] public virtual Especialidade? Especialidade { get; set; }

    [Required, RegularExpression(@"^\d{4,8}$")]
    public string CRM { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
    [Required, JsonIgnore] public string Senha { get; set; } = string.Empty;
}

[Table("agendamentos")]
public class Agendamento
{
    [Key] public int Id { get; set; }
    [Column("paciente_id")] public int PacienteId { get; set; }
    [ForeignKey("PacienteId")] public virtual Paciente? Paciente { get; set; }
    [Column("medico_id")] public int MedicoId { get; set; }
    [ForeignKey("MedicoId")] public virtual Medico? Medico { get; set; }
    [Column("data_hora")] public DateTime DataHora { get; set; }
    public string Status { get; set; } = "Agendado";
    public string? Observacao { get; set; }
}

[Table("documentos_medicos")]
public class DocumentoMedico
{
    [Key] public int Id { get; set; }
    [Column("agendamento_id")] public int? AgendamentoId { get; set; }
    [Column("paciente_id")] public int PacienteId { get; set; }
    [ForeignKey("PacienteId")] public virtual Paciente? Paciente { get; set; }
    [Column("medico_id")] public int MedicoId { get; set; }
    [ForeignKey("MedicoId")] public virtual Medico? Medico { get; set; }
    [Column("especialidade_id")] public int EspecialidadeId { get; set; }
    [ForeignKey("EspecialidadeId")] public virtual Especialidade? Especialidade { get; set; }
    [Required] public string Tipo { get; set; } = string.Empty;
    [Required] public string Conteudo { get; set; } = string.Empty;
    [Column("data_emissao")] public DateTime DataEmissao { get; set; } = DateTime.Now;
    [Column("visualizado")] public bool Visualizado { get; set; }
}

[Table("auditoria")]
public class Auditoria
{
    [Key] public long Id { get; set; }
    [Required] public string Tabela { get; set; } = string.Empty;
    [Required] public string Operacao { get; set; } = string.Empty;
    [Column("registro_id")] public int? RegistroId { get; set; }
    public string? Detalhes { get; set; }
    [Column("data_evento")] public DateTime DataEvento { get; set; }
}

[Table("login_tentativas")]
public class LoginTentativa
{
    [Key] public long Id { get; set; }
    [Required, StringLength(190)] public string Email { get; set; } = string.Empty;
    public int Tentativas { get; set; }
    [Column("primeira_tentativa_em")] public DateTime PrimeiraTentativaEm { get; set; }
    [Column("bloqueado_ate")] public DateTime? BloqueadoAte { get; set; }
}

public class LoginReq
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Senha { get; set; } = string.Empty;
}

public class LoginResp
{
    public string Tipo { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Crm { get; set; }
    public int? EspId { get; set; }
    public string? EspNome { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class CadastrarPacienteReq
{
    [Required, StringLength(255, MinimumLength = 3)] public string NomeCompleto { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter 11 dígitos.")] public string CPF { get; set; } = string.Empty;
    [Required] public DateTime DataNascimento { get; set; }
    [Required, StringLength(40)]
    [RegularExpression(@"^(Masculino cisgênero|Feminino cisgênero|Masculino transgênero|Feminino transgênero|Outros|Prefiro não informar)$", ErrorMessage = "Opção de gênero inválida.")]
    public string SexoGenero { get; set; } = string.Empty;
    [Required, RegularExpression(@"^\d{11}$", ErrorMessage = "Celular deve conter exatamente 11 dígitos.")] public string TelefoneCelular { get; set; } = string.Empty;
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Telefone secundário deve conter 10 ou 11 dígitos.")]
    public string? TelefoneSecundario { get; set; }
    [Required, MinLength(6, ErrorMessage = "A senha deve possuir pelo menos 6 caracteres.")] public string Senha { get; set; } = string.Empty;
}


public class CadastrarMedicoReq
{
    [Required, StringLength(100, MinimumLength = 3)] public string Nome { get; set; } = string.Empty;
    [Required, RegularExpression(@"^\d{4,8}$", ErrorMessage = "CRM deve conter de 4 a 8 dígitos.")] public string CRM { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Senha { get; set; } = string.Empty;
    [Required] public int EspecialidadeId { get; set; }
}

public class CadastrarSecretarioReq
{
    [Required, StringLength(100, MinimumLength = 3)] public string Nome { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Senha { get; set; } = string.Empty;
}

public class CadastrarEspecialidadeReq
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nome { get; set; } = string.Empty;
}
