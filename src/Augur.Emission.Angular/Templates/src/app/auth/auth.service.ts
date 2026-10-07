import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, Provider, inject, signal } from '@angular/core';
import { AuthConfig, OAuthService, OAuthStorage } from 'angular-oauth2-oidc';
import { environment } from '../../environments/environment';

/** Keeps tokens in sessionStorage in the browser (never localStorage), and in memory during server rendering. */
export function provideAuthStorage(): Provider {
  return {
    provide: OAuthStorage,
    useFactory: () => (isPlatformBrowser(inject(PLATFORM_ID)) ? sessionStorage : new MemoryStorage()),
  };
}

class MemoryStorage implements OAuthStorage {
  private readonly items = new Map<string, string>();

  getItem(key: string): string | null {
    return this.items.get(key) ?? null;
  }

  setItem(key: string, data: string): void {
    this.items.set(key, data);
  }

  removeItem(key: string): void {
    this.items.delete(key);
  }
}

/**
 * Signs users in with the OpenID Connect Authorization Code flow and PKCE (S256) against the authority in
 * environment.ts. The app is a public client: there is no client secret.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly oauth = inject(OAuthService);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly signedInState = signal(false);
  private configured = false;

  /** Whether a user is signed in with a valid access token. */
  readonly signedIn = this.signedInState.asReadonly();

  /** Completes a sign-in that is returning from the authority, if any. Runs once at startup. */
  async initialize(): Promise<void> {
    if (!this.browser || !environment.auth.authority) {
      return;
    }

    this.oauth.configure(this.config());
    this.configured = true;
    try {
      await this.oauth.loadDiscoveryDocumentAndTryLogin();
    } catch (error) {
      console.warn('Sign-in is unavailable: the authority could not be reached.', error);
    }

    this.signedInState.set(this.oauth.hasValidAccessToken());
    this.oauth.events.subscribe(() => this.signedInState.set(this.oauth.hasValidAccessToken()));
  }

  isSignedIn(): boolean {
    return this.browser && this.configured && this.oauth.hasValidAccessToken();
  }

  accessToken(): string | null {
    return this.isSignedIn() ? this.oauth.getAccessToken() : null;
  }

  signIn(): void {
    if (!this.browser) {
      return;
    }

    if (!this.configured) {
      console.warn('Sign-in is not configured: set environment.auth.authority and clientId.');
      return;
    }

    this.oauth.initCodeFlow();
  }

  signOut(): void {
    if (this.browser && this.configured) {
      this.oauth.logOut();
      this.signedInState.set(false);
    }
  }

  private config(): AuthConfig {
    return {
      issuer: environment.auth.authority,
      clientId: environment.auth.clientId,
      redirectUri: `${window.location.origin}/`,
      postLogoutRedirectUri: `${window.location.origin}/sign-out`,
      responseType: 'code',
      scope: 'openid profile',
      requireHttps: 'remoteOnly',
      showDebugInformation: false,
    };
  }
}
