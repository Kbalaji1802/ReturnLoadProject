import { Injectable, signal } from '@angular/core';

type ThemeMode = 'light' | 'dark';
const KEY = 'returnload.admin.theme';

/**
 * Light/dark theme control. Drives the CSS `color-scheme` on the document root, which the
 * M3 theme (theme-type: color-scheme) follows automatically. Persists the choice.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _mode = signal<ThemeMode>(this.initial());
  readonly mode = this._mode.asReadonly();

  constructor() {
    this.apply(this._mode());
  }

  toggle(): void {
    this.set(this._mode() === 'dark' ? 'light' : 'dark');
  }

  set(mode: ThemeMode): void {
    this._mode.set(mode);
    localStorage.setItem(KEY, mode);
    this.apply(mode);
  }

  private apply(mode: ThemeMode): void {
    document.documentElement.style.colorScheme = mode;
  }

  private initial(): ThemeMode {
    const saved = localStorage.getItem(KEY);
    if (saved === 'light' || saved === 'dark') {
      return saved;
    }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
