import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

type Tone = 'neutral' | 'info' | 'success' | 'warn' | 'danger';

const MAP: Record<string, Tone> = {
  // Driver / vehicle / carrier
  Pending: 'warn', Active: 'success', Suspended: 'danger', Blocked: 'danger', Draft: 'neutral',
  Maintenance: 'warn',
  // Verification
  NotSubmitted: 'neutral', Submitted: 'info', UnderReview: 'info', Verified: 'success',
  Rejected: 'danger', Expired: 'danger',
  // Load / trip
  Posted: 'info', Matched: 'info', Booked: 'success', InTransit: 'info', Delivered: 'success',
  Completed: 'success', Cancelled: 'danger', Created: 'neutral', Assigned: 'info', Started: 'info',
};

const ICON: Record<Tone, string> = {
  neutral: 'radio_button_unchecked', info: 'sync', success: 'check_circle',
  warn: 'schedule', danger: 'error',
};

/** A coloured status pill mapping a domain status string to a tone + icon. */
@Component({
  selector: 'rl-status-chip',
  imports: [MatChipsModule, MatIconModule],
  template: `
    <mat-chip [class]="'tone-' + tone()" disableRipple>
      <mat-icon matChipAvatar>{{ icon() }}</mat-icon>
      {{ label() }}
    </mat-chip>
  `,
  styles: [
    `
      mat-chip { font-weight: 500; --mdc-chip-label-text-size: 12px; }
      .tone-neutral { --mdc-chip-elevated-container-color: var(--mat-sys-surface-container-high); }
      .tone-info    { --mdc-chip-elevated-container-color: color-mix(in srgb, var(--mat-sys-primary) 16%, transparent); }
      .tone-success { --mdc-chip-elevated-container-color: color-mix(in srgb, #16a34a 18%, transparent); }
      .tone-warn    { --mdc-chip-elevated-container-color: color-mix(in srgb, #d97706 20%, transparent); }
      .tone-danger  { --mdc-chip-elevated-container-color: color-mix(in srgb, var(--mat-sys-error) 18%, transparent); }
      mat-icon { font-size: 16px; height: 16px; width: 16px; }
    `,
  ],
})
export class StatusChip {
  readonly label = input.required<string>();
  protected readonly tone = computed<Tone>(() => MAP[this.label()] ?? 'neutral');
  protected readonly icon = computed(() => ICON[this.tone()]);
}
