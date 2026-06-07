import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../api/api-base-url.token';

export const apiBaseUrlInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api')) {
    return next(request);
  }

  const baseUrl = inject(API_BASE_URL).replace(/\/$/, '');
  return next(request.clone({ url: `${baseUrl}${request.url}` }));
};
