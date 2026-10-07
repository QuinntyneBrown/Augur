import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HealthApi, HealthStatus } from './health-api';

describe('HealthApi', () => {
  let api: HealthApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(HealthApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reports Healthy when the API says it is healthy', () => {
    let status: HealthStatus | undefined;
    api.check().subscribe((value) => (status = value));

    http.expectOne('/api/health').flush({ status: 'Healthy' });

    expect(status).toBe('Healthy');
  });

  it('reports Unavailable when the request fails', () => {
    let status: HealthStatus | undefined;
    api.check().subscribe((value) => (status = value));

    http.expectOne('/api/health').flush('', { status: 504, statusText: 'Gateway Timeout' });

    expect(status).toBe('Unavailable');
  });
});
