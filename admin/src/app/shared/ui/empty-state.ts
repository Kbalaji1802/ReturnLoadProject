import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Friendly empty-state placeholder for lists/tables with no data. */
@Component({
  selector: 'rl-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty">
      <mat-icon>{{ icon() }}</mat-icon>
      <p class="title">{{ title() }}</p>
      @if (message()) {
        <p class="msg">{{ message() }}</p>
      }
    </div>
  `,
  styles: [
    `
      .empty { display: grid; place-items: center; text-align: center; padding: 48px 16px; color: var(--mat-sys-on-surface-variant); }
      mat-icon { font-size: 48px; width: 48px; height: 48px; opacity: 0.5; margin-bottom: 8px; }
      .title { font-weight: 600; margin: 0; color: var(--mat-sys-on-surface); }
      .msg { margin: 4px 0 0; font-size: 0.85rem; }
    `,
  ],
})
export class EmptyState {
  readonly icon = input<string>('inbox');
  readonly title = input.required<string>();
  readonly message = input<string>('');
}
