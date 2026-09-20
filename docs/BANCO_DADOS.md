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


## Evolução v8

A versão v8 mantém a tabela `login_tentativas` para o bloqueio persistente de autenticação. A verificação de e-mail foi removida da aplicação e não exige SMTP. Bancos que vieram da v7 podem manter as colunas antigas de verificação em `pacientes`; elas não são utilizadas pela aplicação.
