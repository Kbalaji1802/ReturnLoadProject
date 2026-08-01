import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api/api.service';
import { TripDetailView, TripLiveView, TrackingPointView, ReviewView } from '../../core/api/api.models';
import { TRIP_STATUS, TRIP_LIFECYCLE_ORDER, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';

/** Admin trip-details page (Part 11): timeline, participants, live position + ETA, breadcrumb, reviews. */
@Component({
  selector: 'app-trip-detail',
  imports: [DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip],
  template: `
    <div class="rl-page">
      <rl-page-header title="Trip" [subtitle]="t()?.originAddress ? (t()!.originAddress + ' → ' + (t()!.destinationAddress ?? '—')) : 'Trip details'">
        <a actions mat-stroked-button routerLink="/trips"><mat-icon>arrow_back</mat-icon> Back to trips</a>
      </rl-page-header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      @else if (t(); as x) {
        <div class="grid">
          <section class="rl-surface card">
            <h3>Timeline</h3>
            <ol class="timeline">
              @for (s of lifecycle(); track s.status) {
                <li [class.done]="s.done" [class.current]="s.current">
                  <mat-icon>{{ s.done ? 'check_circle' : 'radio_button_unchecked' }}</mat-icon>
                  <span>{{ s.labelText }}</span>
                </li>
              }
            </ol>
            @if (x.pickupAutoConfirmed || x.deliveryAutoConfirmed) {
              <p class="muted auto">
                <mat-icon>info</mat-icon>
                @if (x.pickupAutoConfirmed) { Pickup was auto-confirmed (owner did not confirm in time). }
                @if (x.deliveryAutoConfirmed) { Delivery was auto-confirmed. }
              </p>
            }
          </section>

          <section class="rl-surface card">
            <h3>Details</h3>
            <div class="statusline"><rl-status-chip [label]="ts(x.status)" /></div>
            <dl>
              <div><dt>Driver</dt><dd><a routerLink="/drivers/{{ x.driverProfileId }}">{{ x.driverName ?? '—' }}</a></dd></div>
              <div><dt>Vehicle</dt><dd><a routerLink="/vehicles/{{ x.vehicleId }}">{{ x.vehicleRegistration ?? '—' }}</a></dd></div>
              <div><dt>Started</dt><dd>{{ x.startedAtUtc ? (x.startedAtUtc | date:'d MMM y, HH:mm') : '—' }}</dd></div>
              <div><dt>Completed</dt><dd>{{ x.completedAtUtc ? (x.completedAtUtc | date:'d MMM y, HH:mm') : '—' }}</dd></div>
              <div><dt>Distance remaining</dt><dd>{{ live()?.distanceRemainingKm != null ? (live()!.distanceRemainingKm + ' km') : '—' }}</dd></div>
              <div><dt>ETA</dt><dd>{{ live()?.etaMinutes != null ? eta(live()!.etaMinutes!) : '—' }}</dd></div>
              <div><dt>Current location</dt><dd>{{ locationText() }}</dd></div>
              <div><dt>Last update</dt><dd>{{ live()?.lastUpdateUtc ? (live()!.lastUpdateUtc | date:'d MMM y, HH:mm') : '—' }}</dd></div>
            </dl>
          </section>

          <section class="rl-surface card wide">
            <h3>Tracking history ({{ points().length }})</h3>
            @if (points().length === 0) { <p class="muted">No location points recorded yet.</p> }
            @else {
              <div class="scroll">
                @for (p of pointsDesc(); track p.id) {
                  <div class="pt">
                    <mat-icon>place</mat-icon>
                    <span class="grow">{{ p.latitude | number:'1.4-4' }}, {{ p.longitude | number:'1.4-4' }}</span>
                    <span class="muted">{{ p.speedKph != null ? ((p.speedKph | number:'1.0-0') + ' km/h') : '' }}</span>
                    <span class="muted">{{ p.capturedAtUtc | date:'HH:mm:ss' }}</span>
                  </div>
                }
              </div>
            }
          </section>

          <section class="rl-surface card wide">
            <h3>Reviews ({{ reviews().length }})</h3>
            @if (reviews().length === 0) { <p class="muted">No reviews for this trip.</p> }
            @for (r of reviews(); track r.id) {
              <div class="review">
                <span class="stars">{{ '★'.repeat(r.stars) }}<span class="dim">{{ '★'.repeat(5 - r.stars) }}</span></span>
                <span class="grow">{{ r.comment ?? '—' }}</span>
                <small class="muted">{{ r.createdAtUtc | date:'d MMM y' }}</small>
              </div>
            }
          </section>
        </div>
      } @else {
        <div class="rl-surface card"><p class="muted">{{ error() ?? 'Trip not found.' }}</p></div>
      }
    </div>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--rl-gap); margin-top: 16px; }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
    .card { padding: 20px; } .card.wide { grid-column: 1 / -1; }
    .card h3 { font-size: 0.95rem; margin: 0 0 12px; }
    .timeline { list-style: none; margin: 0; padding: 0; }
    .timeline li { display: flex; align-items: center; gap: 10px; padding: 6px 0; color: var(--mat-sys-on-surface-variant); }
    .timeline li.done { color: inherit; } .timeline li.done mat-icon { color: #16a34a; }
    .timeline li.current { font-weight: 700; }
    .statusline { margin-bottom: 12px; }
    dl { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 20px; margin: 0; }
    dl dt { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--mat-sys-on-surface-variant); }
    dl dd { margin: 2px 0 0; font-weight: 500; }
    .scroll { max-height: 320px; overflow-y: auto; }
    .pt, .review { display: flex; align-items: center; gap: 10px; padding: 8px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .grow { flex: 1; }
    .stars { color: #d97706; letter-spacing: 2px; } .dim { opacity: 0.25; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .auto { display: flex; align-items: center; gap: 6px; margin-top: 12px; font-size: 0.85rem; }
    .auto mat-icon { font-size: 18px; height: 18px; width: 18px; }
  `],
})
export class TripDetail {
  private readonly api = inject(ApiService);
  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly t = signal<TripDetailView | null>(null);
  protected readonly live = signal<TripLiveView | null>(null);
  protected readonly points = signal<TrackingPointView[]>([]);
  protected readonly reviews = signal<ReviewView[]>([]);
  protected readonly error = signal<string | null>(null);

