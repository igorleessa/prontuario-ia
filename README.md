# Prontuário IA

Camada de IA que ouve a consulta médica e preenche o prontuário enquanto ela
acontece, sem gravar o áudio, deixando a nota pronta para revisão — acoplável ao
prontuário eletrônico que a clínica já usa, ou operando como prontuário próprio.

O médico revisa e confirma. A IA nunca salva nem exporta sozinha.

## As duas modalidades

O mesmo núcleo (consentimento → transcrição ao vivo → sanitização → preenchimento
dos campos → revisão) atende dois modelos de negócio, escolhidos por configuração da clínica
(`Clinica.ModoOperacao`) — sem build nem deploy separado:

| | **Integrado** | **Conector** |
|---|---|---|
| Onde vive o prontuário definitivo | Aqui | No EMR que a clínica já usa |
| Cadastro de paciente | Completo | Referência ao paciente do EMR (ID/CPF) |
| Saída da revisão | Prontuário nativo, assinado e travado | Nota enviada por webhook assinado, PDF ou cópia |
| Para quem | Consultório sem EMR | Clínica com EMR consolidado que só quer a camada de IA |

## O que o sistema faz hoje

**Núcleo clínico**
- Consentimento do paciente registrado com data e hora antes de qualquer escuta.
- **Transcrição ao vivo, sem gravação**: o navegador fala direto com a Realtime API
  da OpenAI (WebRTC, com chave temporária emitida pelo backend), e o áudio nunca
  passa pelo servidor nem é guardado. Se o tempo real não estiver disponível, a
  escuta segue por trechos de poucos segundos, transcritos em memória e descartados.
- **Prontuário preenchido durante a consulta**: o rascunho é refeito conforme a
  fala chega, e os campos se preenchem na tela. O que o médico edita nunca é
  sobrescrito — a novidade da IA aparece como sugestão para aceitar ou não.
- **Origem de cada campo**: a IA indica de quais falas tirou o conteúdo, e o
  médico vê esses trechos destacados na transcrição com um clique. Citação para
  trecho que não existe é descartada.
- **Dois modelos**: um rápido e barato refaz o rascunho durante a consulta
  (padrão `gpt-4o-mini`); o completo faz a passada final ao encerrar.
- Tela de revisão campo a campo, com etiqueta no que veio da IA e no que o médico
  alterou, e comparação com a sugestão original.
- Modelos de nota por especialidade (clínica geral, cardiologia, pediatria,
  ortopedia, psiquiatria, ginecologia) e preferências de redação por médico.
- Receita, pedido de exame, atestado e encaminhamento redigidos a partir da
  mesma consulta, editáveis e exportáveis em PDF.
- Falha da IA não bloqueia nada: o formulário continua preenchível à mão, e o
  rascunho pode ser refeito sobre a transcrição sem duplicar o atendimento.

**Privacidade: sanitização antes da IA**
- Camada local (sem nenhum serviço externo) entre a transcrição e todo o resto:
  modelo de texto, banco e logs. Ver `backend/src/Prontuario.Application/Privacidade`.
- Três camadas de detecção: dicionário do atendimento (nome, CPF, nascimento e
  contato do paciente, vindos do cadastro, tolerante a variações de grafia),
  padrões com validação (CPF e CNS com dígito verificador, telefone, e-mail, CEP,
  endereço, números longos) e nomes de terceiros — por contexto de fala ("minha
  filha Mariana", "seu João") e por um léxico local de prenomes e sobrenomes
  brasileiros ("a Mariana Costa trouxe os exames").
- **Reconhecimento de nomes por modelo local** (`servicos/privacidade-ner`): o
  modelo de português do spaCy roda num container sem porta publicada e aponta
  nomes que nenhuma lista conhece ("o Gervásio, meu cunhado"). O backend filtra o
  que o modelo erra em contexto clínico: epônimos ("doença de Parkinson"), verbos
  ("Moro em…") e palavras comuns no início da frase ("Dores nas costas").
  Pessoas por padrão; lugares e organizações por `NER_CATEGORIAS=PER,LOC,ORG`.
- **Falha sem vazamento**: com o reconhecedor fora do ar, a fala continua sendo
  guardada (com as demais camadas) e fica pendente. Nada vai à IA até ele voltar
  e passar pelos trechos pendentes.
- **Correção pelo médico**: selecionar um trecho da transcrição e marcar como dado
  pessoal. Ele some da transcrição já guardada, vale para as próximas falas e
  para os documentos, e fica cifrado no banco (é o próprio dado pessoal).
- Base de teste de privacidade (`CorpusPrivacidadeTests`): consultas fictícias com
  o que não pode vazar e o que não pode sumir. Vazamento encontrado em uso vira
  uma linha nela.
- Lista de proteção médica: epônimos (Parkinson, Murphy, Chagas…) e números
  clínicos (doses, sinais vitais, "1.200.000 UI") não são apagados.
- Os serviços de IA só aceitam o tipo `TextoSanitizado`, que só o sanitizador cria:
  mandar texto bruto ao provedor vira erro de compilação.
- No banco fica só a transcrição sanitizada. Os dados do paciente voltam à tela a
  partir do cadastro; os de terceiros ficam apagados de vez.
- A resposta da IA é conferida pelo mesmo detector: dado pessoal nela gera alerta
  no log (só a categoria, nunca o valor).

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
- Nenhum áudio é armazenado em lugar algum: não há storage de arquivos.

**Ainda não implementado**: assinatura digital ICP-Brasil, identificação de quem
fala na transcrição,
agenda, faturamento e TISS.

## Stack

| Camada | Tecnologia |
|---|---|
| Frontend | Angular 18 (standalone + signals), SCSS |
| Backend | ASP.NET Core 8 (Clean Architecture), JWT |
| Banco | PostgreSQL 16 + EF Core |
| IA | OpenAI Realtime (gpt-4o-transcribe) + GPT-4o, atrás de interfaces trocáveis |
| Privacidade | Sanitização em .NET + reconhecimento de nomes local (Python, spaCy `pt_core_news_md`) |
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
servicos/privacidade-ner/       reconhecimento de nomes local (Python, spaCy)
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
| `DEMONSTRACAO_HABILITADA` | Libera o botão "Simular consulta", que envia as falas de uma consulta fictícia como se viessem da transcrição |
| `NER_CATEGORIAS` | Entidades que o reconhecedor de nomes retira: `PER` (padrão), `LOC`, `ORG` |
| `TRANSCRICAO_TEMPO_REAL` | `false` força o envio por trechos, para redes que bloqueiam WebRTC com a OpenAI |
| `MINUTOS_DOCUMENTACAO_MANUAL` | Linha de base usada no cálculo de tempo economizado |
| `EMR_DEMO_SECRET` | Segredo que o EMR de demonstração usa para conferir a assinatura |

## Documentação

- [Roteiro de demonstração](docs/demonstracao.md) — apresentação de 5 minutos
- [Integração com o EMR do cliente](docs/integracao-emr.md) — webhook e API
- [Especificação do MVP](docs/especificacao-mvp.md) — requisitos e modelo de dados
- [Plano de desenvolvimento](docs/plano-desenvolvimento.md) — fases e riscos
