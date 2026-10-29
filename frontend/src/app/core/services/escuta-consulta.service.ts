import { Injectable, signal } from '@angular/core';
import { ModoTranscricao, SessaoTranscricao } from '../models/transcricao.model';

export type EstadoEscuta = 'parada' | 'conectando' | 'ouvindo' | 'pausada' | 'reconectando';

/** Para onde vai o que a escuta produz. Implementado pela tela do atendimento. */
export interface DestinoEscuta {
  /** Trecho que o provedor em tempo real terminou de transcrever. */
  texto(sequencia: number, texto: string): void;
  /** Modo por trechos: alguns segundos de áudio, para o servidor transcrever e descartar. */
  audio(sequencia: number, audio: Blob, extensao: string): void;
  /** Nova credencial de tempo real: a anterior só vale por alguns minutos. */
  renovarSessao(): Promise<SessaoTranscricao>;
  /** O tempo real caiu de vez e a escuta seguiu por trechos. */
  mudouDeModo(modo: ModoTranscricao, motivo: string): void;
}

/** Formatos do modo por trechos, em ordem de preferência. Safari não grava WebM e cai no MP4. */
const FORMATOS: { mimeType: string; extensao: string }[] = [
  { mimeType: 'audio/webm;codecs=opus', extensao: 'webm' },
  { mimeType: 'audio/ogg;codecs=opus', extensao: 'ogg' },
  { mimeType: 'audio/mp4', extensao: 'mp4' },
  { mimeType: 'audio/webm', extensao: 'webm' },
];

/** Abaixo disso o microfone está captando silêncio (nível de 0 a 1). */
const LIMIAR_SILENCIO = 0.04;
/** Trecho cujo pico não chega aqui não tinha fala: nem é enviado. */
const LIMIAR_FALA = 0.08;
/** Corte do trecho na primeira pausa depois deste tempo... */
const TRECHO_MINIMO_MS = 6_000;
/** ...ou à força neste, para o texto não demorar a aparecer. */
const TRECHO_MAXIMO_MS = 20_000;
const PAUSA_DE_FALA_MS = 700;
const TENTATIVAS_TEMPO_REAL = 2;

interface TrechoEmCaptura {
  gravador: MediaRecorder;
  partes: Blob[];
  inicio: number;
  pico: number;
}

/**
 * Escuta a consulta pelo microfone e a transforma em texto enquanto ela
 * acontece, sem gravar. Nada é acumulado: no modo tempo real o áudio sai do
 * navegador direto para o provedor (WebRTC) e volta como texto; no modo por
 * trechos cada pedaço de poucos segundos é entregue e esquecido em seguida.
 */
@Injectable({ providedIn: 'root' })
export class EscutaConsultaService {
  readonly estado = signal<EstadoEscuta>('parada');
  readonly modo = signal<ModoTranscricao | null>(null);

  /** Volume captado agora, de 0 a 1: mostra ao médico que o microfone está ouvindo. */
  readonly nivel = signal(0);

  /** Fala que o provedor em tempo real ainda está transcrevendo. */
  readonly falaEmAndamento = signal('');

  private destino: DestinoEscuta | null = null;
  private stream: MediaStream | null = null;
  private proximaSequencia = 0;

  private conexao: RTCPeerConnection | null = null;
  private canal: RTCDataChannel | null = null;
  private readonly itens = new Map<string, number>();
  private readonly parciais = new Map<string, string>();
  private quedaPendente: ReturnType<typeof setTimeout> | null = null;
  private tentativas = 0;

  private trecho: TrechoEmCaptura | null = null;
  private formato = FORMATOS[0]!;
  private silencioDesde: number | null = null;
  private relogioTrechos: ReturnType<typeof setInterval> | null = null;

  private contextoAudio: AudioContext | null = null;
  private quadroNivel: number | null = null;

