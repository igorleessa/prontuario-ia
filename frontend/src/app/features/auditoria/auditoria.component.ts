import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LogAuditoria, ROTULO_ACAO } from '../../core/models/auditoria.model';
import { AuditoriaService } from '../../core/services/auditoria.service';

/**
 * Trilha de auditoria da clinica (RF11). Somente leitura por definicao: nem
 * esta tela nem nenhuma outra apaga uma entrada, e o conteudo clinico em si
 * nunca aparece aqui - so quem acessou o que, e quando.
 */
@Component({
  selector: 'app-auditoria',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './auditoria.component.html',
  styleUrl: './auditoria.component.scss',
})
export class AuditoriaComponent {
  private readonly auditoria = inject(AuditoriaService);

  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly registros = signal<LogAuditoria[]>([]);
  readonly filtro = signal<string | null>(null);

  readonly rotuloAcao = ROTULO_ACAO;

  /** Ações oferecidas no filtro, na ordem em que aparecem no fluxo de um atendimento. */
  readonly acoes = [
    'atendimento.aberto',
    'atendimento.lido',
    'consentimento.registrado',
    'audio.enviado',
    'registro.confirmado',
    'nota.exportada',
    'nota.pdf',
    'configuracao.alterada',
  ];

  constructor() {
    this.carregar();
  }

  filtrar(acao: string | null): void {
    this.filtro.set(acao);
    this.carregar();
  }

  descrever(acao: string): string {
    return this.rotuloAcao[acao] ?? acao;
  }

  private carregar(): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.auditoria.listar(this.filtro()).subscribe({
      next: (registros) => {
        this.registros.set(registros);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar a trilha de auditoria.');
        this.carregando.set(false);
      },
    });
  }
}
