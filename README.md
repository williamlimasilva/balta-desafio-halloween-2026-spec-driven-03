<img width="100%" alt="Halloween 2026" src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/banner.jpg" />

## 🎃 Halloween - Desafio 3

Este repositório contém um encurtador de URLs desenvolvido como parte do
Desafio 3 do Halloween 2026 da [balta.io](https://balta.io), aplicando
Spec-Driven Development com SpecKit.

## O que foi implementado

- API para criar links curtos e redirecionar para as URLs originais.
- Personalização de códigos, expiração de links e consulta de estatísticas.
- Persistência em SQLite com Entity Framework Core.
- Testes unitários e de integração.

<img src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/03.png" width="200" alt="Badge do Desafio 3" />

## Sobre a aplicação

O projeto usa .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core e
SQLite.

## Requisitos

- .NET 10 SDK.
- PowerShell.

## Executar localmente

Abra o PowerShell e vá para a raiz do repositório:

```powershell
Set-Location 'C:\Private\Courses\Balta\desafio 3\balta-desafio-halloween-2026-spec-driven-03'
```

Execute os comandos abaixo **a partir da raiz**:

```powershell
# Restaurar as dependências da solução
dotnet restore

# Restaurar a ferramenta local do Entity Framework Core
dotnet tool restore

# Criar uma chave temporária para desenvolvimento local
$env:LinkSecurity__ClientIpFingerprintKey = [Convert]::ToBase64String(
    [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
)

# Aplicar as migrações do banco SQLite (opcional: a API também migra ao iniciar)
dotnet ef database update --project src\ShortLinks.Infrastructure --startup-project src\ShortLinks.Api

# Habilitar a documentação interativa (Scalar roda só em Development)
$env:ASPNETCORE_ENVIRONMENT = 'Development'

# Iniciar a API em http://localhost:5000
dotnet run --project src\ShortLinks.Api --urls http://localhost:5000
```

Com a API em execução, acesse a documentação interativa em
[http://localhost:5000/scalar](http://localhost:5000/scalar).

Para encerrar a aplicação, pressione **Ctrl+C** no terminal em que `dotnet run`
está em execução.

### Chave de segurança

A API exige a configuração `LinkSecurity__ClientIpFingerprintKey`, que deve ser
uma chave Base64 com pelo menos 32 bytes. O comando acima gera uma chave
temporária para desenvolvimento local. Para preservar a identificação por IP
entre reinicializações, use a mesma chave. Em produção, configure uma chave
estável por meio de um mecanismo seguro; não a salve no repositório.

### Comandos úteis

Execute-os a partir da raiz do repositório:

```powershell
# Compilar a solução
dotnet build

# Executar os testes unitários e de integração
dotnet test
```

Para iniciar a API de outro diretório, informe o caminho para o projeto
`src\ShortLinks.Api\ShortLinks.Api.csproj` no parâmetro `--project`.

## Estrutura da solução

- `src\ShortLinks.Api`: endpoints HTTP e inicialização da aplicação.
- `src\ShortLinks.Application`: regras de negócio e serviços de links.
- `src\ShortLinks.Infrastructure`: persistência SQLite e migrações do EF Core.
- `tests\ShortLinks.UnitTests`: testes unitários.
- `tests\ShortLinks.IntegrationTests`: testes de integração da API.
- `specs\001-create-short-links`: especificação, plano, contrato e guia detalhado.

O arquivo do banco de dados local fica em
`src\ShortLinks.Api\Data\shortlinks.db`, conforme a configuração atual.
O caminho é relativo ao diretório da aplicação; em produção, configure
`Links:DatabasePath` para um caminho persistente e gravável fora dos arquivos
publicados da aplicação.
Os detalhes do fluxo funcional e cenários de validação estão em
[specs/001-create-short-links/quickstart.md](specs/001-create-short-links/quickstart.md).
