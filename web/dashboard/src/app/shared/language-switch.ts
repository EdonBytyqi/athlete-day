import { Component, inject } from '@angular/core';
import { I18n, Language } from '../i18n/i18n.service';

/** English / German. Switching is immediate: every text is a signal read (AC-53). */
@Component({
  selector: 'app-language-switch',
  template: `
    <div class="lang-switch" role="group" [attr.aria-label]="i18n.t('app.language')">
      @for (option of options; track option.lang) {
        <button
          type="button"
          [attr.lang]="option.lang"
          [attr.aria-label]="option.name"
          [attr.aria-pressed]="i18n.lang() === option.lang"
          (click)="i18n.setLanguage(option.lang)"
        >
          {{ option.short }}
        </button>
      }
    </div>
  `,
})
export class LanguageSwitch {
  protected readonly i18n = inject(I18n);

  // Each language names itself, so people find theirs whatever is selected.
  protected readonly options: { lang: Language; short: string; name: string }[] = [
    { lang: 'en', short: 'EN', name: 'English' },
    { lang: 'de', short: 'DE', name: 'Deutsch' },
  ];
}
