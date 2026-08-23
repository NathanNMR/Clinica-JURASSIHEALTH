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

---

## 6. Segunda rodada: identidade visual, segurança de sessão e demais melhorias

### 6.1 Identidade visual

O site usava Bootstrap "de fábrica" (cores padrão, cards genéricos, hero em carrossel com foto escurecida + texto centralizado — um padrão muito comum e pouco memorável) e as imagens que eu havia gerado antes eram ilustrações abstratas (batimento cardíaco, DNA, escudo). Nesta rodada:

- **Mascote "Doutor Rex"**: personagem desenhado do zero (SVG, estilo *chibi*), um T-Rex de jaleco branco e estetoscópio, com 3 poses (estetoscópio, prancheta, escudo) usadas no hero e nas seções de Missão/Visão/Valores.
- **Nova paleta**: teal profundo + âmbar "fóssil" (referência à resina fossilizada do período Jurássico — mais alinhado ao tema do que o laranja genérico anterior) sobre fundo creme/osso.
- **Nova tipografia**: Fraunces (serifada, para títulos) + Inter (corpo) + IBM Plex Mono (rótulos pequenos, estilo etiqueta de museu/catálogo).
- **Fundo com "vários Doutor Rex"**: conforme solicitado, o fundo do site (`fundosite.svg`) tem múltiplas silhuetas do mascote em tom de cinza, espalhadas em posições/rotações/escalas variadas, com opacidade baixa para não competir com o conteúdo. *(Durante a criação, encontrei e corrigi um bug de pré-visualização: `<use>` referenciando `<symbol>` com `opacity` não renderizava a opacidade corretamente em alguns renderizadores — troquei para grupos `<g opacity="...">` inline, que é o padrão mais confiável entre navegadores.)*
- **Imagens de hospital/atendimento**: 4 cenas ilustradas novas — fachada do hospital com ambulância, sala de atendimento/consulta, equipe multidisciplinar, e pronto-atendimento com monitor cardíaco — substituindo o carrossel genérico anterior.
- **Hero redesenhado**: o antigo padrão de "foto escurecida + texto branco centralizado" foi substituído por um layout mais autoral (mascote + texto lado a lado, com uma pequena faixa de fatos rápidos), e o carrossel de imagens foi realocado para uma seção de vitrine abaixo do hero, sem o filtro de escurecimento (desnecessário em ilustrações vetoriais).
- **Favicon e logo-mark**: ambos usam o rosto do mascote simplificado, legível mesmo em 16×16px.

### 6.2 Segurança de conta e sessão (novas funcionalidades pedidas)

| Funcionalidade | Como foi implementada |
|---|---|
| **Limite de tentativas de login** | Novo `LoginAttemptService` (em memória, via `IMemoryCache`): 5 tentativas erradas para o mesmo e-mail bloqueiam novos logins por 15 minutos (ambos configuráveis em `appsettings.json → LoginLockout`). A resposta HTTP `429 Too Many Requests` informa quanto tempo falta; enquanto ainda há tentativas, a mensagem de erro informa quantas restam. |
| **Tempo máximo de sessão** | O token já expirava (`Auth:ExpiracaoMinutos`, reduzido de 8h para 2h como padrão mais razoável); agora o **front-end também decodifica a expiração do token e faz logout automático** assim que ela é atingida, mesmo com a aba aberta — antes, uma sessão salva no `localStorage` "durava para sempre" do ponto de vista do navegador. |

### 6.3 Outras melhorias adicionadas

- **Cabeçalhos de segurança HTTP** (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) — nenhum existia antes.
- **Confirmação de senha** no cadastro de paciente (campo duplicado + checagem no front-end antes de enviar).
- **Estados vazios** nas listas (consultas, documentos, agenda do médico, histórico do paciente) — antes, uma lista vazia simplesmente não mostrava nada; agora mostra uma mensagem amigável com ícone.
- **Indicadores de carregamento** (spinner + texto "Enviando...") nos botões de Login e Cadastro, para dar feedback durante a chamada à API.
- **Selo de sessão** na barra de navegação, mostrando "Sessão ativa · expira em N min" (atualizado a cada 30s).
- **Status "Finalizado"** agora aparece como selo na lista de consultas do paciente, em vez de simplesmente não mostrar nada quando não é possível cancelar.
- **Meta description** e `:focus-visible` (contorno visível ao navegar por teclado) adicionados para acessibilidade/SEO básicos.

