# Verificação de e-mail e proteção de login — v7

## Fluxo de cadastro do paciente

1. O paciente preenche o cadastro.
2. O backend valida CPF/e-mail duplicados e grava a senha como BCrypt.
3. É gerado um código criptograficamente aleatório de 6 dígitos.
4. Apenas o **hash HMAC-SHA256** desse código é salvo no banco.
5. O código em texto é enviado por SMTP para o e-mail do paciente.
6. O paciente possui 15 minutos e no máximo 5 tentativas para confirmar.
7. O login de paciente só é liberado após `email_verificado = 1`.

## Reenvio

O código pode ser reenviado, mas existe intervalo mínimo de 60 segundos. Um novo envio invalida o código anterior.

## Variáveis SMTP no Render

Configure em **Environment**:

- `Email__Smtp__Host`
- `Email__Smtp__Port` (normalmente 587)
- `Email__Smtp__User`
- `Email__Smtp__Password`
- `Email__Smtp__FromEmail`
- `Email__Smtp__FromName`
- `Email__Smtp__EnableSsl=true`

As credenciais SMTP não devem ser salvas no GitHub.

## Limite de login

O controle agora é persistido em `login_tentativas` no MySQL. Por padrão:

- máximo: 5 falhas;
- janela: 15 minutos;
- bloqueio: 15 minutos.

Os valores continuam configuráveis em `LoginLockout` no `appsettings.json`. Um login válido remove o registro de falhas daquele e-mail.

## Atualização do banco Aiven

Se o banco já existia antes da v7, execute uma única vez:

`database/migration-v7-email-login-especialidades.sql`

O script mantém pacientes antigos como verificados e faz com que novos cadastros passem pela verificação.
