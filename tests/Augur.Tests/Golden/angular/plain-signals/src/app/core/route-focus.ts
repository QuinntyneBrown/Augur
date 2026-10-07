import { DOCUMENT, Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/**
 * Moves keyboard and screen-reader focus to the new page's heading after each navigation, so users know the page
 * changed. The first page load keeps the browser's default focus so the skip link stays first.
 */
@Injectable({ providedIn: 'root' })
export class RouteFocus {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private started = false;

  start(): void {
    if (this.started || !this.browser) {
      return;
    }

    this.started = true;
    let firstNavigation = true;
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      if (firstNavigation) {
        firstNavigation = false;
        return;
      }

      setTimeout(() => this.document.querySelector<HTMLElement>('main h1')?.focus());
    });
  }
}
