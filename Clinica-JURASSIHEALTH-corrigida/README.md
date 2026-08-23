# Clínica JurassiHealth 🦖🩺

Sistema web para uma clínica médica fictícia (projeto acadêmico — PIM), com três perfis de
acesso (Paciente, Secretaria e Médico) e um painel de Administrador, construído em
**ASP.NET Core 8 (Web API) + Entity Framework Core + MySQL** no backend e uma **SPA em
HTML/JS puro + Bootstrap 5** no frontend, servida pela própria aplicação.

O site tem identidade visual própria: o mascote **"Doutor Rex"** (um T-Rex de jaleco e
estetoscópio, desenhado especialmente para este projeto), paleta em tons de teal/âmbar
fóssil, tipografia Fraunces + Inter, e ilustrações originais de hospital/atendimento médico.

> Esta versão é uma correção/evolução do repositório original. Veja
> [`RELATORIO_ANALISE.md`](./RELATORIO_ANALISE.md) para a lista completa de pontos fortes,
> bugs encontrados e o que foi corrigido — incluindo a rodada mais recente (identidade visual,
> limite de tentativas de login e tempo máximo de sessão).

## Como rodar

### 1. Pré-requisitos
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- MySQL 8.0+ (local ou em container)

### 2. Banco de dados
```bash
mysql -u root -p < database.sql
```
Isso cria o banco `clinica_jurassihealth`, as tabelas e as especialidades iniciais.

### 3. Configuração
Ajuste a connection string em `appsettings.Development.json` (já incluso, apontando para
`root` sem senha — troque para o seu ambiente). **Nunca** commite credenciais reais; em
produção, prefira variáveis de ambiente:

```bash
export ConnectionStrings__Default="Server=SEU_HOST;Database=clinica_jurassihealth;Uid=SEU_USUARIO;Pwd=SUA_SENHA;"
export Auth__SecretKey="uma-chave-longa-e-aleatoria-só-sua"
```

### 4. Rodar
```bash
dotnet restore
dotnet run
```
A aplicação sobe em `http://localhost:5000` e já serve o site em `/` (não é mais necessário
abrir o `index.html` separadamente — ver seção de bugs corrigidos).

## Segurança da conta / sessão

- **Limite de tentativas de login**: por padrão, 5 tentativas erradas seguidas para o mesmo
  e-mail bloqueiam novas tentativas por 15 minutos (configurável em `LoginLockout`, no
  `appsettings.json`).
- **Tempo máximo de sessão**: o token emitido no login expira em 120 minutos por padrão
  (`Auth:ExpiracaoMinutos`). O front-end faz logout automático assim que o token expira,
  mesmo que a aba fique aberta.

## Acesso de administrador

O admin não é mais uma conta cadastrada pela tela de "Cadastro" — ele é o único perfil com
permissão para criar médicos e secretárias, e usa as credenciais configuradas em
`AdminPadrao` (dentro de `appsettings.json`):

- **E-mail padrão (dev):** `adm@clinica.com`
- **Senha padrão (dev):** `adm123`

Troque `AdminPadrao:SenhaHash` por um novo hash BCrypt antes de qualquer uso real. Você pode
gerar um hash novo com qualquer gerador BCrypt (rounds 10–12) ou via `BCrypt.Net.BCrypt.HashPassword("nova-senha")`
em um projeto de teste rápido.

## Identidade visual

- `wwwroot/img/logo-mark.png` / `favicon.png` — marca (recorte circular do mascote oficial).
- `wwwroot/img/mascote-hero.png` — o mascote "Doutor Rex" (arte fornecida pelo cliente), usado no hero.
- `wwwroot/img/foto-*.jpg` — fotografias de hospital/atendimento (fornecidas pelo cliente), usadas na vitrine e nos cards de Missão/Visão/Valores.
- `wwwroot/img/fundosite.png` / `fundo-footer.png` — padrão de fundo com o mascote repetido em tom acinzentado, gerado a partir da arte oficial.

> Seis das sete fotografias fornecidas têm resolução de origem baixa (147px de altura) e foram ampliadas com reamostragem de alta qualidade; para nitidez ideal, substitua por versões em resolução maior quando disponíveis (basta trocar o arquivo mantendo o mesmo nome em `wwwroot/img/`).

## Estrutura do projeto

```
├── Controllers/SistemaController.cs   # Endpoints da API (login, agendamento, prontuário...)
├── Data/ClinicaContext.cs             # DbContext (EF Core)
├── Models/Models.cs                   # Entidades + DTOs + validações
├── Services/TokenService.cs           # Emissão/validação de token de sessão (HMAC)
├── Services/TokenAuthAttribute.cs     # Filtro de autorização usado nos endpoints
├── Services/LoginAttemptService.cs    # Limite de tentativas de login (lockout)
├── Program.cs                         # Composição da aplicação (DI, CORS, arquivos estáticos)
├── database.sql                       # Schema do banco
└── wwwroot/
    ├── index.html                     # SPA (front-end)
    └── img/                           # Identidade visual e ilustrações do site
```

## Limitações conhecidas / próximos passos sugeridos

Este projeto continua sendo, propositalmente, enxuto (sem ASP.NET Identity, sem refresh
token, sem HTTPS forçado, sem testes automatizados) por ser um trabalho acadêmico. Para uma
evolução futura, valeria a pena:

- Migrar o esquema de autenticação para ASP.NET Identity ou JWT com refresh tokens;
- Adicionar testes de integração para os endpoints do `SistemaController`;
- Forçar HTTPS e adicionar uma Content-Security-Policy completa antes de qualquer deploy público;
- Criar um pipeline de migrations do EF Core (`dotnet ef migrations`) em vez do `database.sql` manual;
- Adicionar paginação nas listagens (documentos, consultas) para contas com muito histórico;
- Mover o limite de tentativas de login (hoje em memória) para um cache distribuído (ex.: Redis) caso a aplicação passe a rodar em múltiplas instâncias.

