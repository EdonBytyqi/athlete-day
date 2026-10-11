import { Component, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormField, form, max, maxLength, min, minLength, required, submit } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDetails, AthleteRequest, Sex } from '../api/models';
import { ModalDialog } from '../shared/modal-dialog';

const THIS_YEAR = new Date().getFullYear();

const EMPTY: AthleteRequest = { displayName: '', sport: '', birthYear: null, sex: 'NotStated', bodyMassKg: 70 };

/** Add or edit an athlete. The same rules as AthleteRequest in AthleteDay.Contracts; the API checks them again. */
@Component({
  selector: 'app-athlete-form',
  imports: [FormField, ModalDialog],
  template: `
    <app-modal-dialog kind="athlete-form" [heading]="athlete() ? 'Edit ' + athlete()!.displayName : 'Add athlete'" (closed)="cancelled.emit()">
      <form class="form" novalidate (submit)="save($event)">
        @if (athlete(); as a) {
          <p class="muted">Code {{ a.code }} stays the same.</p>
        } @else {
          <p class="muted">The code (ATH-…) is assigned automatically. Use invented names only.</p>
        }

        <label>
          Name
          <input autocomplete="off" [formField]="form.displayName" [attr.aria-invalid]="showError(form.displayName)" />
          @if (showError(form.displayName)) {
            <span class="field-error">{{ form.displayName().errors()[0].message }}</span>
          }
        </label>

        <label>
          Sport
          <input list="known-sports" autocomplete="off" [formField]="form.sport" [attr.aria-invalid]="showError(form.sport)" />
          <datalist id="known-sports">
            @for (s of sports(); track s) {
              <option [value]="s"></option>
            }
          </datalist>
          @if (showError(form.sport)) {
            <span class="field-error">{{ form.sport().errors()[0].message }}</span>
          }
        </label>

        <div class="row">
          <label>
            Birth year
            <input type="number" inputmode="numeric" [formField]="form.birthYear" [attr.aria-invalid]="showError(form.birthYear)" />
            <span class="hint">Only the year is stored.</span>
            @if (showError(form.birthYear)) {
              <span class="field-error">{{ form.birthYear().errors()[0].message }}</span>
            }
          </label>
          <label>
            Sex
            <select [formField]="form.sex">
              @for (option of sexes; track option.value) {
                <option [value]="option.value">{{ option.label }}</option>
              }
            </select>
          </label>
          <label>
            Body mass (kg)
            <input type="number" step="0.1" [formField]="form.bodyMassKg" [attr.aria-invalid]="showError(form.bodyMassKg)" />
            @if (showError(form.bodyMassKg)) {
              <span class="field-error">{{ form.bodyMassKg().errors()[0].message }}</span>
            }
          </label>
        </div>

        @if (error(); as message) {
          <p class="banner error" role="alert">{{ message }}</p>
        }

        <div class="dialog-actions">
          <button type="button" class="secondary" (click)="cancelled.emit()">Cancel</button>
          <button type="submit" [disabled]="form().submitting()">{{ athlete() ? 'Save changes' : 'Add athlete' }}</button>
        </div>
      </form>
    </app-modal-dialog>
  `,
})
export class AthleteForm {
  private readonly api = inject(ApiService);

  /** null adds a new athlete. */
  readonly athlete = input<AthleteDetails | null>(null);
  /** Existing sports, suggested while typing so the filter chips stay tidy. */
  readonly sports = input<string[]>([]);
  readonly saved = output<{ athlete: AthleteDetails; created: boolean }>();
  readonly cancelled = output<void>();

  protected readonly sexes: { value: Sex; label: string }[] = [
    { value: 'NotStated', label: 'Not stated' },
    { value: 'Female', label: 'Female' },
    { value: 'Male', label: 'Male' },
  ];

  private readonly model = linkedSignal<AthleteRequest>(() => {
    const a = this.athlete();
    return a ? { displayName: a.displayName, sport: a.sport, birthYear: a.birthYear, sex: a.sex, bodyMassKg: a.bodyMassKg } : { ...EMPTY };
  });

  protected readonly form = form(this.model, (p) => {
    required(p.displayName, { message: 'Enter a name.' });
    minLength(p.displayName, 2, { message: 'Name must be 2 to 60 characters.' });
    maxLength(p.displayName, 60, { message: 'Name must be 2 to 60 characters.' });
    required(p.sport, { message: 'Enter a sport.' });
    minLength(p.sport, 2, { message: 'Sport must be 2 to 60 characters.' });
    maxLength(p.sport, 60, { message: 'Sport must be 2 to 60 characters.' });
    required(p.birthYear, { message: 'Enter the birth year.' });
    min(p.birthYear, THIS_YEAR - 70, { message: 'The birth year must give an age between 10 and 70.' });
    max(p.birthYear, THIS_YEAR - 10, { message: 'The birth year must give an age between 10 and 70.' });
    min(p.bodyMassKg, 30, { message: 'Body mass must be between 30 and 200 kg.' });
    max(p.bodyMassKg, 200, { message: 'Body mass must be between 30 and 200 kg.' });
  }, { name: 'athlete' });

  protected readonly error = signal<string | null>(null);

  protected showError(field: () => { touched(): boolean; invalid(): boolean }): boolean {
    const state = field();
    return state.touched() && state.invalid();
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
        this.error.set(describeError(e, 'Coach'));
      }
      return undefined;
    });
  }
}
