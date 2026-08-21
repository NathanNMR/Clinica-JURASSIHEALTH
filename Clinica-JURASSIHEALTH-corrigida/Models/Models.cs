using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClinicaJurassica.Models
{
    [Table("pacientes")]
    public class Paciente
    {
        [Key] public int Id { get; set; }

        [Column("nome_completo")]
        [Required, StringLength(255, MinimumLength = 3)]
        public string NomeCompleto { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        // CPF deve ter exatamente 11 dígitos numéricos.
        [Required, RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter 11 dígitos numéricos.")]
        public string CPF { get; set; } = string.Empty;

        [Column("data_nascimento")]
        [Required]
        public DateTime DataNascimento { get; set; }

        [Column("sexo_genero")]
        [Required]
        public string SexoGenero { get; set; } = string.Empty;

        [Column("telefone_celular")]
        [Required, RegularExpression(@"^\d{10,11}$", ErrorMessage = "Celular deve conter 10 ou 11 dígitos.")]
        public string TelefoneCelular { get; set; } = string.Empty;

        [Column("telefone_secundario")]
        [RegularExpression(@"^\d{10,11}$|^$", ErrorMessage = "Telefone secundário inválido.")]
        public string? TelefoneSecundario { get; set; }

        // Sempre armazenada como hash BCrypt (nunca texto puro) — ver SistemaController.CadPac.
        [Required, MinLength(6, ErrorMessage = "A senha deve ter ao menos 6 caracteres.")]
        public string Senha { get; set; } = string.Empty;

        // BUG CORRIGIDO: a coluna do banco é TIMESTAMP DEFAULT CURRENT_TIMESTAMP (um valor
        // calculado pelo próprio banco), e não uma coluna de auto-incremento ("Identity").
        // Usar "Identity" aqui podia fazer o EF Core tentar tratá-la como chave/identidade,
        // gerando comportamento inconsistente ao inserir. O valor correto é "Computed".
        [Column("data_cadastro")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime DataCadastro { get; set; }
    }

    [Table("secretarios")]
    public class Secretario
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(100, MinimumLength = 3)] public string Nome { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Senha { get; set; } = string.Empty;
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

        [Required, RegularExpression(@"^\d{4,8}$", ErrorMessage = "CRM deve conter apenas números.")]
        public string CRM { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Senha { get; set; } = string.Empty;
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
        // Nullable de propósito: se a consulta de origem for cancelada/excluída, o documento
        // (receita/laudo/atestado) deve continuar existindo para o paciente — ver database.sql
        // (ON DELETE SET NULL) e o comentário no SistemaController.Cancelar.
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
        [Column("visualizado")] public bool Visualizado { get; set; } = false;
    }

    public class LoginReq
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Senha { get; set; } = string.Empty;
    }

    // DTO de resposta do login — evita expor a entidade completa (com hash de senha etc.)
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
}
