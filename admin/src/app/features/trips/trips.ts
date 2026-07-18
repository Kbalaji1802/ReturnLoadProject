import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ApiService } from '../../core/api/api.service';
import { TRIP_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

interface TripView {
  id: string;
  carrierId: string;
  vehicleId: string;
  driverProfileId: string;
  status: number;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
}

@Component({
  selector: 'app-trips',
  imports: [
    FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule,
    MatProgressBarModule, PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Trips" subtitle="Look up a trip and advance its lifecycle" />

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>tag</mat-icon>
          <mat-label>Trip id</mat-label>
          <input matInput [(ngModel)]="tripId" placeholder="paste a trip GUID" />
        </mat-form-field>
        <button mat-flat-button (click)="load()" [disabled]="!tripId || loading()">
          <mat-icon>search</mat-icon> View
        </button>
      </div>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }

      @if (trip(); as t) {
        <div class="rl-surface detail">
          <div class="row">
            <div>
              <div class="muted">Trip</div>
              <div class="mono">{{ t.id }}</div>
            </div>
            <rl-status-chip [label]="tripStatus(t.status)" />
          </div>
          <div class="grid">
            <div><div class="muted">Vehicle</div><div class="mono">{{ t.vehicleId }}</div></div>
            <div><div class="muted">Driver</div><div class="mono">{{ t.driverProfileId }}</div></div>
            <div><div class="muted">Started</div><div>{{ t.startedAtUtc ?? '—' }}</div></div>
            <div><div class="muted">Completed</div><div>{{ t.completedAtUtc ?? '—' }}</div></div>
          </div>
          <div class="actions">
            <button mat-stroked-button (click)="advance('Assigned')">Assign</button>
            <button mat-stroked-button (click)="advance('Started')">Start</button>
            <button mat-stroked-button (click)="advance('InTransit')">In transit</button>
            <button mat-flat-button (click)="advance('Completed')"><mat-icon>flag</mat-icon> Complete</button>
          </div>
        </div>
      } @else if (!loading()) {
        <div class="rl-surface">
          <rl-empty-state icon="local_shipping" title="No trip loaded"
            message="Enter a trip id above to view status and tracking." />
        </div>
      }
    </div>
  `,
  styles: [
    `
      .search { min-width: 360px; }
      .detail { padding: 24px; }
      .row { display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px; }
      .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 20px; }
      .muted { color: var(--mat-sys-on-surface-variant); font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; }
      .mono { font-family: ui-monospace, monospace; font-size: 0.85rem; }
      .actions { display: flex; gap: 10px; flex-wrap: wrap; }
    `,
  ],
})
export class Trips {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);

  protected tripId = '';
  protected readonly loading = signal(false);
  protected readonly trip = signal<TripView | null>(null);

  protected tripStatus = (v: number) => label(TRIP_STATUS, v);

  protected load(): void {
    this.loading.set(true);
    this.api.get<TripView>(`trips/${this.tripId.trim()}`).subscribe({
      next: (t) => { this.trip.set(t); this.loading.set(false); },
      error: () => { this.trip.set(null); this.loading.set(false); this.snack.open('Trip not found.', 'OK', { duration: 3000 }); },
    });
  }

  protected advance(target: string): void {
    this.loading.set(true);
    this.api.post(`trips/${this.tripId.trim()}/status/${target}`, {}).subscribe({
      next: () => { this.snack.open(`Trip ${target}.`, 'OK', { duration: 2500 }); this.load(); },
      error: () => { this.loading.set(false); this.snack.open('Transition not allowed in the current state.', 'OK', { duration: 3500 }); },
    });
  }
}
