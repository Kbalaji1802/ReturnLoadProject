import { Component, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api/api.service';
import { DriverSummary, DocumentView, LoadView } from '../../core/api/api.models';
import { CARGO_TYPE, DRIVER_STATUS, LOAD_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatCard } from '../../shared/ui/stat-card';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

@Component({
  selector: 'app-dashboard',
  imports: [
    RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule,
    PageHeader, StatCard, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Dashboard" subtitle="Operational overview of the ReturnLoad marketplace">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }

      <section class="rl-stat-grid">
        <rl-stat-card icon="badge" [value]="drivers().length" label="Total drivers"
          [hint]="activeDrivers() + ' active'" />
        <rl-stat-card icon="verified" [value]="pendingDocs().length" label="Documents pending review"
          tint="color-mix(in srgb, #d97706 16%, transparent)" />
        <rl-stat-card icon="inventory_2" [value]="loads().length" label="Available loads"
          tint="color-mix(in srgb, #16a34a 16%, transparent)" />
        <rl-stat-card icon="local_shipping" [value]="activeDrivers()" label="Verified &amp; ready"
          tint="color-mix(in srgb, var(--mat-sys-tertiary) 18%, transparent)" />
      </section>

      <div class="split">
        <section class="rl-surface panel">
          <div class="panel-head">
            <h2>Recent loads</h2>
            <a mat-button routerLink="/loads">View all</a>
          </div>
          @if (loads().length === 0) {
            <rl-empty-state icon="inventory_2" title="No loads posted" message="Posted loads will appear here." />
          } @else {
            <table class="mini">
              <thead><tr><th>Route</th><th>Cargo</th><th>Weight</th><th>Status</th></tr></thead>
              <tbody>
                @for (l of recentLoads(); track l.id) {
                  <tr>
                    <td>{{ l.originAddress ?? '—' }} → {{ l.destinationAddress ?? '—' }}</td>
                    <td>{{ cargo(l.cargoType) }}</td>
                    <td>{{ l.weightKg }} kg</td>
                    <td><rl-status-chip [label]="loadStatus(l.status)" /></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>

        <section class="rl-surface panel">
          <div class="panel-head"><h2>Quick actions</h2></div>
          <div class="quick">
            <a mat-stroked-button routerLink="/documents"><mat-icon>fact_check</mat-icon> Review documents</a>
            <a mat-stroked-button routerLink="/drivers"><mat-icon>badge</mat-icon> Manage drivers</a>
            <a mat-stroked-button routerLink="/loads"><mat-icon>inventory_2</mat-icon> Browse loads</a>
            <a mat-stroked-button routerLink="/trips"><mat-icon>local_shipping</mat-icon> Track trips</a>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [
    `
      .split { display: grid; grid-template-columns: 2fr 1fr; gap: var(--rl-gap); margin-top: 20px; }
      @media (max-width: 900px) { .split { grid-template-columns: 1fr; } }
      .panel { padding: 8px 20px 20px; }
      .panel-head { display: flex; align-items: center; justify-content: space-between; }
      .panel-head h2 { font-size: 1rem; font-weight: 600; }
      table.mini { width: 100%; border-collapse: collapse; }
      .mini th { text-align: left; font-size: 0.75rem; color: var(--mat-sys-on-surface-variant); text-transform: uppercase; letter-spacing: 0.04em; padding: 8px; }
      .mini td { padding: 10px 8px; border-top: 1px solid var(--mat-sys-outline-variant); font-size: 0.9rem; }
      .quick { display: flex; flex-direction: column; gap: 10px; }
      .quick a { justify-content: flex-start; }
      section.rl-stat-grid { margin-top: 8px; }
    `,
  ],
})
export class Dashboard {
  private readonly api = inject(ApiService);

  protected readonly loading = signal(true);
  protected readonly drivers = signal<DriverSummary[]>([]);
  protected readonly loads = signal<LoadView[]>([]);
  protected readonly pendingDocs = signal<DocumentView[]>([]);

  protected readonly activeDrivers = computed(() => this.drivers().filter((d) => d.status === 1).length);
  protected readonly recentLoads = computed(() => this.loads().slice(0, 5));

  constructor() {
    this.load();
  }

  protected cargo = (v: number) => label(CARGO_TYPE, v);
  protected loadStatus = (v: number) => label(LOAD_STATUS, v);
  protected driverStatus = (v: number) => label(DRIVER_STATUS, v);

  protected load(): void {
    this.loading.set(true);
    forkJoin({
      drivers: this.api.get<DriverSummary[]>('drivers'),
      loads: this.api.get<LoadView[]>('loads/available'),
      docs: this.api.get<DocumentView[]>('documents/pending'),
    }).subscribe({
      next: ({ drivers, loads, docs }) => {
        this.drivers.set(drivers);
        this.loads.set(loads);
        this.pendingDocs.set(docs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
