import { Component, DOCUMENT, inject } from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from './auth/auth.service';
import { RouteFocus } from './core/route-focus';
import { AppStore } from './state/app-store';

/** The app shell: skip link, header, navigation, main content, and footer. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatSidenavModule, MatListModule, MatButtonModule],
  templateUrl: './app.html',
})
export class App {
  private readonly state = inject(AppStore);
  private readonly document = inject(DOCUMENT);
  protected readonly menuOpen = this.state.menuOpen;
  protected readonly auth = inject(AuthService);

  /** True at 768px and wider, where the navigation stays open beside the content. */
  protected readonly wide = toSignal(
    inject(BreakpointObserver).observe('(min-width: 768px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

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
