import { Component, inject, input } from '@angular/core';
import { Insight } from '../api/models';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';

/** Units of the device tests (spec section 4), for the sentences. */
export const TEST_UNITS: Record<string, string> = { CMJ: 'cm', SJ: 'cm', SPRINT_30M: 's', GPS_DISTANCE: 'm', GPS_HSR: 'm' };

/**
 * Insights as plain sentences with their figures (AC-99, ADR-16). The API sends the kind and the numbers; the wording
 * lives here, in both languages.
 */
@Component({
  selector: 'app-insight-list',
  template: `
    <ul class="insight-list">
      @for (insight of insights(); track $index) {
        <li class="insight" [attr.data-tone]="insight.tone">
          <span class="insight-tone">{{ t(toneLabel(insight)) }}</span>
          <span class="insight-text">{{ sentence(insight) }}</span>
        </li>
      } @empty {
        <li class="muted insights-empty">{{ t('insights.empty') }}</li>
      }
    </ul>
  `,
})
export class InsightList {
  private readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly insights = input.required<Insight[]>();

  protected toneLabel(insight: Insight): TranslationKey {
    return `insights.tone.${insight.tone}`;
  }

  protected sentence(insight: Insight): string {
    const f = insight.figures;
    const n = (value: number | undefined, max = 0, min = max) => (value === undefined ? '–' : this.i18n.number(value, max, min));
    const test = insight.testType ? this.t(`performance.tests.${insight.testType}` as TranslationKey) : '';
    const unit = insight.testType ? ` ${TEST_UNITS[insight.testType] ?? ''}` : '';
    switch (insight.kind) {
      case 'LoadSpike':
      case 'LoadDrop':
        return this.t(`insights.kinds.${insight.kind}`, { ratio: n(f['ratio'], 2), acute: n(f['acute']), chronic: n(f['chronic']) });
      case 'SleepFalling':
      case 'SorenessRising':
        return this.t(`insights.kinds.${insight.kind}`, { recent: n(f['recent'], 1, 1), before: n(f['before'], 1, 1) });
      case 'MissingCheckIns':
        return this.t('insights.kinds.MissingCheckIns', { checkIns: n(f['checkIns']) });
      case 'PersonalBest':
        return this.t('insights.kinds.PersonalBest', { test, value: n(f['value'], 2, 0) + unit, previous: n(f['previous'], 2, 0) + unit });
      case 'GoalAtRisk':
        return f['latest'] === undefined
          ? this.t('insights.kinds.GoalAtRiskNoResult', { test, target: n(f['target'], 2, 0) + unit, days: n(f['daysLeft']) })
          : this.t('insights.kinds.GoalAtRisk', {
              test,
              target: n(f['target'], 2, 0) + unit,
              days: n(f['daysLeft']),
              latest: n(f['latest'], 2, 0) + unit,
            });
      case 'PlanOffTrack': {
        const difference = f['difference'] ?? 0;
        return this.t('insights.kinds.PlanOffTrack', {
          planned: n(f['planned']),
          logged: n(f['logged']),
          difference: `${difference > 0 ? '+' : ''}${n(difference)} %`,
        });
      }
    }
  }
}
