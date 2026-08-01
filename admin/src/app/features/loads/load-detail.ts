import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ApiService } from '../../core/api/api.service';
import { LoadView, BookingRequestView, TripView, DriverSummary, TripLiveView } from '../../core/api/api.models';
import { LOAD_STATUS, CARGO_TYPE, AREA_TYPE, BOOKING_STATUS, TRIP_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';

/** Admin load-details page (Part 11): route, requested drivers, approved driver, current status, GPS. */
@Component({
  selector: 'app-load-detail',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip],
  template: `
    <div class="rl-page">
      <rl-page-header title="Load" [subtitle]="l() ? (l()!.originAddress + ' → ' + (l()!.destinationAddress ?? '—')) : 'Load details'">
        <a actions mat-stroked-button routerLink="/loads"><mat-icon>arrow_back</mat-icon> Back to loads</a>
      </rl-page-header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      @else if (l(); as x) {
        <div class="grid">
          <section class="rl-surface card">
            <h3>Route</h3>
            <div class="statusline"><rl-status-chip [label]="ls(x.status)" /></div>
            <dl>
              <div><dt>Pickup</dt><dd>{{ x.originAddress ?? '—' }}</dd></div>
              <div><dt>Destination</dt><dd>{{ x.destinationAddress ?? '—' }}</dd></div>
              <div><dt>Distance</dt><dd>{{ x.distanceKm != null ? (x.distanceKm + ' km') : '—' }}</dd></div>
              <div><dt>ETA (route)</dt><dd>{{ x.estimatedDurationMinutes != null ? eta(x.estimatedDurationMinutes) : '—' }}</dd></div>
              <div><dt>Pickup area</dt><dd>{{ at(x.pickupAreaType) }}</dd></div>
              <div><dt>Cargo</dt><dd>{{ cg(x.cargoType) }} · {{ x.weightKg }} kg</dd></div>
              <div><dt>Price</dt><dd>{{ x.offeredPriceInr != null ? ('₹' + x.offeredPriceInr) : '—' }}</dd></div>
              <div><dt>Pickup window</dt><dd>{{ x.pickupStart | date:'d MMM, HH:mm' }} – {{ x.pickupEnd | date:'HH:mm' }}</dd></div>
            </dl>
          </section>

          <section class="rl-surface card">
            <h3>Trip & GPS</h3>
            @if (trip(); as tr) {
              <div class="row">
                <mat-icon>commute</mat-icon>
                <a class="grow" routerLink="/trips/{{ tr.id }}">Open trip</a>
                <rl-status-chip [label]="ts(tr.status)" />
              </div>
              <dl>
                <div><dt>Distance remaining</dt><dd>{{ live()?.distanceRemainingKm != null ? (live()!.distanceRemainingKm + ' km') : '—' }}</dd></div>
                <div><dt>ETA</dt><dd>{{ live()?.etaMinutes != null ? eta(live()!.etaMinutes!) : '—' }}</dd></div>
                <div><dt>Location</dt><dd>{{ locationText() }}</dd></div>
                <div><dt>Last update</dt><dd>{{ live()?.lastUpdateUtc ? (live()!.lastUpdateUtc | date:'HH:mm') : '—' }}</dd></div>
              </dl>
            } @else {
              <p class="muted">No trip has started for this load yet.</p>
            }
          </section>

          <section class="rl-surface card wide">
            <h3>Requested drivers ({{ requests().length }})</h3>
            @if (requests().length === 0) { <p class="muted">No drivers have requested this load yet.</p> }
            @for (r of requests(); track r.id) {
              <div class="row" [class.approved]="r.status === 1">
                <mat-icon>{{ r.status === 1 ? 'how_to_reg' : 'person' }}</mat-icon>
                <a class="grow" routerLink="/drivers/{{ r.driverProfileId }}">{{ driverName(r.driverProfileId) }}</a>
                <rl-status-chip [label]="bs(r.status)" />
              </div>
            }
            @if (approvedDriverId()) { <p class="muted approved-note"><mat-icon>check</mat-icon> Approved driver highlighted above.</p> }
          </section>
        </div>
      } @else {
        <div class="rl-surface card"><p class="muted">{{ error() ?? 'Load not found.' }}</p></div>
      }
    </div>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--rl-gap); margin-top: 16px; }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
    .card { padding: 20px; } .card.wide { grid-column: 1 / -1; }
    .card h3 { font-size: 0.95rem; margin: 0 0 12px; }
    .statusline { margin-bottom: 12px; }
    dl { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 20px; margin: 0; }
    dl dt { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--mat-sys-on-surface-variant); }
    dl dd { margin: 2px 0 0; font-weight: 500; }
    .row { display: flex; align-items: center; gap: 10px; padding: 10px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .row.approved { background: color-mix(in srgb, #16a34a 8%, transparent); border-radius: 8px; padding-left: 8px; }
    .grow { flex: 1; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .approved-note { display: flex; align-items: center; gap: 6px; margin-top: 10px; font-size: 0.85rem; }
    .approved-note mat-icon { font-size: 18px; height: 18px; width: 18px; color: #16a34a; }
  `],
})
export class LoadDetail {
  private readonly api = inject(ApiService);
  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly l = signal<LoadView | null>(null);
  protected readonly requests = signal<BookingRequestView[]>([]);
  protected readonly trip = signal<TripView | null>(null);
  protected readonly live = signal<TripLiveView | null>(null);
  protected readonly drivers = signal<DriverSummary[]>([]);
  protected readonly error = signal<string | null>(null);

  protected readonly approvedDriverId = computed(() => this.requests().find((r) => r.status === 1)?.driverProfileId ?? null);
  protected readonly locationText = computed(() => {
    const v = this.live();
    return v?.hasLocation && v.latitude != null && v.longitude != null ? `${v.latitude.toFixed(4)}, ${v.longitude.toFixed(4)}` : '—';
  });

  constructor() {
    effect(() => { const id = this.id(); if (id) this.load(id); });
  }

  protected ls = (v: number) => label(LOAD_STATUS, v);
  protected cg = (v: number) => label(CARGO_TYPE, v);
  protected at = (v: number) => label(AREA_TYPE, v);
  protected bs = (v: number) => label(BOOKING_STATUS, v);
  protected ts = (v: number) => label(TRIP_STATUS, v);
  protected eta = (m: number) => (m >= 60 ? `${Math.floor(m / 60)}h ${m % 60}m` : `${m}m`);

  protected driverName(driverProfileId: string): string {
    const d = this.drivers().find((x) => x.id === driverProfileId);
    return d?.fullName ?? `Driver ${driverProfileId.slice(0, 8)}…`;
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      load: this.api.get<LoadView>(`loads/${id}`),
      bookings: this.api.get<BookingRequestView[]>('bookings').pipe(catchError(() => of([] as BookingRequestView[]))),
      drivers: this.api.get<DriverSummary[]>('drivers').pipe(catchError(() => of([] as DriverSummary[]))),
      trip: this.api.get<TripView>(`trips/for-load/${id}`).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ load, bookings, drivers, trip }) => {
        this.l.set(load);
        this.requests.set(bookings.filter((b) => b.loadId === id));
        this.drivers.set(drivers);
        this.trip.set(trip);
        this.loading.set(false);
        if (trip) {
          this.api.get<TripLiveView>(`trips/${trip.id}/tracking/live`).subscribe({ next: (x) => this.live.set(x), error: () => {} });
        }
      },
      error: () => { this.error.set('Could not load this load.'); this.loading.set(false); },
    });
  }
}
