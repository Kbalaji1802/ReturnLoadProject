import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** A dashboard statistic widget: icon, big value, label, and optional trend hint. */
@Component({
  selector: 'rl-stat-card',
  imports: [MatIconModule],
  template: `
    <div class="stat rl-surface">
      <div class="icon" [style.background]="tint()">
        <mat-icon>{{ icon() }}</mat-icon>
      </div>
      <div class="body">
        <div class="value">{{ value() }}</div>
        <div class="label">{{ label() }}</div>
        @if (hint()) {
          <div class="hint">{{ hint() }}</div>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .stat { display: flex; gap: 16px; align-items: center; padding: 18px 20px; }
      .icon {
        width: 48px; height: 48px; border-radius: 12px; display: grid; place-items: center;
        color: var(--mat-sys-primary); flex: none;
      }
      .icon mat-icon { font-size: 26px; width: 26px; height: 26px; }
      .value { font-size: 1.75rem; font-weight: 700; line-height: 1.1; letter-spacing: -0.02em; }
      .label { color: var(--mat-sys-on-surface-variant); font-size: 0.85rem; margin-top: 2px; }
      .hint { color: var(--mat-sys-on-surface-variant); font-size: 0.75rem; margin-top: 4px; }
    `,
  ],
})
export class StatCard {
  readonly icon = input.required<string>();
  readonly value = input.required<string | number>();
  readonly label = input.required<string>();
  readonly hint = input<string>('');
  readonly tint = input<string>('color-mix(in srgb, var(--mat-sys-primary) 14%, transparent)');
}
