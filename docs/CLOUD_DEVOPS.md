# Cloud e DevOps

Hospedagem escolhida: Render.

## Componentes
- container Docker ASP.NET Core 8;
- banco MySQL/MariaDB externo compatível;
- health check `/health`;
- CI com GitHub Actions;
- auto-deploy pelo Render.

## Segurança
Segredos devem ser variáveis de ambiente, nunca commitados.

## Escalabilidade
A aplicação pode ser replicada, mas o bloqueio de tentativas de login usa memória local; em múltiplas instâncias recomenda-se Redis.
