# Integração com o EMR do cliente (Modalidade Conector)

Guia técnico para acoplar o Prontuário IA a um prontuário eletrônico já existente.
A camada de IA não substitui o sistema do cliente: ela ouve a consulta, gera a nota
clínica, o médico revisa, e a nota volta para o EMR de origem.

```
EMR do cliente  ──(1) abre atendimento por API──►  Prontuário IA
                                                     │
                                          médico grava, IA transcreve,
                                              médico revisa e confirma
                                                     │
EMR do cliente  ◄──(2) webhook assinado com a nota ──┘
```

Nenhuma das duas pontas é obrigatória: um cliente pode usar só o webhook (abrindo os
atendimentos pela nossa tela) ou só a API de entrada (exportando a nota por PDF/cópia).

## 1. Abrir um atendimento a partir do EMR

Gere a chave em **Configurações → Exportação para o EMR → Gerar chave**. Ela aparece
uma única vez; guarde-a no cofre de segredos do cliente.

```http
POST /api/integracao/atendimentos
X-Api-Key: pia_3f8c1b…
Content-Type: application/json

{
  "idExternoEmr": "PAC-99812",
  "nome": "Maria Souza",
  "cpf": "123.456.789-00",
  "dataNascimento": "1979-04-12",
  "medicoEmail": "ana@clinica.com.br"
}
```

```json
201 Created
{
  "atendimentoId": "0f1c…",
  "pacienteId": "7a44…",
  "url": "https://prontuario.clinica.com.br/atendimentos/0f1c…"
}
```

| Campo | Obrigatório | Observação |
|---|---|---|
| `idExternoEmr` | sim, ou `cpf` | Chave de correlação com o registro do EMR. Reaproveita o paciente se já existir. |
| `cpf` | sim, se não houver `idExternoEmr` | |
| `nome` | não | Sem ele, o paciente aparece como "Paciente PAC-99812". |
| `dataNascimento` | não | Formato `AAAA-MM-DD`. |
| `medicoEmail` | não | Sem ele, o atendimento vai para o primeiro médico da clínica. |

O EMR abre `url` em nova aba (ou iframe) e o médico segue o fluxo de consentimento,
gravação e revisão. Nenhum dado clínico é devolvido por esta rota.

Para validar a chave na implantação, sem criar nada: `GET /api/integracao/ping`.

## 2. Receber a nota revisada (webhook)

Cadastre a URL e um segredo em **Configurações → Exportação para o EMR**. Quando o
médico confirma a revisão, enviamos:

```http
POST https://emr-do-cliente/api/notas
Content-Type: application/json
X-Prontuario-Evento: nota.exportada
X-Prontuario-Timestamp: 1758312000
X-Prontuario-Assinatura: sha256=9b1c…

{
  "evento": "nota.exportada",
  "ocorridoEm": "2026-09-20T14:22:31Z",
  "atendimento": {
    "id": "0f1c…",
    "dataHora": "2026-09-20T13:40:00Z",
    "medico": "Dra. Ana Lima"
  },
  "paciente": {
    "nome": "Maria Souza",
    "cpf": "123.456.789-00",
    "idExternoEmr": "PAC-99812"
  },
  "nota": {
    "formato": "texto",
    "conteudo": "QUEIXA PRINCIPAL\n…"
  }
}
```

### Conferindo a assinatura

A assinatura é o HMAC-SHA256 de `"{timestamp}.{corpo bruto}"`, em hexadecimal
minúsculo, prefixado por `sha256=`. Assinar o timestamp junto com o corpo permite
recusar uma requisição antiga reapresentada por terceiros.

```csharp
var esperado = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
    Encoding.UTF8.GetBytes(segredo),
    Encoding.UTF8.GetBytes($"{timestamp}.{corpo}"))).ToLowerInvariant();

var valido = CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(recebida));
```

```javascript
const esperado = 'sha256=' + crypto
  .createHmac('sha256', segredo)
  .update(`${timestamp}.${corpoBruto}`)
  .digest('hex');
```

Use sempre o **corpo bruto** da requisição: reserializar o JSON muda bytes e invalida a
assinatura.

### Reentrega

Respondemos ao médico com o resultado do envio. Em erro de rede ou resposta 5xx,
tentamos três vezes (imediata, +2s, +5s). Uma resposta 4xx não é repetida — é
configuração errada, não indisponibilidade.

Se o envio falhar, a nota fica com status `Revisada` e o médico vê na tela do
atendimento o motivo, com os botões **Enviar ao EMR** (reenvio manual), **Baixar PDF** e
**Copiar nota**. O registro nunca se perde por indisponibilidade do destino.

Esperamos HTTP 2xx para considerar entregue. Responda rápido e processe de forma
assíncrona do seu lado: o médico está esperando na tela.

## 3. Outras formas de exportação

| Forma | Onde | Quando usar |
|---|---|---|
| Copiar texto | botão na tela do atendimento | EMR sem API, colagem manual |
| PDF | `GET /api/atendimentos/{id}/nota.pdf` | anexo ao prontuário, impressão |
| Webhook | automático na confirmação | integração de verdade |

## 4. Testar sem escrever código

O ambiente local sobe um EMR fictício em `http://localhost:9080` que recebe o webhook,
confere a assinatura e mostra a nota chegando. Para apontá-lo:

- Webhook: `http://emr-demo:8080/webhook`
- Segredo: o valor de `EMR_DEMO_SECRET` no `.env` (o `setup.sh` imprime ao final)

O botão **Enviar evento de teste** na tela de Configurações dispara um payload fictício,
sem dado real de paciente, e mostra a resposta do destino.

## 5. Segurança

- Todo tráfego em produção deve ser HTTPS (RNF01).
- A chave de integração é guardada como hash SHA-256; o segredo do webhook é cifrado
  com Data Protection. Nenhum dos dois é devolvido por consulta.
- Gerar uma nova chave invalida a anterior imediatamente.
- A partir da exportação, o registro legal definitivo é o do EMR de destino — isso
  precisa estar explícito no contrato com a clínica.