  protected readonly pointsDesc = computed(() => [...this.points()].reverse());
  protected readonly lifecycle = computed(() => {
    const status = this.t()?.status ?? 0;
    const currentPos = TRIP_LIFECYCLE_ORDER.indexOf(status);
    return TRIP_LIFECYCLE_ORDER.map((s, pos) => ({
      status: s, labelText: label(TRIP_STATUS, s),
      done: currentPos >= 0 && pos <= currentPos, current: pos === currentPos,
    }));
  });
  protected readonly locationText = computed(() => {
    const l = this.live();
    return l?.hasLocation && l.latitude != null && l.longitude != null
      ? `${l.latitude.toFixed(4)}, ${l.longitude.toFixed(4)}` : '—';
  });

  constructor() {
    effect(() => { const id = this.id(); if (id) this.load(id); });
  }

  protected ts = (v: number) => label(TRIP_STATUS, v);
  protected eta = (m: number) => (m >= 60 ? `${Math.floor(m / 60)}h ${m % 60}m` : `${m}m`);

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get<TripDetailView>(`trips/${id}/detail`).subscribe({
      next: (t) => {
        this.t.set(t);
        this.loading.set(false);
        this.api.get<TripLiveView>(`trips/${id}/tracking/live`).subscribe({ next: (x) => this.live.set(x), error: () => {} });
        this.api.get<TrackingPointView[]>(`trips/${id}/tracking`).subscribe({ next: (x) => this.points.set(x), error: () => {} });
        this.api.get<ReviewView[]>(`reviews/trip/${id}`).subscribe({ next: (x) => this.reviews.set(x), error: () => {} });
      },
      error: () => { this.error.set('Could not load this trip.'); this.loading.set(false); },
    });
  }
}
