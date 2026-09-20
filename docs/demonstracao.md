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

Mostre a tela de consentimento. Diga: *"a gravação só começa depois do aceite, e
o aceite fica registrado com data e hora. Isso é exigência da LGPD, não enfeite."*

### 4. A consulta (60 s)

Duas opções:

- **Com áudio real**: grave 30–60 segundos simulando uma consulta com alguém da
  sala. É mais convincente, mas depende de microfone.
- **Consulta simulada**: clique em **Simular consulta**. Roda o pipeline inteiro
  sobre uma gravação fictícia de exemplo. Use esta se a reunião for remota ou se
  o ambiente for desconhecido.

Enquanto processa: *"a transcrição e a extração rodam em segundo plano — o médico
não fica olhando para uma ampulheta."*

### 5. A revisão (90 s) — o ponto alto

Quando o rascunho aparecer:

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
| "Onde ficam os áudios?" | Em storage S3-compatível da clínica, apagados automaticamente após o prazo de retenção configurado. |
| "Quem paga a OpenAI?" | A chave é da clínica, cadastrada por vocês. O custo é medido por consulta, sem intermediação. |
| "Isso atende à LGPD?" | Consentimento registrado, trilha de auditoria imutável, acesso por papel e retenção definida. O contrato precisa deixar explícito que, após a exportação, o registro legal é o do EMR de destino. |
| "Dá para integrar com o nosso sistema?" | Webhook de saída e API de entrada, ambos documentados em `docs/integracao-emr.md`. Sem conector proprietário. |

## O que não prometer

- Assinatura digital ICP-Brasil: não está implementada.
- Transcrição em tempo real durante a fala: o processamento é em lote, ao final.
- Agenda, faturamento e TISS: fora do escopo, por decisão de produto.
