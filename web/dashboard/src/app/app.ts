import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { PERSONAS, PersonaService } from './api/persona.service';
import { RealtimeService } from './api/realtime.service';

const SCREEN_LABELS: Record<string, string> = {
  dashboard: 'Squad',
  kitchen: 'Kitchen',
  imports: 'Device import',
  audit: 'Audit log',
};

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  protected readonly persona = inject(PersonaService);
  protected readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);

  protected readonly personas = PERSONAS;
  protected readonly labels = SCREEN_LABELS;
  protected readonly staffUrl =
    location.port === '4200' ? 'http://localhost:5090' : `${location.protocol}//${location.hostname}:8081`;

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
  }
}
