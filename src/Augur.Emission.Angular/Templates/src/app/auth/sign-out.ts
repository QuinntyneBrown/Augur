import { Component, inject } from '@angular/core';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-sign-out',
  template: `
    <h1 tabindex="-1">Sign out</h1>
    @if (auth.signedIn()) {
      <p>You are signed in.</p>
      <p><button type="button" class="button" (click)="signOut()">Sign out</button></p>
    } @else {
      <p>You are signed out.</p>
    }
  `,
})
export class SignOut {
  protected readonly auth = inject(AuthService);

  protected signOut(): void {
    this.auth.signOut();
  }
}
