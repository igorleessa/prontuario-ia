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
    const auth = TestBed.inject(AuthService);
    auth.autenticado.set(true);
    auth.usuario.set({
      nome: 'Ana Beatriz Souza',
      email: 'ana@clinica.com.br',
      papel: 'Medico',
      clinicaNome: 'Clinica Teste',
      modoOperacao: 'Integrado',
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelector('.cabecalho')).not.toBeNull();
    expect(elemento.querySelector('.avatar')?.textContent?.trim()).toBe('AS');
  });
});
