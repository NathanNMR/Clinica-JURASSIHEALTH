# Clínica JurassiHealth 🦖🩺

Sistema web para uma clínica médica fictícia (projeto acadêmico — PIM), com três perfis de
acesso (Paciente, Secretaria e Médico) e um painel de Administrador, construído em
**ASP.NET Core 8 (Web API) + Entity Framework Core + MySQL** no backend e uma **SPA em
HTML/JS puro + Bootstrap 5** no frontend, servida pela própria aplicação.

> Esta versão é uma correção/evolução do repositório original. Veja
> [`RELATORIO_ANALISE.md`](./RELATORIO_ANALISE.md) para a lista completa de pontos fortes,
> bugs encontrados e o que foi corrigido.

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

## Acesso de administrador

O admin não é mais uma conta cadastrada pela tela de "Cadastro" — ele é o único perfil com
permissão para criar médicos e secretárias, e usa as credenciais configuradas em
`AdminPadrao` (dentro de `appsettings.json`):

- **E-mail padrão (dev):** `adm@clinica.com`
- **Senha padrão (dev):** `adm123`

Troque `AdminPadrao:SenhaHash` por um novo hash BCrypt antes de qualquer uso real. Você pode
gerar um hash novo com qualquer gerador BCrypt (rounds 10–12) ou via `BCrypt.Net.BCrypt.HashPassword("nova-senha")`
em um projeto de teste rápido.

## Estrutura do projeto

```
├── Controllers/SistemaController.cs   # Endpoints da API (login, agendamento, prontuário...)
├── Data/ClinicaContext.cs             # DbContext (EF Core)
├── Models/Models.cs                   # Entidades + DTOs + validações
├── Services/TokenService.cs           # Emissão/validação de token de sessão (HMAC)
├── Services/TokenAuthAttribute.cs     # Filtro de autorização usado nos endpoints
├── Program.cs                         # Composição da aplicação (DI, CORS, arquivos estáticos)
├── database.sql                       # Schema do banco
└── wwwroot/
    ├── index.html                     # SPA (front-end)
    └── img/                           # Ilustrações do site (geradas para este projeto)
```

## Limitações conhecidas / próximos passos sugeridos

Este projeto continua sendo, propositalmente, enxuto (sem ASP.NET Identity, sem refresh
token, sem HTTPS forçado, sem testes automatizados) por ser um trabalho acadêmico. Para uma
evolução futura, valeria a pena:

- Migrar o esquema de autenticação para ASP.NET Identity ou JWT com refresh tokens;
- Adicionar testes de integração para os endpoints do `SistemaController`;
- Forçar HTTPS e adicionar cabeçalhos de segurança (HSTS, CSP) antes de qualquer deploy público;
- Criar um pipeline de migrations do EF Core (`dotnet ef migrations`) em vez do `database.sql` manual;
- Adicionar paginação nas listagens (documentos, consultas) para contas com muito histórico.
