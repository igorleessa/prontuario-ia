import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';
import { AuthService } from './core/services/auth.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('cria o shell da aplicacao', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('esconde o cabecalho enquanto ninguem esta autenticado', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelector('.cabecalho')).toBeNull();
  });

  it('mostra o cabecalho com as iniciais do medico apos o login', () => {
    autenticar('Medico');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelector('.cabecalho')).not.toBeNull();
    expect(elemento.querySelector('.avatar')?.textContent?.trim()).toBe('AS');
  });

  // Quem atende nao configura a clinica nem le a trilha de auditoria. O backend
  // recusa de qualquer forma; o cabecalho nao deve oferecer o caminho.
  it('nao oferece configuracoes nem auditoria ao medico', () => {
    autenticar('Medico');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(rotulosDoCabecalho(fixture.nativeElement)).toEqual(['Meu perfil', 'Sair']);
  });

  it('oferece configuracoes e auditoria ao administrador', () => {
    autenticar('Administrador');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const rotulos = rotulosDoCabecalho(fixture.nativeElement);
    expect(rotulos).toContain('Configurações');
    expect(rotulos).toContain('Auditoria');
  });
});

function autenticar(papel: string): void {
  const auth = TestBed.inject(AuthService);
  auth.autenticado.set(true);
  auth.usuario.set({
    nome: 'Ana Beatriz Souza',
    email: 'ana@clinica.com.br',
    papel,
    clinicaNome: 'Clinica Teste',
    modoOperacao: 'Integrado',
  });
}

function rotulosDoCabecalho(elemento: HTMLElement): string[] {
  return Array.from(elemento.querySelectorAll('.cabecalho__usuario a, .cabecalho__usuario button')).map(
    (item) => item.textContent?.trim() ?? '',
  );
}
