import { Injectable, signal } from '@angular/core';

/** Application state shared across the app, held in signals. */
@Injectable({ providedIn: 'root' })
export class AppState {
  private readonly menuOpenState = signal(false);

  /** Whether the navigation is open on a narrow screen. */
  readonly menuOpen = this.menuOpenState.asReadonly();

  toggleMenu(): void {
    this.menuOpenState.update((open) => !open);
  }

  closeMenu(): void {
    this.menuOpenState.set(false);
  }
}
