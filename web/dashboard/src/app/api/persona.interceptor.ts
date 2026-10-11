import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { I18n } from '../i18n/i18n.service';
import { PersonaService } from './persona.service';

/** Sends the demo identity and the current language with every API call (server texts follow it, ADR-09). */
export const personaInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api')) return next(req);

  const persona = inject(PersonaService).current();
  return next(
    req.clone({
      setHeaders: {
        'X-Demo-User': persona.user,
        'X-Demo-Role': persona.role,
        'Accept-Language': inject(I18n).lang(),
      },
    }),
  );
};
