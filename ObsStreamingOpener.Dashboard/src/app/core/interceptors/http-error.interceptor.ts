import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const httpErrorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      const payload = error.error as { error?: string } | string | null;
      const message =
        typeof payload === 'string'
          ? payload
          : payload?.error ?? `Request failed with status ${error.status}`;
      return throwError(() => new Error(message));
    })
  );
