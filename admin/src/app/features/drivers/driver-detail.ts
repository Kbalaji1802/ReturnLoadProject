import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api/api.service';
import { DriverDetailView, DocumentView, ReviewView } from '../../core/api/api.models';
import {
  DRIVER_STATUS, DRIVER_AVAILABILITY, VEHICLE_STATUS, VEHICLE_TYPE, VERIFICATION_STATUS,
  DOCUMENT_TYPE, TRIP_STATUS, label,
} from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';

/** Admin driver-details page (Part 11): identity, verification, availability, fleet, documents, reviews. */
@Component({
  selector: 'app-driver-detail',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip],
  template: `
    <div class="rl-page">
      <rl-page-header [title]="d()?.fullName ?? 'Driver'" subtitle="Driver profile">
        <a actions mat-stroked-button routerLink="/drivers"><mat-icon>arrow_back</mat-icon> Back to drivers</a>
      </rl-page-header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      @else if (d(); as v) {
        <div class="grid">
          <section class="rl-surface card">
            <div class="who">
              @if (v.photoUrl) { <img class="avatar" [src]="v.photoUrl" alt="" /> }
              @else { <span class="avatar ph"><mat-icon>person</mat-icon></span> }
              <div>
                <h2>{{ v.fullName }}</h2>
                <div class="chips">
                  <rl-status-chip [label]="ds(v.status)" />
                  <rl-status-chip [label]="av(v.availability)" />
                </div>
              </div>
            </div>
            <dl>
              <div><dt>Phone</dt><dd>{{ v.mobile }}</dd></div>
              <div><dt>Email</dt><dd>{{ v.email ?? '—' }}</dd></div>
              <div><dt>Licence</dt><dd>{{ v.licence }}</dd></div>
              <div><dt>Company</dt><dd>{{ v.company ?? '—' }}</dd></div>
              <div><dt>Rating</dt><dd>{{ v.rating != null ? (v.rating + ' ★ (' + v.ratingCount + ')') : 'No ratings yet' }}</dd></div>
              <div><dt>Completed trips</dt><dd>{{ v.completedTrips }}</dd></div>
              <div><dt>Current trip</dt><dd>
                @if (v.currentTripId) {
                  <a routerLink="/trips/{{ v.currentTripId }}">{{ ts(v.currentTripStatus) }}</a>
                } @else { None }
              </dd></div>
            </dl>
          </section>

          <section class="rl-surface card">
            <h3>Vehicles ({{ v.vehicles.length }})</h3>
            @if (v.vehicles.length === 0) { <p class="muted">No vehicles registered.</p> }
            @for (veh of v.vehicles; track veh.id) {
              <a class="row link" routerLink="/vehicles/{{ veh.id }}">
                <mat-icon>local_shipping</mat-icon>
                <span class="grow">{{ veh.registrationNumber }} · {{ vt(veh.type) }} · {{ veh.maxPayloadKg }} kg</span>
                <rl-status-chip [label]="vs(veh.status)" />
              </a>
            }
          </section>

          <section class="rl-surface card">
            <h3>Documents ({{ docs().length }})</h3>
            @if (docs().length === 0) { <p class="muted">No documents uploaded.</p> }
            @for (doc of docs(); track doc.id) {
              <div class="row">
                <mat-icon>description</mat-icon>
                <span class="grow">{{ dt(doc.type) }} @if (doc.documentNumber) { · {{ doc.documentNumber }} }
                  @if (doc.rejectionReason) { <em class="reason">— {{ doc.rejectionReason }}</em> }</span>
                <rl-status-chip [label]="vfy(doc.verificationStatus)" />
              </div>
            }
          </section>

          <section class="rl-surface card">
            <h3>Reviews ({{ reviews().length }})</h3>
            @if (reviews().length === 0) { <p class="muted">No reviews yet.</p> }
            @for (r of reviews(); track r.id) {
              <div class="review">
                <div class="stars">{{ '★'.repeat(r.stars) }}<span class="dim">{{ '★'.repeat(5 - r.stars) }}</span></div>
                <p>{{ r.comment ?? '—' }}</p>
                <small class="muted">{{ r.createdAtUtc | date:'d MMM y' }}</small>
              </div>
            }
          </section>
        </div>
      } @else {
        <div class="rl-surface card"><p class="muted">{{ error() ?? 'Driver not found.' }}</p></div>
      }
    </div>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--rl-gap); margin-top: 16px; }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
    .card { padding: 20px; }
    .card h2 { font-size: 1.15rem; margin: 0 0 6px; }
    .card h3 { font-size: 0.95rem; margin: 0 0 12px; }
    .who { display: flex; gap: 16px; align-items: center; margin-bottom: 16px; }
    .avatar { width: 64px; height: 64px; border-radius: 50%; object-fit: cover; }
    .avatar.ph { display: inline-flex; align-items: center; justify-content: center;
      background: var(--mat-sys-surface-container-high); color: var(--mat-sys-on-surface-variant); }
    .avatar.ph mat-icon { font-size: 34px; height: 34px; width: 34px; }
    .chips { display: flex; gap: 8px; margin-top: 4px; }
    dl { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 20px; margin: 0; }
    dl dt { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--mat-sys-on-surface-variant); }
    dl dd { margin: 2px 0 0; font-weight: 500; }
    .row { display: flex; align-items: center; gap: 10px; padding: 10px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .row.link { text-decoration: none; color: inherit; }
    .row.link:hover { color: var(--mat-sys-primary); }
    .grow { flex: 1; }
    .reason { color: var(--mat-sys-error); font-style: italic; }
    .review { padding: 10px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .review p { margin: 4px 0; }
    .stars { color: #d97706; letter-spacing: 2px; } .dim { opacity: 0.25; }
    .muted { color: var(--mat-sys-on-surface-variant); }
  `],
})
export class DriverDetail {
  private readonly api = inject(ApiService);
  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly d = signal<DriverDetailView | null>(null);
  protected readonly docs = signal<DocumentView[]>([]);
  protected readonly reviews = signal<ReviewView[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) this.load(id);
    });
  }

  protected ds = (v: number) => label(DRIVER_STATUS, v);
  protected av = (v: number) => label(DRIVER_AVAILABILITY, v);
  protected vs = (v: number) => label(VEHICLE_STATUS, v);
  protected vt = (v: number) => label(VEHICLE_TYPE, v);
  protected dt = (v: number) => label(DOCUMENT_TYPE, v);
  protected vfy = (v: number) => label(VERIFICATION_STATUS, v);
  protected ts = (v: number | null) => (v == null ? '—' : label(TRIP_STATUS, v));

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get<DriverDetailView>(`drivers/${id}`).subscribe({
      next: (d) => {
        this.d.set(d);
        this.loading.set(false);
        this.api.get<DocumentView[]>(`documents?ownerType=0&ownerId=${id}`).subscribe({ next: (x) => this.docs.set(x), error: () => {} });
        this.api.get<ReviewView[]>(`reviews/for-user/${d.userProfileId}`).subscribe({ next: (x) => this.reviews.set(x), error: () => {} });
      },
      error: () => { this.error.set('Could not load this driver.'); this.loading.set(false); },
    });
  }
}
