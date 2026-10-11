import { Component, computed, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { PERSONAS, PersonaService } from './api/persona.service';
import { RealtimeService } from './api/realtime.service';
import { TranslationKey } from './i18n/catalogue';
import { I18n } from './i18n/i18n.service';
import { LanguageSwitch } from './shared/language-switch';
import { staffBaseUrl } from './shared/staff-url';
import { ToastOutlet } from './shared/toast-outlet';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastOutlet, LanguageSwitch],
  templateUrl: './app.html',
})
export class App {
  protected readonly persona = inject(PersonaService);
  protected readonly realtime = inject(RealtimeService);
  private readonly i18n = inject(I18n);
  private readonly router = inject(Router);

  protected readonly t = this.i18n.t;
  protected readonly personas = PERSONAS;
  protected readonly staffUrl = computed(() => staffBaseUrl(this.i18n.lang()));
  protected readonly liveLabel = computed(() => this.t(`app.live.${this.realtime.state()}` as TranslationKey));
  /** On phones, navigation and the persona switch fold away behind the Menu button (NFR-06). */
  protected readonly menuOpen = signal(false);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: null },
  );

  constructor() {
    this.realtime.start();

    // After a role switch, leave screens the new role may not open (e.g. Coach → Kitchen).
    effect(() => {
      const screens = this.persona.screens();
      const url = this.url();
      if (url === null) return; // wait for the first navigation to finish
      const first = url.split('/')[1]?.split('?')[0] ?? '';
      const screen = first === 'athletes' ? 'dashboard' : first;
      if (screens.length && !screens.includes(screen)) {
        this.router.navigate(['/', screens[0]]);
      }
    });

    // Close the phone menu after navigating.
    effect(() => {
      this.url();
      this.menuOpen.set(false);
    });
  }

  protected screenLabel(screen: string): TranslationKey {
    return `app.screens.${screen}` as TranslationKey;
  }

  protected personaLabel(user: string): TranslationKey {
    return `personas.${user}` as TranslationKey;
  }
}
