# Arquitetura

```text
Desktop / Tablet / Celular
            |
       Site responsivo
            |
      ASP.NET Core API
            |
       Services / EF Core
            |
       MySQL / MariaDB
```

A aplicação Web é servida pelo próprio ASP.NET Core. O frontend chama endpoints REST do mesmo domínio, simplificando CORS e implantação.

Camadas:
- Apresentação: `wwwroot`;
- API: `Controllers`;
- Serviços: `Services`;
- Dados: `Data`;
- Entidades: `Models`;
- Persistência: MySQL/MariaDB.
