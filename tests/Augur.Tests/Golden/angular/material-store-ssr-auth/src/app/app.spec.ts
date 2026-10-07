import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideOAuthClient } from 'angular-oauth2-oidc';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideOAuthClient(),
      ],
    }).compileComponents();
  });

  it('starts with a skip link and has one banner, navigation, and main region', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('a, button')?.textContent?.trim()).toBe('Skip to main content');
    expect(element.querySelectorAll('header')).toHaveLength(1);
    expect(element.querySelectorAll('nav, [role="navigation"]')).toHaveLength(1);
    expect(element.querySelectorAll('main')).toHaveLength(1);
  });

  it('opens and closes the navigation from the menu button', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.menu-button')!;

    button.click();
    await fixture.whenStable();
    expect(button.getAttribute('aria-expanded')).toBe('true');

    button.click();
    await fixture.whenStable();
    expect(button.getAttribute('aria-expanded')).toBe('false');
  });

  it('shows the home page with its heading', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('main h1')?.textContent).toContain('Contoso Orders');
  });

  it('shows the not-found page for an unknown address', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/does-not-exist');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('main h1')?.textContent).toContain('Page not found');
  });
});
