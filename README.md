# Prontuário IA

Camada de IA que ouve a consulta médica, transcreve e devolve a nota clínica
pronta para revisão — acoplável ao prontuário eletrônico que a clínica já usa,
ou operando como prontuário próprio.

O médico revisa e confirma. A IA nunca salva nem exporta sozinha.

## As duas modalidades

O mesmo núcleo (consentimento → captura → transcrição → extração estruturada →
revisão) atende dois modelos de negócio, escolhidos por configuração da clínica
(`Clinica.ModoOperacao`) — sem build nem deploy separado:

| | **Integrado** | **Conector** |
|---|---|---|
| Onde vive o prontuário definitivo | Aqui | No EMR que a clínica já usa |
| Cadastro de paciente | Completo | Referência ao paciente do EMR (ID/CPF) |
| Saída da revisão | Prontuário nativo, assinado e travado | Nota enviada por webhook assinado, PDF ou cópia |
| Para quem | Consultório sem EMR | Clínica com EMR consolidado que só quer a camada de IA |

## O que o sistema faz hoje

**Núcleo clínico**
- Consentimento do paciente registrado com data e hora antes de qualquer gravação.
- Captura de áudio no navegador, transcrição (Whisper) e extração estruturada
  (GPT-4o com Structured Outputs) em segundo plano.
- Tela de revisão campo a campo, com etiqueta no que o médico alterou e
  comparação com a sugestão original da IA.
- Modelos de nota por especialidade (clínica geral, cardiologia, pediatria,
  ortopedia, psiquiatria, ginecologia) e preferências de redação por médico.
- Receita, pedido de exame, atestado e encaminhamento redigidos a partir da
  mesma consulta, editáveis e exportáveis em PDF.
- Falha da IA não bloqueia nada: o formulário continua preenchível à mão, e há
  reprocessamento sem duplicar o atendimento.

**Integração (modalidade Conector)**
- Webhook assinado em HMAC-SHA256, com retentativas e status de entrega visível
  ao médico.
- API de entrada para o EMR abrir o atendimento a partir do paciente que ele já tem.
- Nota em PDF e cópia formatada, para EMRs sem API.
- EMR de demonstração incluído no ambiente local, que recebe a nota ao vivo.
- Documentação de integração: [`docs/integracao-emr.md`](docs/integracao-emr.md).

**Segurança e conformidade**
- Trilha de auditoria imutável de todo acesso a dado clínico, com tela própria
  para o administrador.
- Papéis distintos: o médico atende, o administrador configura a clínica.
- Prontuário confirmado é somente leitura — correção posterior exige adendo.
- Chave de IA e segredo de webhook cifrados em repouso; chave de integração
  guardada como hash.
- Expurgo automático do áudio bruto após o prazo de retenção configurado.

**Ainda não implementado**: assinatura digital ICP-Brasil, transcrição em tempo
real durante a fala, agenda, faturamento e TISS.

## Stack

| Camada | Tecnologia |
|---|---|
| Frontend | Angular 18 (standalone + signals), SCSS |
| Backend | ASP.NET Core 8 (Clean Architecture), JWT |
| Banco | PostgreSQL 16 + EF Core |
| Storage de áudio | MinIO (S3-compatible) |
| IA | OpenAI Whisper + GPT-4o, atrás de interfaces trocáveis |
| Orquestração | Docker Compose |

A chave de IA é **da clínica**, cadastrada na própria interface: o custo por
consulta é medido e pago por ela, sem intermediação.

## Estrutura

```
backend/
  src/
    Prontuario.Domain/          entidades e enums
    Prontuario.Application/     interfaces e DTOs (sem dependência de infraestrutura)
    Prontuario.Infrastructure/  EF Core, serviços, saídas por modalidade
    Prontuario.Api/             Web API, controllers, autenticação
    Prontuario.EmrDemo/         EMR fictício para demonstrar a integração
  tests/
frontend/                       SPA Angular
docs/                           especificação, plano, integração e roteiro de demo
```

## Rodando localmente

O script pergunta os dados, gera as chaves, escreve o `.env`, sobe tudo no Docker
e semeia uma clínica, um médico, um administrador e um paciente.

```bash
./scripts/setup.sh          # macOS/Linux
.\scripts\setup.ps1         # Windows
```

Para recriar o banco do zero (apaga os volumes): `./scripts/setup.sh --recriar`.

Para não apresentar o sistema vazio, popule pacientes e atendimentos de teste:

```bash
python3 scripts/dados-demo.py            # rápido, sem custo de IA
python3 scripts/dados-demo.py --com-ia   # inclui consultas processadas pela IA de verdade
```

Ao final o script mostra os endereços e os dois logins:

- Frontend: http://localhost:4200
- API (Swagger): http://localhost:8080/swagger
- EMR de demonstração: http://localhost:9080
- MinIO console: http://localhost:9001

O `.env` gerado contém segredos e está no `.gitignore` — não versione.

### Subindo manualmente

Com o `.env` já criado: `docker compose up -d --build`.

### Desenvolvimento

```bash
# backend
cd backend && dotnet run --project src/Prontuario.Api

# migrations
cd backend && dotnet ef database update \
  --project src/Prontuario.Infrastructure --startup-project src/Prontuario.Api

# frontend
cd frontend && npm install && npm start
```

O `ng serve` usa `proxy.conf.json` para encaminhar `/api` ao backend em
`localhost:8080` — ajuste se tiver mudado a porta no setup.

### Variáveis que valem conhecer

| Variável | Para que serve |
|---|---|
| `SEED_MODO_OPERACAO` | `Integrado` ou `Conector` na clínica semeada |
| `DEMONSTRACAO_HABILITADA` | Libera o botão "Simular consulta", que roda o pipeline sobre uma consulta fictícia |
| `AUDIO_RETENCAO_DIAS` | Dias que o áudio bruto fica guardado depois de transcrito (0 desliga o expurgo) |
| `MINUTOS_DOCUMENTACAO_MANUAL` | Linha de base usada no cálculo de tempo economizado |
| `EMR_DEMO_SECRET` | Segredo que o EMR de demonstração usa para conferir a assinatura |

## Documentação

- [Roteiro de demonstração](docs/demonstracao.md) — apresentação de 5 minutos
- [Integração com o EMR do cliente](docs/integracao-emr.md) — webhook e API
- [Especificação do MVP](docs/especificacao-mvp.md) — requisitos e modelo de dados
- [Plano de desenvolvimento](docs/plano-desenvolvimento.md) — fases e riscos
