import { Component, DOCUMENT, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { RouteFocus } from './core/route-focus';
import { AppState } from './state/app-state';

/** The app shell: skip link, header, navigation, main content, and footer. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  private readonly state = inject(AppState);
  private readonly document = inject(DOCUMENT);
  protected readonly menuOpen = this.state.menuOpen;

  constructor() {
    inject(RouteFocus).start();
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.state.closeMenu());
  }

  protected toggleMenu(): void {
    this.state.toggleMenu();
  }

  protected closeMenu(): void {
    this.state.closeMenu();
  }

  protected skipToMain(event: Event): void {
    event.preventDefault();
    this.document.getElementById('main')?.focus();
  }
}
