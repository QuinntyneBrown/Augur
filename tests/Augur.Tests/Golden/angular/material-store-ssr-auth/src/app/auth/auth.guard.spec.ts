import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  function guardWith(signedIn: boolean) {
    const auth = { isSignedIn: () => signedIn, signIn: vi.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }] });
    const allowed = TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
    return { allowed, auth };
  }

  it('sends a signed-out user to sign-in and blocks the page', () => {
    const { allowed, auth } = guardWith(false);

    expect(allowed).toBe(false);
    expect(auth.signIn).toHaveBeenCalledOnce();
  });

  it('lets a signed-in user through', () => {
    const { allowed, auth } = guardWith(true);

    expect(allowed).toBe(true);
    expect(auth.signIn).not.toHaveBeenCalled();
  });
});
