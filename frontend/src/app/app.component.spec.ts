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

  it('esconde a navegacao enquanto ninguem esta autenticado', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelector('.lateral')).toBeNull();
  });

  it('mostra a navegacao com as iniciais do medico apos o login', () => {
    autenticar('Medico');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelector('.lateral')).not.toBeNull();
    expect(elemento.querySelector('.usuario .avatar')?.textContent?.trim()).toBe('AS');
  });

  // Quem atende nao configura a clinica nem le a trilha de auditoria. O backend
  // recusa de qualquer forma; o menu nao deve oferecer o caminho.
  it('nao oferece configuracoes nem auditoria ao medico', () => {
    autenticar('Medico');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(rotulosDoMenu(fixture.nativeElement)).toEqual(['Painel', 'Nova consulta', 'Pacientes']);
  });

  it('oferece configuracoes e auditoria ao administrador', () => {
    autenticar('Administrador');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const rotulos = rotulosDoMenu(fixture.nativeElement);
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

function rotulosDoMenu(elemento: HTMLElement): string[] {
  return Array.from(elemento.querySelectorAll('.menu__item')).map((item) => item.textContent?.trim() ?? '');
}