---

## 7. Terceira rodada: mascote mais fofo e ilustrações humanas

O feedback foi direto: o mascote e as cenas da rodada anterior estavam com aparência fraca. Refiz as duas coisas do zero:

- **Mascote "Doutor Rex" 2.0**: proporções *kawaii* (cabeça bem maior em relação ao corpo, olhos enormes com brilho e reflexo, bochechas rosadas), gradientes suaves na pele e no jaleco (em vez de cor lisa), sombra de contato no chão, espinhos da cabeça arredondados como uma coroinha (em vez de picos agressivos) e uma boquinha sorridente com dois dentinhos discretos.
- **Pessoas de verdade nas cenas** (ilustradas, já que este ambiente não tem uma ferramenta de geração de fotos e usar fotos de terceiros da internet nos arquivos do site violaria direitos autorais): um gerador de personagens humanos com cabeça, pescoço, tronco em "V" (jaleco/scrub), braços com mãos, pernas com sapatos, 4 estilos de cabelo (curto, coque, longo, cacheado), tons de pele variados e crachá de identificação — usado nas cenas de hospital, atendimento e equipe, substituindo os bonecos-blob genéricos.

**Limitação importante, dita com transparência:** não tenho, neste ambiente, uma ferramenta de geração de imagens fotorrealistas, e não posso baixar/usar fotografias de bancos de imagem de terceiros para os arquivos do site (isso violaria direitos autorais do material entregue). O que entreguei é ilustração vetorial (SVG) de qualidade bem superior à rodada anterior — se, no futuro, você tiver fotos reais da equipe/clínica, elas podem substituir essas ilustrações diretamente nos mesmos espaços do layout.

---

## 8. Quarta rodada: assets fornecidos pelo cliente (mascote + fotos reais)

O cliente enviou 8 imagens próprias — um novo mascote ilustrado ("Doutor Rex") e 7 fotografias de hospital/atendimento — pedindo para substituir as imagens do site por elas. Trabalho realizado:

### 8.1 Processamento do mascote
- Removida a faixa de texto "NOSSO NOVO MASCOTE" que vinha no topo do arquivo original.
- Fundo branco removido via chroma-key (limiar de luminosidade/saturação + suavização de borda), para poder usar o personagem como um "adesivo" recortado.
- **Favicon** e **logo-mark** da navbar gerados a partir de um recorte da cabeça do mascote, compostos em selo circular.
- **Hero**: o mascote recortado foi montado sobre um cartão arredondado cor de osso com sombra suave, para não "flutuar" sem contexto sobre o gradiente escuro do hero.
- **Fundo do site e do rodapé**: gerado um padrão repetido com este mesmo mascote (não mais um desenho meu), em tom acinzentado/dessaturado e opacidade baixa — mesmo conceito pedido anteriormente ("vários Doutor Rex no fundo, em tom mais próximo do cinza"), agora usando a arte oficial do cliente.

### 8.2 Fotografias
As 7 fotos foram distribuídas pelas seções do site:

| Foto enviada | Onde foi usada |
|---|---|
| Fachada da clínica | Vitrine (carrossel) — "Estrutura completa" |
| Consulta médica | Vitrine (carrossel) — "Atendimento humanizado" |
| Corredor com equipe | Vitrine (carrossel) — "Equipe especializada" |
| Centro cirúrgico | Vitrine (carrossel) — "Alta complexidade" |
| Coração nas mãos | Card "Missão" |
| Recepção | Card "Visão" |
| Médica com paciente idosa | Card "Valores" |

Como as fotos não têm mais legenda "gravada" na própria imagem (ao contrário das ilustrações anteriores), as legendas do carrossel agora são feitas em HTML/CSS (`carousel-caption` do Bootstrap), o que também as torna editáveis sem precisar mexer em imagem nenhuma.

### 8.3 Limitação de resolução (importante)
Das 7 fotos, 6 vieram em resolução bem baixa (147px de altura). Para exibi-las em áreas maiores do site, foram ampliadas com reamostragem de alta qualidade (Lanczos) e um leve realce de nitidez (unsharp mask) para atenuar a perda de definição — mas um upscale nunca recupera detalhe que a imagem original não tinha. Caso versões maiores dessas fotos existam, recomendo substituí-las para um resultado mais nítido; o arquivo de mais alta resolução entre as sete (usado no card "Valores") ilustra bem a diferença de nitidez.



