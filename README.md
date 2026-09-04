# Prontuário IA

Prontuário eletrônico com IA que ouve a consulta médica e pré-preenche o formulário de atendimento.

Documentação de planejamento:
- [Plano de desenvolvimento](docs/plano-desenvolvimento.md)
- [Especificação do MVP](docs/especificacao-mvp.md)

## Duas modalidades de operação

O mesmo core de IA (consentimento → captura de áudio → transcrição → extração estruturada) atende dois modos, configurados por clínica (`Clinica.ModoOperacao`):

- **Integrado**: prontuário nativo completo, com assinatura e histórico do paciente.
- **Conector**: a IA gera a nota clínica e a exporta (copiar/PDF/webhook) para o EMR que a clínica já usa.

A seleção acontece em tempo de execução em `RegistroClinicoOutputResolver`, que delega para `ProntuarioNativoOutput` ou `ExportacaoEmrOutput`.

## Stack

| Camada | Tecnologia |
|---|---|
| Frontend | Angular 18, SCSS |
| Backend | ASP.NET Core 8 (Clean Architecture), JWT |
| Banco | PostgreSQL 16 + EF Core |
| Storage de áudio | MinIO (S3-compatible) |
| Orquestração | Docker Compose |

## Estrutura

```
backend/
  src/
    Prontuario.Domain/          entidades e enums
    Prontuario.Application/     interfaces e DTOs (sem dependência de infraestrutura)
    Prontuario.Infrastructure/  EF Core, serviços, saídas por modalidade
    Prontuario.Api/             Web API, controllers, autenticação
  tests/
frontend/                       SPA Angular
docs/                           plano e especificação
```

## Rodando localmente

### Setup automatizado (recomendado)

O script pergunta usuário e senha do banco, gera a chave JWT, escreve o `.env`, sobe tudo no Docker e semeia uma clínica, um médico e um paciente para você já conseguir logar.

**macOS / Linux:**

```bash
./scripts/setup.sh
```

**Windows (PowerShell):**

```powershell
.\scripts\setup.ps1
```

Para recriar o banco do zero (apaga os volumes):

```bash
./scripts/setup.sh --recriar      # macOS/Linux
.\scripts\setup.ps1 -Recriar      # Windows
```

Ao final o script mostra os endereços e o e-mail de login. Por padrão:

- Frontend: http://localhost:4200
- API (Swagger): http://localhost:8080/swagger
- MinIO console: http://localhost:9001

O `.env` gerado contém segredos e está no `.gitignore` — não versione.

Durante a execução você escolhe a **modalidade da clínica semeada** (Integrado ou Conector), o que permite testar os dois caminhos da aplicação. Para trocar depois, edite `SEED_MODO_OPERACAO` no `.env` e rode com `--recriar`.

### Subindo manualmente

Com o `.env` já criado:

```bash
docker compose up -d --build
```

### Desenvolvimento

Backend:

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/Prontuario.Api
```

Migrations:

```bash
cd backend
dotnet ef database update --project src/Prontuario.Infrastructure --startup-project src/Prontuario.Api
```

Frontend:

```bash
cd frontend
npm install
npm start
```

O `ng serve` usa `proxy.conf.json` para encaminhar `/api` ao backend em `localhost:8080` — se você mudou a porta da API no setup, ajuste esse arquivo.

## Estado atual

Fase 0 (fundação) concluída: solution .NET com as quatro camadas, modelo de dados completo com migration inicial, autenticação JWT, resolver de modalidade, workspace Angular, Docker Compose e scripts de setup para Windows e macOS/Linux.

As migrations são aplicadas automaticamente no start quando `Database__AplicarMigrationsNaInicializacao` está ligado (o Compose liga por padrão no ambiente local).

Os provedores de STT e LLM ainda não foram escolhidos (ver [especificação](docs/especificacao-mvp.md), seção 12). `PlaceholderTranscriptionService` e `PlaceholderClinicalNoteGenerator` implementam as interfaces e lançam `NotImplementedException` — basta trocar o registro em `DependencyInjection.cs` quando o fornecedor for definido.
