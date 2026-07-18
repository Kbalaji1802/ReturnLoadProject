import { Component, input } from '@angular/core';

/** Consistent page heading with a title, optional subtitle, and a projected actions slot. */
@Component({
  selector: 'rl-page-header',
  template: `
    <header class="header">
      <div>
        <h1 class="rl-page-title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="rl-page-subtitle">{{ subtitle() }}</p>
        }
      </div>
      <span class="rl-spacer"></span>
      <ng-content select="[actions]" />
    </header>
  `,
  styles: [`.header { display: flex; align-items: flex-start; gap: 16px; margin-bottom: 20px; }`],
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
}
