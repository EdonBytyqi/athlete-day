import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { PersonaService } from './persona.service';

/** Sends the demo identity with every API call. */
export const personaInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api')) return next(req);

  const persona = inject(PersonaService).current();
  return next(
    req.clone({
      setHeaders: { 'X-Demo-User': persona.user, 'X-Demo-Role': persona.role },
    }),
  );
};
