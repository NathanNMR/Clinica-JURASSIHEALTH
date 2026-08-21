# Relatório de Análise — Clínica JurassiHealth

Repositório analisado: `https://github.com/NathanNMR/Clinica-JURASSIHEALTH.git`
Stack: ASP.NET Core 8 (Web API) + Entity Framework Core + MySQL (Pomelo) + SPA em HTML/JS/Bootstrap.

---

## 1. Pontos fortes

- **Separação de responsabilidades**: Models, DbContext e Controller bem segmentados, seguindo convenções do ASP.NET Core.
- **Senhas com hash**: os endpoints de cadastro já usavam `BCrypt.Net` para não gravar senha em texto puro.
- **Modelagem relacional coerente**: chaves estrangeiras entre pacientes, médicos, especialidades, agendamentos e documentos médicos, com `ON DELETE CASCADE` bem pensado na maior parte dos casos.
- **Proteção básica contra XSS já presente**: funções `escHtml`/`escJs` no front-end antes de inserir dados vindos do servidor no DOM — um cuidado que muitos projetos acadêmicos deixam passar.
- **Fluxo de negócio completo**: login por perfil, agendamento com verificação de horários ocupados, atendimento médico, emissão e visualização de documentos (receita/laudo/atestado) — o domínio do problema foi bem coberto.
- **Identidade visual consistente**: paleta de cores e tom "clínica + tema jurássico" aplicados de forma coerente no CSS.

## 2. Pontos fracos e bugs encontrados (com a correção aplicada)

### 2.1 Críticos

| # | Problema | Impacto | Correção aplicada |
|---|----------|---------|--------------------|
| 1 | **Faltava o arquivo `.csproj`** no repositório | O projeto **não compila** — `dotnet build`/`dotnet run` falham imediatamente | Criado `ClinicaJurassiHealth.csproj` com as dependências corretas (Pomelo MySQL, EF Core Design, BCrypt.Net-Next) |
| 2 | **Nenhum endpoint validava quem estava logado** (sem token/sessão real) | **IDOR grave**: qualquer pessoa podia editar o `localStorage` no navegador e ler/alterar dados de qualquer paciente, médico ou virar "admin"; endpoints também podiam ser chamados direto via `curl`/Postman sem login algum | Criado `TokenService` (token assinado com HMAC-SHA256) + `TokenAuthAttribute`, aplicados a todas as rotas sensíveis, com checagem de que o usuário só acessa **os próprios dados** (ou é da equipe, quando fizer sentido) |
| 3 | **Credenciais do administrador em texto puro no código-fonte** | Qualquer pessoa com acesso ao repositório (inclusive público, no GitHub) tinha a senha do admin | Movidas para `appsettings.json`, senha agora como **hash BCrypt**, não mais texto puro |
| 4 | **`index.html` nunca era servido pela aplicação** | `UseStaticFiles()` só serve `wwwroot/`, mas o `index.html` ficava na raiz do projeto — abrir `http://localhost:5000/` resultava em 404 | Arquivos movidos para `wwwroot/`, adicionado `UseDefaultFiles()` |
| 5 | **Connection string do MySQL (usuário root) hardcoded no `Program.cs`** | Credenciais de banco expostas no controle de versão | Movida para `appsettings.json` / variável de ambiente |
| 6 | **Cancelar uma consulta apagava, em cascata, qualquer receita/laudo/atestado emitido a partir dela** (`ON DELETE CASCADE` em `documentos_medicos.agendamento_id`) | Paciente podia **perder acesso a um documento médico válido** só porque a consulta de origem foi cancelada — grave em um sistema de saúde | FK alterada para `ON DELETE SET NULL`; coluna passou a aceitar `NULL` |
| 7 | **Imagens referenciadas no HTML nunca existiam no repositório** (`fundosite.png`, `carrossel1-4.jpeg`, `apoiodetexto1-3.jpeg`) | Site com várias imagens quebradas | Geradas 9 ilustrações SVG temáticas (ver seção 4) e referências corrigidas |
| 8 | **`LancarDocumento` confiava no `MedicoId`/`EspecialidadeId` enviados pelo cliente** | Um request forjado podia emitir um documento em nome de outro médico | Servidor agora deriva o médico emissor a partir do token autenticado, ignorando o valor enviado pelo cliente |

### 2.2 Moderados

