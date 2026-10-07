import { Component, inject } from '@angular/core';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-sign-in',
  template: `
    <h1 tabindex="-1">Sign in</h1>
    <p>Sign in to see your notes.</p>
    <p><button type="button" class="button" (click)="signIn()">Sign in</button></p>
  `,
})
export class SignIn {
  private readonly auth = inject(AuthService);

  protected signIn(): void {
    this.auth.signIn();
  }
}
