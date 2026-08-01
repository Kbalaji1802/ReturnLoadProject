import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api/api.service';
import { VehicleDetailView, DocumentView } from '../../core/api/api.models';
import { VEHICLE_STATUS, VEHICLE_TYPE, VERIFICATION_STATUS, DOCUMENT_TYPE, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';

/** Admin vehicle-details page (Part 11): identity, capacity, owner, and its verification documents. */
@Component({
  selector: 'app-vehicle-detail',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip],
  template: `
    <div class="rl-page">
      <rl-page-header [title]="v()?.registrationNumber ?? 'Vehicle'" subtitle="Vehicle profile">
        <a actions mat-stroked-button routerLink="/vehicles"><mat-icon>arrow_back</mat-icon> Back to vehicles</a>
      </rl-page-header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      @else if (v(); as x) {
        <div class="grid">
          <section class="rl-surface card">
            <div class="hero"><mat-icon>local_shipping</mat-icon></div>
            <h2>{{ x.registrationNumber }}</h2>
            <rl-status-chip [label]="vs(x.status)" />
            <dl>
              <div><dt>Type</dt><dd>{{ vt(x.type) }}</dd></div>
              <div><dt>Capacity</dt><dd>{{ x.maxPayloadKg }} kg</dd></div>
              <div><dt>Volume</dt><dd>{{ x.volumeCubicMetres != null ? (x.volumeCubicMetres + ' m³') : '—' }}</dd></div>
              <div><dt>Owner / company</dt><dd>{{ x.company ?? '—' }}</dd></div>
              <div><dt>Registered</dt><dd>{{ x.createdAtUtc | date:'d MMM y' }}</dd></div>
            </dl>
          </section>

          <section class="rl-surface card">
            <h3>Documents ({{ docs().length }})</h3>
            @if (docs().length === 0) { <p class="muted">No documents uploaded for this vehicle.</p> }
            @for (doc of docs(); track doc.id) {
              <div class="row">
                <mat-icon>description</mat-icon>
                <div class="grow">
                  <div>{{ dt(doc.type) }} @if (doc.documentNumber) { · {{ doc.documentNumber }} }</div>
                  <small class="muted">Expiry: {{ doc.expiresOn ? (doc.expiresOn | date:'d MMM y') : '—' }}</small>
                </div>
                <rl-status-chip [label]="vfy(doc.verificationStatus)" />
              </div>
            }
          </section>
        </div>
      } @else {
        <div class="rl-surface card"><p class="muted">{{ error() ?? 'Vehicle not found.' }}</p></div>
      }
    </div>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--rl-gap); margin-top: 16px; }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
    .card { padding: 20px; }
    .card h2 { font-size: 1.15rem; margin: 8px 0 6px; }
    .card h3 { font-size: 0.95rem; margin: 0 0 12px; }
    .hero { height: 120px; border-radius: 12px; display: flex; align-items: center; justify-content: center;
      background: var(--mat-sys-surface-container-high); margin-bottom: 12px; }
    .hero mat-icon { font-size: 56px; height: 56px; width: 56px; color: var(--mat-sys-primary); }
    dl { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 20px; margin: 16px 0 0; }
    dl dt { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--mat-sys-on-surface-variant); }
    dl dd { margin: 2px 0 0; font-weight: 500; }
    .row { display: flex; align-items: center; gap: 10px; padding: 10px 0; border-top: 1px solid var(--mat-sys-outline-variant); }
    .grow { flex: 1; }
    .muted { color: var(--mat-sys-on-surface-variant); }
  `],
})
export class VehicleDetail {
  private readonly api = inject(ApiService);
  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly v = signal<VehicleDetailView | null>(null);
  protected readonly docs = signal<DocumentView[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => { const id = this.id(); if (id) this.load(id); });
  }

  protected vs = (v: number) => label(VEHICLE_STATUS, v);
  protected vt = (v: number) => label(VEHICLE_TYPE, v);
  protected dt = (v: number) => label(DOCUMENT_TYPE, v);
  protected vfy = (v: number) => label(VERIFICATION_STATUS, v);

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get<VehicleDetailView>(`vehicles/${id}`).subscribe({
      next: (v) => {
        this.v.set(v);
        this.loading.set(false);
        this.api.get<DocumentView[]>(`documents?ownerType=1&ownerId=${id}`).subscribe({ next: (x) => this.docs.set(x), error: () => {} });
      },
      error: () => { this.error.set('Could not load this vehicle.'); this.loading.set(false); },
    });
  }
}
