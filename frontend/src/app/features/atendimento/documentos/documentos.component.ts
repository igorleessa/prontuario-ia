import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentoClinico, ROTULO_DOCUMENTO } from '../../../core/models/template.model';
import { DocumentoService } from '../../../core/services/documento.service';

type Tipo = DocumentoClinico['tipo'];

/**
 * Receita, pedido de exame, atestado e encaminhamento redigidos a partir da
 * mesma consulta. Valem as regras da nota clinica: a IA escreve o rascunho, o
 * medico revisa e edita, e nada e emitido sem essa revisao.
 */
@Component({
  selector: 'app-documentos',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './documentos.component.html',
  styleUrl: './documentos.component.scss',
})
export class DocumentosComponent {
  private readonly documentos = inject(DocumentoService);

  readonly atendimentoId = input.required<string>();

  /** Somente leitura depois que o atendimento e encerrado. */
  readonly bloqueado = input(false);

  readonly lista = signal<DocumentoClinico[]>([]);
  readonly gerando = signal<Tipo | null>(null);
  readonly salvando = signal<string | null>(null);
  readonly erro = signal<string | null>(null);
  readonly aberto = signal<string | null>(null);
  readonly rascunhos = signal<Record<string, string>>({});

  readonly tipos: Tipo[] = ['Receita', 'PedidoExame', 'Atestado', 'Encaminhamento'];
  readonly rotulo = ROTULO_DOCUMENTO;

  readonly vazio = computed(() => this.lista().length === 0);

  constructor() {
    // O input so existe apos o binding; carregar no primeiro clique seria pior
    // para o medico que volta a um atendimento com documentos ja gerados.
    queueMicrotask(() => this.carregar());
  }

  gerar(tipo: Tipo): void {
    if (this.gerando()) {
      return;
    }

    this.gerando.set(tipo);
    this.erro.set(null);

    this.documentos.gerar(this.atendimentoId(), tipo).subscribe({
      next: (documento) => {
        this.gerando.set(null);
        this.substituir(documento);
        this.aberto.set(documento.id);
      },
      error: (erro: { error?: { erro?: string } }) => {
        this.gerando.set(null);
        this.erro.set(erro.error?.erro ?? 'Não foi possível gerar o documento.');
      },
    });
  }

  alternar(documentoId: string): void {
    this.aberto.update((atual) => (atual === documentoId ? null : documentoId));
  }

  editar(documentoId: string, conteudo: string): void {
    this.rascunhos.update((atual) => ({ ...atual, [documentoId]: conteudo }));
  }

  conteudo(documento: DocumentoClinico): string {
    return this.rascunhos()[documento.id] ?? documento.conteudo;
  }

  alterado(documento: DocumentoClinico): boolean {
    const rascunho = this.rascunhos()[documento.id];
    return rascunho !== undefined && rascunho !== documento.conteudo;
  }

  salvar(documento: DocumentoClinico): void {
    if (this.salvando()) {
      return;
    }

    this.salvando.set(documento.id);
    this.erro.set(null);

    this.documentos.salvar(this.atendimentoId(), documento.id, this.conteudo(documento)).subscribe({
      next: (atualizado) => {
        this.salvando.set(null);
        this.substituir(atualizado);
        this.rascunhos.update(({ [documento.id]: _, ...resto }) => resto);
      },
      error: () => {
        this.salvando.set(null);
        this.erro.set('Não foi possível salvar o documento.');
      },
    });
  }

  baixarPdf(documento: DocumentoClinico): void {
    this.documentos.baixarPdf(this.atendimentoId(), documento.id).subscribe({
      next: (arquivo) => {
        const url = URL.createObjectURL(arquivo);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${documento.tipo.toLowerCase()}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.erro.set('Não foi possível gerar o PDF do documento.'),
    });
  }

  private carregar(): void {
    this.documentos.listar(this.atendimentoId()).subscribe({
      next: (lista) => this.lista.set(lista),
      error: () => this.erro.set('Não foi possível carregar os documentos.'),
    });
  }

  private substituir(documento: DocumentoClinico): void {
    this.lista.update((atual) => {
      const outros = atual.filter((d) => d.tipo !== documento.tipo);
      return [...outros, documento].sort((a, b) => a.tipo.localeCompare(b.tipo));
    });
  }
}