  /** Abre o microfone e começa a transcrever. Lança se o microfone for negado. */
  async iniciar(sessao: SessaoTranscricao, destino: DestinoEscuta): Promise<void> {
    this.encerrarTudo();
    this.destino = destino;
    this.proximaSequencia = sessao.proximaSequencia;
    this.tentativas = 0;
    this.estado.set('conectando');

    try {
      // Cancelamento de eco, supressão de ruído e ganho automático limpam o
      // ruído de consultório e equilibram a voz de quem está mais longe.
      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      });
    } catch (erro) {
      this.estado.set('parada');
      throw erro;
    }

    this.medirNivel(this.stream);

    if (this.tempoRealDisponivel(sessao)) {
      try {
        await this.conectarTempoReal(sessao);
        this.modo.set('TempoReal');
        this.estado.set('ouvindo');
        return;
      } catch {
        destino.mudouDeModo('Trechos', 'Não foi possível conectar à transcrição em tempo real; seguindo por trechos.');
      }
    }

    this.iniciarTrechos();
    this.estado.set('ouvindo');
  }

  /** Para de ouvir sem soltar o microfone: conversa que não deve entrar no prontuário. */
  pausar(): void {
    if (this.estado() !== 'ouvindo') {
      return;
    }

    this.estado.set('pausada');
    this.stream?.getAudioTracks().forEach((faixa) => (faixa.enabled = false));

    if (this.modo() === 'Trechos') {
      this.fecharTrecho();
    }
  }

  retomar(): void {
    if (this.estado() !== 'pausada') {
      return;
    }

    this.stream?.getAudioTracks().forEach((faixa) => (faixa.enabled = true));
    this.estado.set('ouvindo');

    if (this.modo() === 'Trechos') {
      this.abrirTrecho();
    }
  }

  /**
   * Encerra a escuta. Espera o último trecho chegar ao destino - a frase que o
   * médico disse antes de clicar em "encerrar" não pode se perder.
   */
  async parar(): Promise<void> {
    if (this.estado() === 'parada') {
      return;
    }

    if (this.modo() === 'TempoReal' && this.canal?.readyState === 'open') {
      // Fecha a fala em andamento e dá ao provedor alguns segundos para devolvê-la.
      this.canal.send(JSON.stringify({ type: 'input_audio_buffer.commit' }));
      await this.aguardar(() => this.parciais.size === 0 && this.itensPendentes() === 0, 4_000);
    }

    if (this.modo() === 'Trechos') {
      await this.fecharTrecho();
    }

    this.encerrarTudo();
  }

  /** Solta o microfone na hora, sem entregar nada - ao sair da tela. */
  descartar(): void {
    this.destino = null;
    this.encerrarTudo();
  }

  // ---------- Tempo real (WebRTC direto com o provedor) ----------

  private tempoRealDisponivel(sessao: SessaoTranscricao): boolean {
    return (
      sessao.modo === 'TempoReal' &&
      !!sessao.chaveTemporaria &&
      !!sessao.urlConexao &&
      typeof RTCPeerConnection !== 'undefined'
    );
  }

  private async conectarTempoReal(sessao: SessaoTranscricao): Promise<void> {
    const conexao = new RTCPeerConnection();
    this.conexao = conexao;

    for (const faixa of this.stream!.getAudioTracks()) {
      conexao.addTrack(faixa, this.stream!);
    }

    const canal = conexao.createDataChannel('oai-events');
    this.canal = canal;
    canal.onmessage = (evento) => this.tratarEvento(evento.data);

    conexao.onconnectionstatechange = () => this.acompanharConexao(conexao);

    const oferta = await conexao.createOffer();
    await conexao.setLocalDescription(oferta);

    const resposta = await fetch(sessao.urlConexao!, {
      method: 'POST',
      body: oferta.sdp,
      headers: {
        Authorization: `Bearer ${sessao.chaveTemporaria}`,
        'Content-Type': 'application/sdp',
      },
    });

    if (!resposta.ok) {
      throw new Error(`O provedor recusou a conexão (${resposta.status}).`);
    }

    await conexao.setRemoteDescription({ type: 'answer', sdp: await resposta.text() });

    const aberto = await this.aguardar(() => canal.readyState === 'open', 10_000);
    if (!aberto) {
      throw new Error('O canal de eventos do provedor não abriu.');
    }
  }

  private tratarEvento(dados: string): void {
    let evento: { type?: string; item_id?: string; delta?: string; transcript?: string };
    try {
      evento = JSON.parse(dados);
    } catch {
      return;
    }

    const item = evento.item_id ?? '';

    switch (evento.type) {
      // A ordem em que os trechos sao fechados e a ordem da fala; a sequencia
      // e atribuida aqui, e nao quando o texto chega (que pode vir fora de ordem).
      case 'input_audio_buffer.committed':
        if (!this.itens.has(item)) {
          this.itens.set(item, this.proximaSequencia++);
        }
        break;

      case 'conversation.item.input_audio_transcription.delta':
        this.parciais.set(item, (this.parciais.get(item) ?? '') + (evento.delta ?? ''));
        this.atualizarFalaEmAndamento();
        break;

      case 'conversation.item.input_audio_transcription.completed': {
        const sequencia = this.itens.get(item) ?? this.proximaSequencia++;
        this.itens.delete(item);
        this.parciais.delete(item);
        this.atualizarFalaEmAndamento();

        const texto = (evento.transcript ?? '').trim();
        if (texto) {
          this.destino?.texto(sequencia, texto);
        }
        break;
      }

      case 'conversation.item.input_audio_transcription.failed':
        this.itens.delete(item);
        this.parciais.delete(item);
        this.atualizarFalaEmAndamento();
        break;
    }
  }

  private acompanharConexao(conexao: RTCPeerConnection): void {
    if (conexao !== this.conexao || this.estado() === 'parada') {
      return;
    }

    const estado = conexao.connectionState;

    if (estado === 'connected' && this.quedaPendente !== null) {
      clearTimeout(this.quedaPendente);
      this.quedaPendente = null;
    }

    // "disconnected" costuma ser oscilação de rede e se recupera sozinho;
    // só vira reconexão se persistir.
    if (estado === 'disconnected' && this.quedaPendente === null) {
      this.quedaPendente = setTimeout(() => {
        this.quedaPendente = null;
        if (conexao.connectionState !== 'connected') {
          void this.reconectar();
        }
      }, 5_000);
    }

    if (estado === 'failed') {
      void this.reconectar();
    }
  }

  private async reconectar(): Promise<void> {
    if (!this.destino || this.estado() === 'parada' || this.estado() === 'reconectando') {
      return;
    }

    const estavaPausada = this.estado() === 'pausada';
    this.estado.set('reconectando');
    this.fecharConexao();

    while (this.tentativas < TENTATIVAS_TEMPO_REAL && this.destino) {
      this.tentativas++;
      try {
        const sessao = await this.destino.renovarSessao();
        if (!this.tempoRealDisponivel(sessao)) {
          break;
        }

        await this.conectarTempoReal(sessao);
        this.estado.set(estavaPausada ? 'pausada' : 'ouvindo');
        return;
      } catch {
        this.fecharConexao();
      }
    }

    if (!this.destino) {
      return;
    }

    // Sem tempo real, a consulta segue por trechos com o mesmo microfone.
    this.destino.mudouDeModo('Trechos', 'A conexão em tempo real caiu; a transcrição seguiu por trechos.');
    this.estado.set(estavaPausada ? 'pausada' : 'ouvindo');
    this.iniciarTrechos(!estavaPausada);
  }

  private atualizarFalaEmAndamento(): void {
    this.falaEmAndamento.set([...this.parciais.values()].join(' ').trim());
  }

  private itensPendentes(): number {
    return this.itens.size;
  }

  private fecharConexao(): void {
    if (this.quedaPendente !== null) {
      clearTimeout(this.quedaPendente);
      this.quedaPendente = null;
    }

    this.canal?.close();
    this.conexao?.close();
    this.canal = null;
    this.conexao = null;
    this.itens.clear();
    this.parciais.clear();
    this.falaEmAndamento.set('');
  }

  // ---------- Por trechos (alternativa) ----------

  private iniciarTrechos(abrir = true): void {
    this.modo.set('Trechos');
    this.formato =
      FORMATOS.find((f) => typeof MediaRecorder !== 'undefined' && MediaRecorder.isTypeSupported(f.mimeType)) ??
      FORMATOS[0]!;

    if (abrir) {
      this.abrirTrecho();
    }

    if (this.relogioTrechos === null) {
      this.relogioTrechos = setInterval(() => this.avaliarCorte(), 200);
    }
  }

  private abrirTrecho(): void {
    if (!this.stream) {
      return;
    }

    const gravador = new MediaRecorder(this.stream, {
      mimeType: this.formato.mimeType,
      audioBitsPerSecond: 32_000,
    });
    const trecho: TrechoEmCaptura = { gravador, partes: [], inicio: Date.now(), pico: 0 };

    gravador.ondataavailable = (evento) => {
      if (evento.data.size > 0) {
        trecho.partes.push(evento.data);
      }
    };

    gravador.start();
    this.trecho = trecho;
    this.silencioDesde = null;
  }

  /**
   * Fecha o trecho atual e o entrega. O áudio só existe até aqui: depois de
   * entregue, a referência é solta e nada fica guardado no navegador.
   */
  private fecharTrecho(): Promise<void> {
    const trecho = this.trecho;
    this.trecho = null;

    if (!trecho || trecho.gravador.state === 'inactive') {
      return Promise.resolve();
    }

    return new Promise((resolver) => {
      trecho.gravador.onstop = () => {
        const comFala = trecho.pico >= LIMIAR_FALA && Date.now() - trecho.inicio >= 1_000;

        if (comFala && this.destino) {
          const tipo = trecho.gravador.mimeType || this.formato.mimeType;
          this.destino.audio(this.proximaSequencia++, new Blob(trecho.partes, { type: tipo }), this.formato.extensao);
        }

        trecho.partes.length = 0;
        resolver();
      };

      trecho.gravador.stop();
    });
  }

  /** Corta o trecho numa pausa natural da fala, para não partir palavras ao meio. */
  private avaliarCorte(): void {
    const trecho = this.trecho;
    if (!trecho || this.estado() !== 'ouvindo') {
      return;
    }

    const agora = Date.now();
    const nivel = this.nivel();
    trecho.pico = Math.max(trecho.pico, nivel);

    if (nivel < LIMIAR_SILENCIO) {
      this.silencioDesde ??= agora;
    } else {
      this.silencioDesde = null;
    }

    const duracao = agora - trecho.inicio;
    const emPausa = this.silencioDesde !== null && agora - this.silencioDesde >= PAUSA_DE_FALA_MS;

    if (duracao >= TRECHO_MAXIMO_MS || (duracao >= TRECHO_MINIMO_MS && emPausa)) {
      // O próximo trecho começa antes de o atual fechar: nenhum som cai no vão.
      void this.fecharTrecho();
      this.abrirTrecho();
    }
  }

  // ---------- Comum ----------

  private medirNivel(stream: MediaStream): void {
    try {
      this.contextoAudio = new AudioContext();
      const analisador = this.contextoAudio.createAnalyser();
      analisador.fftSize = 512;
      this.contextoAudio.createMediaStreamSource(stream).connect(analisador);

      const amostras = new Uint8Array(analisador.fftSize);
      const atualizar = () => {
        analisador.getByteTimeDomainData(amostras);
        let soma = 0;
        for (const amostra of amostras) {
          const valor = (amostra - 128) / 128;
          soma += valor * valor;
        }
        // RMS de fala costuma ficar entre 0,02 e 0,2; o fator leva isso à faixa visível.
        this.nivel.set(Math.min(1, Math.sqrt(soma / amostras.length) * 5));
        this.quadroNivel = requestAnimationFrame(atualizar);
      };
      atualizar();
    } catch {
      // Sem Web Audio o medidor fica parado; a transcrição em si não depende dele.
      this.nivel.set(0);
    }
  }

  private encerrarTudo(): void {
    this.fecharConexao();

    if (this.relogioTrechos !== null) {
      clearInterval(this.relogioTrechos);
      this.relogioTrechos = null;
    }

    if (this.trecho) {
      this.trecho.gravador.ondataavailable = null;
      this.trecho.gravador.onstop = null;
      if (this.trecho.gravador.state !== 'inactive') {
        this.trecho.gravador.stop();
      }
      this.trecho.partes.length = 0;
      this.trecho = null;
    }

    this.stream?.getTracks().forEach((faixa) => faixa.stop());
    this.stream = null;

    if (this.quadroNivel !== null) {
      cancelAnimationFrame(this.quadroNivel);
      this.quadroNivel = null;
    }
    void this.contextoAudio?.close();
    this.contextoAudio = null;

    this.nivel.set(0);
    this.modo.set(null);
    this.estado.set('parada');
  }

  private async aguardar(condicao: () => boolean, limiteMs: number): Promise<boolean> {
    const inicio = Date.now();
    while (!condicao()) {
      if (Date.now() - inicio >= limiteMs) {
        return false;
      }
      await new Promise((resolver) => setTimeout(resolver, 100));
    }
    return true;
  }
}
