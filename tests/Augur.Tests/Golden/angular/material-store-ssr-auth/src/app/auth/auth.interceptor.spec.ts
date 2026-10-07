import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  const auth = { accessToken: () => 'token-123', signIn: vi.fn() };
  let http: HttpClient;
  let testing: HttpTestingController;

  beforeEach(() => {
    auth.signIn.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    testing = TestBed.inject(HttpTestingController);
  });

  afterEach(() => testing.verify());

  it('sends the token to the API and nowhere else', () => {
    http.get('/api/health').subscribe();
    http.get('https://example.org/data').subscribe();

    expect(testing.expectOne('/api/health').request.headers.get('Authorization')).toBe('Bearer token-123');
    expect(testing.expectOne('https://example.org/data').request.headers.has('Authorization')).toBe(false);
  });

  it('starts sign-in again when the API answers 401', () => {
    http.get('/api/notes').subscribe({ error: () => undefined });

    testing.expectOne('/api/notes').flush('', { status: 401, statusText: 'Unauthorized' });

    expect(auth.signIn).toHaveBeenCalledOnce();
  });
});
