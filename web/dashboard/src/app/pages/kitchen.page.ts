import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { KitchenRow } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { I18n } from '../i18n/i18n.service';

@Component({
  selector: 'app-kitchen-page',
  template: `
    <header class="page-head">
      <div>
        <h1>{{ t('kitchen.title') }}</h1>
        <p class="muted">{{ t('kitchen.lead') }}</p>
      </div>
      @if (rows(); as list) {
        <div class="stats">
          <div class="stat"><strong>{{ i18n.number(totalCarbs()) }} g</strong><span>{{ t('kitchen.totalCarbs') }}</span></div>
        </div>
      }
    </header>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else if (rows(); as list) {
      <div class="kitchen-grid">
        @for (r of list; track r.athleteCode) {
          <article class="plate">
            <div class="portion" [attr.aria-label]="t('kitchen.portion', { portion: r.portion })">{{ r.portion }}</div>
            <div>
              <h2>{{ r.displayName }}</h2>
              <div class="muted">{{ r.athleteCode }}</div>
              <div class="plate-numbers">{{ t('kitchen.plate', { carbs: i18n.number(r.carbsG), kcal: i18n.number(r.energyKcal) }) }}</div>
              @if (r.kitchenNote) {
                <span class="tag">{{ r.kitchenNote }}</span>
              }
            </div>
          </article>
        } @empty {
          <p class="muted">{{ t('kitchen.empty') }}</p>
        }
      </div>
    } @else {
      <p class="muted">{{ t('common.loading') }}</p>
    }
  `,
})
export class KitchenPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

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
            this.error.set(describeError(e, role, this.t));
          },
        }),
      );
    });
  }
}
