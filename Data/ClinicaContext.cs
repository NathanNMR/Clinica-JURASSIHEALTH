using ClinicaJurassica.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaJurassica.Data;

public class ClinicaContext : DbContext
{
    public ClinicaContext(DbContextOptions<ClinicaContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Medico> Medicos => Set<Medico>();
    public DbSet<Secretario> Secretarios => Set<Secretario>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<Especialidade> Especialidades => Set<Especialidade>();
    public DbSet<DocumentoMedico> DocumentosMedicos => Set<DocumentoMedico>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agendamento>().HasIndex(a => new { a.MedicoId, a.DataHora }).IsUnique();
        modelBuilder.Entity<Paciente>().HasIndex(p => p.Email).IsUnique();
        modelBuilder.Entity<Paciente>().HasIndex(p => p.CPF).IsUnique();
        modelBuilder.Entity<Medico>().HasIndex(m => m.Email).IsUnique();
        modelBuilder.Entity<Medico>().HasIndex(m => m.CRM).IsUnique();
        modelBuilder.Entity<Secretario>().HasIndex(s => s.Email).IsUnique();
        modelBuilder.Entity<Especialidade>().HasIndex(e => e.Nome).IsUnique();
    }
}
