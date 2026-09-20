# Banco de Dados

Script: `database/database-pim4.sql`.

Inclui:
- modelo físico;
- PK/FK;
- índices;
- `vw_consultas_detalhadas`;
- `sp_agenda_medico`;
- `sp_historico_paciente`;
- triggers de INSERT/UPDATE/DELETE em agendamentos;
- tabela de auditoria.

Relacionamentos principais:
- Especialidade 1:N Médico;
- Paciente 1:N Agendamento;
- Médico 1:N Agendamento;
- Agendamento 0:N Documento Médico.

## Evolução v7

A versão v7 adiciona:

- `pacientes.email_verificado`;
- hash e expiração do código de verificação;
- contador de tentativas de verificação;
- data do último envio de código;
- tabela `login_tentativas` para bloqueio persistente de autenticação.

Para bancos criados antes desta versão, use `database/migration-v7-email-login-especialidades.sql`.
