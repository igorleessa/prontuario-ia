import { Injectable, signal } from '@angular/core';

/** Áudio capturado, já com a extensão que corresponde ao formato que o navegador gravou. */
export interface AudioGravado {
  blob: Blob;
  extensao: string;
}

/**
 * Formatos em ordem de preferência. Opus mono a 32 kbps tem qualidade de sobra
 * para fala e cabe no limite de 25 MB da transcrição até cerca de 1h40 de
 * consulta. Safari (Mac e iPad) não grava WebM e cai no MP4.
 */
const FORMATOS: { mimeType: string; extensao: string }[] = [
  { mimeType: 'audio/webm;codecs=opus', extensao: 'webm' },
  { mimeType: 'audio/ogg;codecs=opus', extensao: 'ogg' },
  { mimeType: 'audio/mp4', extensao: 'mp4' },
  { mimeType: 'audio/webm', extensao: 'webm' },
];

const BITS_POR_SEGUNDO = 32_000;

/**
 * Captura o audio da consulta no navegador (RF06). Só deve ser iniciada
 * depois do consentimento registrado (RF05).
 */
@Injectable({ providedIn: 'root' })
export class GravacaoAudioService {
  readonly gravando = signal(false);

  /** Volume captado agora, de 0 a 1: mostra ao médico que o microfone está ouvindo. */
  readonly nivel = signal(0);

  private mediaRecorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private extensao = 'webm';
  private contextoAudio: AudioContext | null = null;
  private quadroNivel: number | null = null;

  async iniciar(): Promise<void> {
    // Cancelamento de eco, supressão de ruído e ganho automático limpam o
    // ruído de consultório (ar-condicionado, teclado) e equilibram a voz de
    // quem está mais longe do microfone - o que mais pesa na transcrição.
    const stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        channelCount: 1,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });

    const formato = FORMATOS.find((f) => MediaRecorder.isTypeSupported(f.mimeType));
    this.extensao = formato?.extensao ?? 'webm';

    this.chunks = [];
    this.mediaRecorder = new MediaRecorder(stream, {
      ...(formato ? { mimeType: formato.mimeType } : {}),
      audioBitsPerSecond: BITS_POR_SEGUNDO,
    });
    this.mediaRecorder.ondataavailable = (evento) => {
      if (evento.data.size > 0) {
        this.chunks.push(evento.data);
      }
    };

    // Fatias de 1 s: o áudio vai sendo guardado durante a consulta, e não só
    // no fim, o que reduz o risco de perder a gravação inteira num travamento.
    this.mediaRecorder.start(1000);
    this.gravando.set(true);
    this.medirNivel(stream);
  }

  parar(): Promise<AudioGravado> {
    return new Promise((resolve, reject) => {
      const recorder = this.mediaRecorder;
      if (!recorder) {
        reject(new Error('Nenhuma gravacao em andamento.'));
        return;
      }

      recorder.onstop = () => {
        this.liberar(recorder);
        const tipo = recorder.mimeType || 'audio/webm';
        resolve({ blob: new Blob(this.chunks, { type: tipo }), extensao: this.extensao });
      };

      recorder.stop();
    });
  }

  /** Solta o microfone sem entregar o áudio - usado ao sair da tela no meio da gravação. */
  descartar(): void {
    const recorder = this.mediaRecorder;
    if (!recorder) {
      return;
    }

    recorder.onstop = null;
    if (recorder.state !== 'inactive') {
      recorder.stop();
    }
    this.liberar(recorder);
    this.chunks = [];
  }

  private liberar(recorder: MediaRecorder): void {
    recorder.stream.getTracks().forEach((track) => track.stop());
    this.mediaRecorder = null;
    this.gravando.set(false);

    if (this.quadroNivel !== null) {
      cancelAnimationFrame(this.quadroNivel);
      this.quadroNivel = null;
    }
    void this.contextoAudio?.close();
    this.contextoAudio = null;
    this.nivel.set(0);
  }

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
      // Sem Web Audio o medidor fica parado; a gravação em si não depende dele.
      this.nivel.set(0);
    }
  }
}
