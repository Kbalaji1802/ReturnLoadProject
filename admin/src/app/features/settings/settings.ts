import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';
import { environment } from '../../../environments/environment';
import { PageHeader } from '../../shared/ui/page-header';

@Component({
  selector: 'app-settings',
  imports: [MatButtonModule, MatIconModule, MatSlideToggleModule, PageHeader],
  template: `
    <div class="rl-page">
      <rl-page-header title="Settings" subtitle="Appearance, environment, and session" />

      <div class="stack">
        <section class="rl-surface card">
          <h2>Appearance</h2>
          <div class="line">
            <div>
              <div class="t">Dark theme</div>
              <div class="s">Switch between light and dark. Follows your system by default.</div>
            </div>
            <mat-slide-toggle [checked]="theme.mode() === 'dark'" (change)="theme.toggle()" />
          </div>
        </section>

        <section class="rl-surface card">
          <h2>Environment</h2>
          <div class="line"><div class="t">API base URL</div><code>{{ apiBaseUrl }}</code></div>
          <div class="line"><div class="t">Integrations</div>
            <div class="s">Maps, SMS/OTP and payment gateways are pending external configuration.</div>
          </div>
        </section>

        <section class="rl-surface card">
          <h2>Session</h2>
          <button mat-flat-button color="warn" (click)="logout()"><mat-icon>logout</mat-icon> Sign out</button>
        </section>
      </div>
    </div>
  `,
  styles: [
    `
      .stack { display: flex; flex-direction: column; gap: var(--rl-gap); max-width: 720px; }
      .card { padding: 20px 24px; }
      .card h2 { font-size: 1rem; font-weight: 600; margin: 0 0 12px; }
      .line { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 10px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
      .line:first-of-type { border-top: none; }
      .t { font-weight: 500; }
      .s { color: var(--mat-sys-on-surface-variant); font-size: 0.85rem; }
      code { background: var(--mat-sys-surface-container); padding: 4px 8px; border-radius: 6px; font-size: 0.85rem; }
    `,
  ],
})
export class Settings {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly theme = inject(ThemeService);
  protected readonly apiBaseUrl = environment.apiBaseUrl;

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
