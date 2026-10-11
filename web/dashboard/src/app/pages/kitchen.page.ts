import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { KitchenRow } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';

@Component({
  selector: 'app-kitchen-page',
  template: `
    <header class="page-head">
      <div>
        <h1>Kitchen · today</h1>
        <p class="muted">Portion size and carbohydrates per athlete. No health data reaches this screen.</p>
      </div>
      @if (rows(); as list) {
        <div class="stats">
          <div class="stat"><strong>{{ totalCarbs() }} g</strong><span>carbohydrates in total</span></div>
        </div>
      }
    </header>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else if (rows(); as list) {
      <div class="kitchen-grid">
        @for (r of list; track r.athleteCode) {
          <article class="plate">
            <div class="portion" [attr.aria-label]="'Portion ' + r.portion">{{ r.portion }}</div>
            <div>
              <h2>{{ r.displayName }}</h2>
              <div class="muted">{{ r.athleteCode }}</div>
              <div class="plate-numbers">{{ r.carbsG }} g carbohydrates · {{ r.energyKcal }} kcal</div>
              @if (r.kitchenNote) {
                <span class="tag">{{ r.kitchenNote }}</span>
              }
            </div>
          </article>
        } @empty {
          <p class="muted">No nutrition targets set for today.</p>
        }
      </div>
    } @else {
      <p class="muted">Loading…</p>
    }
  `,
})
export class KitchenPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);

  protected readonly rows = signal<KitchenRow[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly totalCarbs = computed(() => (this.rows() ?? []).reduce((sum, r) => sum + r.carbsG, 0));

  constructor() {
    effect(() => {
      const role = this.persona.current().role;
      this.realtime.version();
      untracked(() =>
        this.api.kitchenToday().subscribe({
          next: (rows) => {
            this.rows.set(rows);
            this.error.set(null);
          },
          error: (e) => {
            this.rows.set(null);
            this.error.set(describeError(e, role));
          },
        }),
      );
    });
  }
}
