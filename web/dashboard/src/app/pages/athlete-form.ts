import { Component, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormField, form, max, maxLength, min, minLength, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDetails, AthleteRequest, Sex } from '../api/models';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';
import { ModalDialog } from '../shared/modal-dialog';

const THIS_YEAR = new Date().getFullYear();

const EMPTY: AthleteRequest = { displayName: '', sport: '', birthYear: null, sex: 'NotStated', bodyMassKg: 70 };

/** A field's state, as far as showing its error goes. */
interface FieldView {
  touched(): boolean;
  invalid(): boolean;
  errors(): { message?: string }[];
}

/**
 * Add or edit an athlete. The same rules as AthleteRequest in AthleteDay.Contracts; the API checks them again.
 * Validation messages are catalogue keys, translated when shown, so they follow a language switch.
 */
@Component({
  selector: 'app-athlete-form',
  imports: [FormField, ModalDialog],
  template: `
    <app-modal-dialog
      kind="athlete-form"
      [heading]="athlete() ? t('athleteForm.editTitle', { name: athlete()!.displayName }) : t('athleteForm.addTitle')"
      (closed)="cancelled.emit()"
    >
      <form class="form" novalidate (submit)="save($event)">
        @if (athlete(); as a) {
          <p class="muted">{{ t('athleteForm.codeStays', { code: a.code }) }}</p>
        } @else {
          <p class="muted">{{ t('athleteForm.codeAuto') }}</p>
        }

        <label>
          {{ t('athleteForm.name') }}
          <input autocomplete="off" [formField]="form.displayName" [attr.aria-invalid]="showError(form.displayName)" />
          @if (errorText(form.displayName); as message) {
            <span class="field-error">{{ message }}</span>
          }
        </label>

        <label>
          {{ t('athleteForm.sport') }}
          <input list="known-sports" autocomplete="off" [formField]="form.sport" [attr.aria-invalid]="showError(form.sport)" />
          <datalist id="known-sports">
            @for (s of sports(); track s) {
              <option [value]="s"></option>
            }
          </datalist>
          @if (errorText(form.sport); as message) {
            <span class="field-error">{{ message }}</span>
          }
        </label>

        <div class="row">
          <label>
            {{ t('athleteForm.birthYear') }}
            <input type="number" inputmode="numeric" [formField]="form.birthYear" [attr.aria-invalid]="showError(form.birthYear)" />
            <span class="hint">{{ t('athleteForm.birthYearHint') }}</span>
            @if (errorText(form.birthYear); as message) {
              <span class="field-error">{{ message }}</span>
            }
          </label>
          <label>
            {{ t('athleteForm.sex') }}
            <select [formField]="form.sex">
              @for (sex of sexes; track sex) {
                <option [value]="sex">{{ t(sexLabel(sex)) }}</option>
              }
            </select>
          </label>
          <label>
            {{ t('athleteForm.bodyMass') }}
            <input type="number" step="0.1" [formField]="form.bodyMassKg" [attr.aria-invalid]="showError(form.bodyMassKg)" />
            @if (errorText(form.bodyMassKg); as message) {
              <span class="field-error">{{ message }}</span>
            }
          </label>
        </div>

        @if (error(); as message) {
          <p class="banner error" role="alert">{{ message }}</p>
        }

        <div class="dialog-actions">
          <button type="button" class="secondary" (click)="cancelled.emit()">{{ t('common.cancel') }}</button>
          <button type="submit" [disabled]="form().submitting()">
            {{ athlete() ? t('athleteForm.saveChanges') : t('athleteForm.add') }}
          </button>
        </div>
      </form>
    </app-modal-dialog>
  `,
})
export class AthleteForm {
  private readonly api = inject(ApiService);
  protected readonly t = inject(I18n).t;

  /** null adds a new athlete. */
  readonly athlete = input<AthleteDetails | null>(null);
  /** Existing sports, suggested while typing so the filter chips stay tidy. */
  readonly sports = input<string[]>([]);
  readonly saved = output<{ athlete: AthleteDetails; created: boolean }>();
  readonly cancelled = output<void>();

  protected readonly sexes: Sex[] = ['NotStated', 'Female', 'Male'];

  private readonly model = linkedSignal<AthleteRequest>(() => {
    const a = this.athlete();
    return a ? { displayName: a.displayName, sport: a.sport, birthYear: a.birthYear, sex: a.sex, bodyMassKg: a.bodyMassKg } : { ...EMPTY };
  });

  protected readonly form = form(this.model, (p) => {
    required(p.displayName, { message: 'athleteForm.error.nameRequired' });
    minLength(p.displayName, 2, { message: 'athleteForm.error.nameLength' });
    maxLength(p.displayName, 60, { message: 'athleteForm.error.nameLength' });
    required(p.sport, { message: 'athleteForm.error.sportRequired' });
    minLength(p.sport, 2, { message: 'athleteForm.error.sportLength' });
    maxLength(p.sport, 60, { message: 'athleteForm.error.sportLength' });
    required(p.birthYear, { message: 'athleteForm.error.birthYearRequired' });
    min(p.birthYear, THIS_YEAR - 70, { message: 'athleteForm.error.ageRange' });
    max(p.birthYear, THIS_YEAR - 10, { message: 'athleteForm.error.ageRange' });
    min(p.bodyMassKg, 30, { message: 'athleteForm.error.bodyMassRange' });
    max(p.bodyMassKg, 200, { message: 'athleteForm.error.bodyMassRange' });
  }, { name: 'athlete' });

  protected readonly error = signal<string | null>(null);

  protected sexLabel(sex: Sex): TranslationKey {
    return `sexOption.${sex}`;
  }

  protected showError(field: () => FieldView): boolean {
    const state = field();
    return state.touched() && state.invalid();
  }

  protected errorText(field: () => FieldView): string | null {
    const key = this.showError(field) ? field().errors()[0]?.message : undefined;
    return key ? this.t(key as TranslationKey) : null;
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.form, async () => {
      const existing = this.athlete();
      const request = this.model();
      try {
        const athlete = await firstValueFrom(
          existing ? this.api.editAthlete(existing.id, request) : this.api.addAthlete(request),
        );
        this.error.set(null);
        this.saved.emit({ athlete, created: !existing });
      } catch (e) {
        this.error.set(describeError(e, 'Coach', this.t));
      }
      return undefined;
    });
  }
}
