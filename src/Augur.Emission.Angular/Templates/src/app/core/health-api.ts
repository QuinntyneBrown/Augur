import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, timeout } from 'rxjs';
import { environment } from '../../environments/environment';

export type HealthStatus = 'Healthy' | 'Unavailable';

/** Asks the API whether it is up. Never throws: any failure, including a 5-second timeout, is "Unavailable". */
@Injectable({ providedIn: 'root' })
export class HealthApi {
  private readonly http = inject(HttpClient);

  check(): Observable<HealthStatus> {
    return this.http.get<{ status?: string }>(`${environment.apiBaseUrl}/health`).pipe(
      timeout(5000),
      map((body): HealthStatus => (body.status === 'Healthy' ? 'Healthy' : 'Unavailable')),
      catchError(() => of<HealthStatus>('Unavailable')),
    );
  }
}