| # | Problema | Impacto | Correção aplicada |
|---|----------|---------|--------------------|
| 9 | Nada impedia **dois agendamentos no mesmo horário para o mesmo médico** (condição de corrida entre a checagem em C# e o `INSERT`) | Overbooking | Índice único `(medico_id, data_hora)` no banco + tratamento de conflito (`409`) na API |
| 10 | `DateTime.Parse(data)` sem cultura fixa no endpoint `Ocupados` | Comportamento inconsistente entre servidores com culturas diferentes (ex.: `01/02` interpretado como dia ou mês trocado); exceção não tratada virava erro 500 | Trocado para `DateTime.TryParseExact` com formato fixo (`yyyy-MM-dd`) e `CultureInfo.InvariantCulture`, com retorno `400` amigável |
| 11 | `DataCadastro` mapeada como `DatabaseGeneratedOption.Identity`, mas a coluna real é `TIMESTAMP DEFAULT CURRENT_TIMESTAMP` (calculada, não um autoincremento) | Mapeamento semanticamente incorreto no EF Core | Alterado para `DatabaseGeneratedOption.Computed` |
| 12 | Endpoints administrativos (`CadastrarMedico`, `CadastrarSecretario`) podiam ser chamados por **qualquer pessoa**, sem estar logada como admin | Qualquer visitante podia criar um médico ou secretário no sistema | Restritos com `[TokenAuth("adm")]` |
| 13 | Busca de paciente por CPF (`BuscarPacienteCpf`) era pública | Vazamento de dados de pacientes para qualquer requisição não autenticada | Restrita a `secretaria`/`adm` |
| 14 | `BASE` da API fixo em `http://localhost:5000/Sistema` no JavaScript | Quebra em qualquer outra porta/host/deploy | Trocado para caminho relativo `/Sistema` |
| 15 | Ausência de `.gitignore` | Risco de versionar `bin/`, `obj/` e segredos locais | `.gitignore` criado |
| 16 | Faltavam validações de servidor (formato de e-mail, tamanho de CPF/telefone, senha mínima) | Dados inconsistentes podiam chegar ao banco mesmo burlando a validação do front-end | `DataAnnotations` adicionadas aos Models + checagem de `ModelState.IsValid` nos endpoints de cadastro |
| 17 | Cancelamento de consulta (`DELETE /Cancelar/{id}`) não conferia se quem cancelava era o dono da consulta | Um paciente podia cancelar a consulta de **qualquer outro paciente** só trocando o id na URL | Checagem de posse adicionada (`agendamento.PacienteId == usuário do token`) |

## 3. Sobre o CORS

A política original liberava `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` incondicionalmente. Isso é comum (e aceitável) em ambiente de desenvolvimento, mas perigoso se for para produção sem ajuste. Agora a liberação total só ocorre automaticamente em ambiente de **Desenvolvimento**; para outros ambientes, é preciso listar as origens permitidas em `Cors:OrigensPermitidas` (`appsettings.json`).

## 4. Imagens criadas

O repositório referenciava 8 imagens que nunca foram adicionadas (`fundosite.png`, `carrossel1.jpeg` a `carrossel4.jpeg`, `apoiodetexto1.jpeg` a `apoiodetexto3.jpeg`), resultando em ícones de imagem quebrada por todo o site. Foram geradas 9 ilustrações vetoriais (SVG, leves e nítidas em qualquer resolução) seguindo a paleta oficial do site (teal `#53818e`, laranja `#F39C12`, dourado `#d4af37`) e o tema "paleontologia + saúde" da marca:

- `fundosite.svg` — textura de fundo sutil para o body inteiro;
- `carrossel1.svg` a `carrossel4.svg` — banners do carrossel principal (excelência médica, equipe, tecnologia, cuidado ao paciente);
- `apoiodetexto1.svg` a `apoiodetexto3.svg` — imagens de apoio das seções de Missão, Visão e Valores;
- `favicon.svg` — ícone da aba do navegador.

## 5. O que **não** foi alterado de propósito

Para manter o escopo do trabalho fiel ao que já existia (é um projeto acadêmico/PIM, com um manual em PDF descrevendo requisitos), optei por **não** trocar a arquitetura de autenticação por algo como ASP.NET Identity + JWT completo, nem adicionar testes automatizados ou um pipeline de CI — essas são evoluções recomendadas, mas fora do escopo de "corrigir bugs existentes" (ver README para a lista de sugestões futuras).
