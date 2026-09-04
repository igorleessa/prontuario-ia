import { Injectable, signal } from '@angular/core';

/**
 * Captura o audio da consulta no navegador (RF06). Só deve ser iniciada
 * depois do consentimento registrado (RF05).
 */
@Injectable({ providedIn: 'root' })
export class GravacaoAudioService {
  readonly gravando = signal(false);

  private mediaRecorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];

  async iniciar(): Promise<void> {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

    this.chunks = [];
    this.mediaRecorder = new MediaRecorder(stream);
    this.mediaRecorder.ondataavailable = (evento) => this.chunks.push(evento.data);
    this.mediaRecorder.start();
    this.gravando.set(true);
  }

  parar(): Promise<Blob> {
    return new Promise((resolve, reject) => {
      const recorder = this.mediaRecorder;
      if (!recorder) {
        reject(new Error('Nenhuma gravacao em andamento.'));
        return;
      }

      recorder.onstop = () => {
        recorder.stream.getTracks().forEach((track) => track.stop());
        this.mediaRecorder = null;
        this.gravando.set(false);
        resolve(new Blob(this.chunks, { type: 'audio/webm' }));
      };

      recorder.stop();
    });
  }
}
