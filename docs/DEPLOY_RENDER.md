# Deploy no Render

1. Envie esta pasta para o GitHub.
2. No Render, crie um **Web Service** conectado ao repositório.
3. Escolha Docker.
4. Se o repositório contiver esta pasta como subpasta, configure o Root Directory como `Clinica-JURASSIHEALTH`.
5. Configure:
   - `ConnectionStrings__Default`
   - `Auth__SecretKey`
   - `AdminPadrao__Email`
   - `AdminPadrao__SenhaHash`
6. Health Check Path: `/health`
7. Faça o deploy.
8. Teste `/`, `/health` e o login.

Não coloque senha do banco no `appsettings.json`.


## Variáveis adicionais da v7

Para a verificação de e-mail funcionar, configure também no Render:


Antes do primeiro deploy da v7 sobre o banco atual da Aiven, execute `database/migration-v7-email-login-especialidades.sql`.
