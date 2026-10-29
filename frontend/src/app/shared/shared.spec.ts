import { TestBed } from '@angular/core/testing';
import { cpfMascarado, idade, iniciais } from './pessoa';
import { TextoProtegidoComponent } from './texto-protegido.component';

describe('pessoa', () => {
  it('monta as iniciais ignorando titulo e conectivos', () => {
    expect(iniciais('Dra. Ana Beatriz Souza')).toBe('AS');
    expect(iniciais('Maria das Dores')).toBe('MD');
    expect(iniciais('Helena')).toBe('H');
    expect(iniciais('')).toBe('–');
  });

  it('oculta o miolo do CPF exibido durante a consulta', () => {
    expect(cpfMascarado('529.982.247-25')).toBe('529.***.***-25');
    expect(cpfMascarado(null)).toBeNull();
  });

  it('calcula a idade em anos completos', () => {
    const hoje = new Date();
    const nascimento = `${hoje.getFullYear() - 40}-01-01`;
    expect(idade(nascimento)).toBe(40);
    expect(idade(null)).toBeNull();
  });
});

describe('TextoProtegidoComponent', () => {
  it('troca os marcadores da sanitizacao por etiquetas legiveis', () => {
    const fixture = TestBed.createComponent(TextoProtegidoComponent);
    fixture.componentRef.setInput('texto', 'Ligue para a filha [PESSOA] no [TELEFONE].');
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    const marcadores = Array.from(elemento.querySelectorAll('.marcador-privado')).map((m) => m.textContent);

    expect(marcadores).toEqual(['nome protegido', 'telefone protegido']);
    expect(elemento.textContent).toBe('Ligue para a filha nome protegido no telefone protegido.');
  });

  it('mantem colchetes que nao sao marcadores', () => {
    const fixture = TestBed.createComponent(TextoProtegidoComponent);
    fixture.componentRef.setInput('texto', 'Dose [ver bula]');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.marcador-privado')).toBeNull();
  });
});
