import { Injectable, computed, signal } from '@angular/core';
import { Role } from './models';

export interface Persona {
  user: string;
  role: Role;
  label: string;
}

export const PERSONAS: readonly Persona[] = [
  { user: 'lena', role: 'Coach', label: 'Lena · Athletic coach' },
  { user: 'jonas', role: 'Physio', label: 'Jonas · Physio' },
  { user: 'mia', role: 'Nutrition', label: 'Mia · Nutrition scientist' },
  { user: 'luca', role: 'Kitchen', label: 'Luca · Kitchen' },
  { user: 'admin', role: 'Admin', label: 'Admin · Data protection' },
];

/** Which screens each role may open. The API enforces the same rules; this only keeps the UI tidy. */
export const SCREENS_BY_ROLE: Record<Role, readonly string[]> = {
  Coach: ['dashboard', 'imports'],
  Physio: ['dashboard', 'imports'],
  Nutrition: ['dashboard', 'kitchen'],
  Kitchen: ['kitchen'],
  Admin: ['audit'],
  Athlete: [],
};

const STORAGE_KEY = 'athlete-day.persona';

/** Demo persona switcher. In production the identity comes from Entra ID instead. */
@Injectable({ providedIn: 'root' })
export class PersonaService {
  private readonly selected = signal<Persona>(this.restore());

  readonly current = this.selected.asReadonly();
  readonly screens = computed(() => SCREENS_BY_ROLE[this.selected().role]);

  switchTo(user: string): void {
    const persona = PERSONAS.find((p) => p.user === user);
    if (!persona) return;
    this.selected.set(persona);
    try {
      localStorage.setItem(STORAGE_KEY, user);
    } catch {
      // Storage can be unavailable (private mode); the switcher still works for this session.
    }
  }

  canOpen(screen: string): boolean {
    return this.screens().includes(screen);
  }

  private restore(): Persona {
    // ?as=luca opens the app directly as a persona, e.g. the kitchen screen in a second window.
    const fromUrl = PERSONAS.find((p) => p.user === new URLSearchParams(location.search).get('as'));
    if (fromUrl) return fromUrl;
    try {
      const user = localStorage.getItem(STORAGE_KEY);
      return PERSONAS.find((p) => p.user === user) ?? PERSONAS[0];
    } catch {
      return PERSONAS[0];
    }
  }
}
