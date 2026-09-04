import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AtendimentoService } from '../../core/services/atendimento.service';
import { GravacaoAudioService } from '../../core/services/gravacao-audio.service';

type Etapa = 'consentimento' | 'gravacao' | 'revisao';

@Component({
  selector: 'app-atendimento',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './atendimento.component.html',
  styleUrl: './atendimento.component.scss',
})
export class AtendimentoComponent {
  private readonly atendimentos = inject(AtendimentoService);
  private readonly gravacaoAudio = inject(GravacaoAudioService);

  readonly etapa = signal<Etapa>('consentimento');
  readonly atendimentoId = signal<string | null>(null);
  readonly gravando = this.gravacaoAudio.gravando;
  readonly mensagem = signal<string | null>(null);

  /** Campos do formulario de atendimento, pre-preenchidos pela IA e editaveis pelo medico (RF09/RF10). */
  readonly form = inject(FormBuilder).nonNullable.group({
    queixaPrincipal: [''],
    hda: [''],
    antecedentes: [''],
    exameFisico: [''],
    hipoteseDiagnostica: [''],
    cid10Sugerido: [''],
    conduta: [''],
  });

  aceitarConsentimento(): void {
    const id = this.atendimentoId();
    if (!id) {
      this.mensagem.set('Abra um atendimento antes de registrar o consentimento.');
      return;
    }

    this.atendimentos.registrarConsentimento(id).subscribe({
      next: () => this.etapa.set('gravacao'),
      error: () => this.mensagem.set('Nao foi possivel registrar o consentimento.'),
    });
  }

  async iniciarGravacao(): Promise<void> {
    try {
      await this.gravacaoAudio.iniciar();
    } catch {
      this.mensagem.set('Nao foi possivel acessar o microfone.');
    }
  }

  async pararGravacao(): Promise<void> {
    await this.gravacaoAudio.parar();

    // TODO: enviar o audio ao backend e aguardar o rascunho da IA via SignalR (RF07/RF12).
    // Enquanto os provedores de STT/LLM nao sao definidos, o medico preenche manualmente.
    this.etapa.set('revisao');
  }

  confirmar(): void {
    const id = this.atendimentoId();
    if (!id) {
      return;
    }

    const valores = this.form.getRawValue();
    this.atendimentos
      .confirmar(id, {
        queixaPrincipal: valores.queixaPrincipal || null,
        hda: valores.hda || null,
        antecedentes: valores.antecedentes || null,
        exameFisico: valores.exameFisico || null,
        hipoteseDiagnostica: valores.hipoteseDiagnostica || null,
        cid10Sugerido: valores.cid10Sugerido || null,
        conduta: valores.conduta || null,
      })
      .subscribe({
        next: () => this.mensagem.set('Atendimento finalizado.'),
        error: () => this.mensagem.set('Nao foi possivel finalizar o atendimento.'),
      });
  }
}
