# Roteiro de demonstração — 5 minutos

Roteiro para apresentar o Prontuário IA a uma clínica. A ordem importa: o
diferencial só aparece quando a nota chega ao sistema que o cliente já usa.

## Antes de começar (10 minutos antes da reunião)

```bash
./scripts/setup.sh          # macOS/Linux
.\scripts\setup.ps1         # Windows
```

Escolha a modalidade **Conector** quando o script perguntar — é a que demonstra o
argumento principal. Ao final, ele imprime os endereços e os dois logins.

1. Entre como **administrador** e abra **Configurações**:
   - Cadastre a chave da OpenAI (Transcrição automática).
   - Em Exportação para o EMR, cadastre `http://emr-demo:8080/webhook` e o segredo
     que o script imprimiu. Clique em **Enviar evento de teste** e confirme o "200".
2. Abra `http://localhost:9080` em uma segunda janela e deixe-a visível ao lado —
   é o "EMR do cliente".
3. Saia e entre como **médico**. Deixe a tela em Atendimentos.

Se a reunião for por vídeo, compartilhe as duas janelas lado a lado.

## O roteiro

### 1. O problema (30 s)

"O médico gasta de 5 a 10 minutos digitando depois de cada consulta. Não vamos
trocar o prontuário de vocês — vamos tirar essa digitação do caminho."

### 2. Abrir o atendimento (30 s)

**Novo atendimento** → selecione o paciente → escolha o modelo da especialidade
(Cardiologia, Pediatria…). Diga: *"o modelo muda o que a IA procura na consulta —
o que se espera de uma nota de cardiologia não é o que se espera de uma pediátrica."*

### 3. Consentimento (20 s)

Mostre a tela de consentimento e leia o roteiro em voz alta. Diga: *"nada é
gravado — a conversa vira texto na hora e o áudio é descartado. E a transcrição
só começa depois do aceite, que fica registrado com data e hora."*

### 4. A consulta (60 s)

Duas opções:

- **Com voz real**: clique em **Iniciar escuta** e converse 1–2 minutos com
  alguém da sala. É mais convincente, mas depende de microfone.
- **Consulta simulada**: clique em **Simular consulta**. As falas de uma consulta
  fictícia entram uma a uma, como se viessem da transcrição. Use esta se a reunião
  for remota ou se o ambiente for desconhecido.

Aponte duas coisas enquanto a consulta acontece:

1. **O prontuário se preenche sozinho** à direita, campo a campo, com a etiqueta
   **IA**. *"O médico conversa olhando para o paciente, não para o teclado."*
2. **O contador de dados protegidos** e as etiquetas "nome protegido" e "telefone
   protegido" na transcrição: a consulta de exemplo dita um CPF, um celular e o nome
   da filha. *"Esses dados são retirados aqui no servidor antes de qualquer coisa
   chegar à IA."*

Edite um campo no meio da consulta: o texto do médico não é sobrescrito, e a
novidade da IA aparece como sugestão para aceitar ou não.

A transcrição aparece como diálogo. Clique no rótulo de um trecho (**Médico**,
**Paciente**) para corrigir quem falou: *"a IA deduz pelo conteúdo, o médico tem
a palavra final — e ela deixa de confundir a opinião do paciente com a hipótese do
médico."*

Clique em **origem** ao lado de um campo: os trechos da conversa de onde a IA
tirou aquele conteúdo ficam destacados. *"O médico não precisa confiar na IA —
ele confere na própria fala."*

Selecione com o mouse uma palavra da transcrição (o nome de um lugar, por exemplo)
e clique em **Proteger**: ela some da transcrição e não chega mais à IA. *"Se a
proteção automática deixar passar algo, o médico corrige na hora."*

### 5. A revisão (90 s) — o ponto alto

Clique em **Encerrar consulta**. A tela já está na revisão, com o rascunho completo:

1. Percorra os campos preenchidos.
2. Edite um campo qualquer. A etiqueta **"editado por você"** aparece ao lado.
3. Clique em **Comparar com a sugestão da IA** e mostre os dois textos.

Diga: *"a IA nunca salva sozinha. O que fica no registro é o que o médico
confirmou — e o sistema prova qual parte foi revisada."*

4. Gere uma **receita** e um **pedido de exame** na seção Documentos. Edite uma
   linha e salve.

### 6. A entrega ao EMR (60 s) — o diferencial

Clique em **Confirmar e finalizar** e aponte para a outra janela: a nota aparece
no EMR de demonstração em até dois segundos, com o selo **assinatura conferida**.

Diga: *"isso é o que muda a conversa. A nota não ficou aqui — ela entrou no
sistema de vocês, assinada, sem ninguém copiar e colar. É o mesmo webhook que
vamos apontar para o EMR real de vocês."*

Se perguntarem como: abra `docs/integracao-emr.md` e mostre o payload e a
verificação de assinatura. São duas páginas.

### 7. Fechamento (30 s)

Volte para Atendimentos e mostre os indicadores no topo: tempo de consulta
documentado e o percentual estimado de economia. Deixe claro que a linha de base
de digitação manual é configurável — *"esse número precisa ser o de vocês, não o
nosso."*

Se o interlocutor for de compliance ou TI, entre como administrador e mostre a
**trilha de auditoria**: quem acessou qual atendimento, quando, sem conteúdo
clínico exposto.

## Perguntas que costumam aparecer

| Pergunta | Resposta curta |
|---|---|
| "Precisamos trocar nosso prontuário?" | Não. A modalidade Conector é um add-on: a nota vai para o sistema de vocês. |
| "E se a IA errar?" | Nada é salvo sem revisão. A tela mostra o que a IA sugeriu e o que o médico mudou. |
| "E se a IA cair?" | O médico documenta manualmente no mesmo formulário; e há o botão de reprocessar. |
| "Onde ficam os áudios?" | Em lugar nenhum: não há gravação. O áudio vira texto durante a consulta e é descartado. |
| "A OpenAI vê o nome do paciente?" | O modelo que preenche o prontuário recebe só texto sanitizado: nome, CPF, telefone, endereço e documentos são trocados por marcadores aqui no servidor. A transcrição em si ouve a voz, por isso recomendamos retenção zero com a OpenAI. |
| "Quem paga a OpenAI?" | A chave é da clínica, cadastrada por vocês. O custo é medido por consulta, sem intermediação. |
| "Isso atende à LGPD?" | Consentimento registrado, sem gravação de áudio, dados pessoais sanitizados antes da IA, trilha de auditoria imutável e acesso por papel. O contrato precisa deixar explícito que, após a exportação, o registro legal é o do EMR de destino. |
| "Dá para integrar com o nosso sistema?" | Webhook de saída e API de entrada, ambos documentados em `docs/integracao-emr.md`. Sem conector proprietário. |

## O que não prometer

- Assinatura digital ICP-Brasil: não está implementada.
- Separação de vozes pelo áudio: quem fala é atribuído pelo conteúdo da fala, e o médico corrige quando a IA erra.
- Anonimização total: dado pseudonimizado continua sendo dado pessoal pela LGPD.
- Agenda, faturamento e TISS: fora do escopo, por decisão de produto.
