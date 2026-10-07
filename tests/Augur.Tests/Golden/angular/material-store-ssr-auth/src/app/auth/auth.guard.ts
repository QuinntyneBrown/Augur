import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { AuthService } from './auth.service';

/** Lets signed-in users through and sends everyone else to sign-in before the protected page renders. */
export const authGuard: CanActivateFn = () => {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return false;
  }

  const auth = inject(AuthService);
  if (auth.isSignedIn()) {
    return true;
  }

  auth.signIn();
  return false;
};
