using ClinicaJurassica.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaJurassica.Data
{
    public class ClinicaContext : DbContext
    {
        public ClinicaContext(DbContextOptions<ClinicaContext> options) : base(options) { }

        public DbSet<Paciente> Pacientes { get; set; } = null!;
        public DbSet<Medico> Medicos { get; set; } = null!;
        public DbSet<Secretario> Secretarios { get; set; } = null!;
        public DbSet<Agendamento> Agendamentos { get; set; } = null!;
        public DbSet<Especialidade> Especialidades { get; set; } = null!;
        public DbSet<DocumentoMedico> DocumentosMedicos { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // BUG CORRIGIDO: nada impedia dois agendamentos para o mesmo médico no mesmo
            // horário (ex.: dois cliques rápidos em "Confirmar", ou paciente e secretária
            // agendando ao mesmo tempo). O índice único abaixo garante essa regra no nível
            // do banco, que é o único lugar em que ela pode ser garantida de forma confiável.
            modelBuilder.Entity<Agendamento>()
                .HasIndex(a => new { a.MedicoId, a.DataHora })
                .IsUnique();

            modelBuilder.Entity<Paciente>().HasIndex(p => p.Email).IsUnique();
            modelBuilder.Entity<Paciente>().HasIndex(p => p.CPF).IsUnique();
            modelBuilder.Entity<Medico>().HasIndex(m => m.Email).IsUnique();
            modelBuilder.Entity<Medico>().HasIndex(m => m.CRM).IsUnique();
            modelBuilder.Entity<Secretario>().HasIndex(s => s.Email).IsUnique();
        }
    }
}
