# JURASSIHEALTH — PIM IV

Aplicação Web responsiva para gestão clínica, construída em ASP.NET Core 8, Entity Framework Core e MySQL/MariaDB.

## Decisão sobre acesso mobile

Conforme orientação do professor orientador, o requisito de uso em dispositivos móveis é atendido pelo próprio site responsivo. Não há aplicativo Android separado. A mesma aplicação Web funciona em desktop, tablet e celular.

## Principais recursos

- cadastro e login de paciente;
- perfis paciente, médico, secretaria e administrador;
- agenda médica;
- agendamento e cancelamento;
- documentos médicos;
- dashboard administrativo;
- API REST;
- BCrypt;
- tokens assinados;
- limite de tentativas de login;
- procedures, triggers, view e auditoria;
- Docker;
- GitHub Actions;
- configuração de deploy no Render;
- interface responsiva.

## Executar localmente

1. Importe `database/database-pim4.sql`.
2. Configure as variáveis:
   - `ConnectionStrings__Default`
   - `Auth__SecretKey`
   - `AdminPadrao__Email`
   - `AdminPadrao__SenhaHash`
3. Execute:

```bash
dotnet restore
dotnet run
```

Abra `http://localhost:5000`.

## Render

O projeto inclui `Dockerfile` e `render.yaml`. No Render, cadastre as quatro variáveis acima. O endpoint `/health` pode ser usado para health check.

## Documentação do PIM

Veja a pasta `docs` e `PIM_IV_CHECKLIST.md`.

## Atualização visual

Esta versão também inclui uma renovação visual da interface:

- remoção de emojis nos blocos principais;
- uso de ícones vetoriais;
- inclusão de imagens ilustrativas de hospital, atendimento e recepção;
- uso do mascote **DR Rex** como elemento de identidade visual;
- melhoria do layout responsivo para celular e desktop.


## Melhorias visuais e mobile (v3)

Esta versão substitui as imagens provisórias por artes melhores e melhora a experiência mobile:

- novas imagens PNG de alta qualidade para hospital, atendimento e recepção;
- novo mascote DR Rex em melhor qualidade;
- seções reorganizadas para o celular;
- versão em cartões para consultas no mobile;
- hero, galeria e destaque mobile com melhor hierarquia visual.


## Expansão visual e mascote fofo (v4)

Atualização com foco em apresentação visual:

- mascote DR Rex redesenhado em versão mais fofa;
- novas imagens para sala de espera, equipe médica, agendamento no celular e cuidado com paciente idosa;
- galeria ampliada com mais cartões visuais;
- hero atualizado com reforço da proposta mobile;
- assets prontos em `wwwroot/img`.
