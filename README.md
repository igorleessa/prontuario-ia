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

### Ambiente completo (Docker)

```bash
docker compose up --build
```

- API: http://localhost:8080 (Swagger em `/swagger`)
- Frontend: http://localhost:4200
- MinIO console: http://localhost:9001

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

## Estado atual

Fase 0 (fundação) concluída: solution .NET com as quatro camadas, modelo de dados completo com migration inicial, autenticação JWT, resolver de modalidade, workspace Angular e Docker Compose.

Os provedores de STT e LLM ainda não foram escolhidos (ver [especificação](docs/especificacao-mvp.md), seção 12). `PlaceholderTranscriptionService` e `PlaceholderClinicalNoteGenerator` implementam as interfaces e lançam `NotImplementedException` — basta trocar o registro em `DependencyInjection.cs` quando o fornecedor for definido.
