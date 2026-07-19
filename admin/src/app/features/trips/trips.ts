import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { ApiService } from '../../core/api/api.service';
import { TRIP_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

interface TripView {
  readonly id: string;
  readonly carrierId: string;
  readonly vehicleId: string;
  readonly driverProfileId: string;
  readonly status: number;
  readonly startedAtUtc: string | null;
  readonly completedAtUtc: string | null;
}

@Component({
  selector: 'app-trips',
  imports: [
    DatePipe, RouterLink, FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Trips" subtitle="Every trip and its lifecycle status">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search trip / driver / vehicle id</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select [ngModel]="statusFilter()" (ngModelChange)="onStatus($event)">
            <mat-option [value]="-1">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ tripStatus(s) }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!busy() && filtered().length === 0) {
          <rl-empty-state icon="local_shipping" title="No trips" message="Trips appear once load owners approve drivers." />
        } @else {
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="id">
              <th mat-header-cell *matHeaderCellDef>Trip</th>
              <td mat-cell *matCellDef="let t" class="mono">{{ t.id.slice(0, 8) }}…</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let t"><rl-status-chip [label]="tripStatus(t.status)" /></td>
            </ng-container>
            <ng-container matColumnDef="driver">
              <th mat-header-cell *matHeaderCellDef>Driver</th>
              <td mat-cell *matCellDef="let t" class="mono">{{ t.driverProfileId.slice(0, 8) }}…</td>
            </ng-container>
            <ng-container matColumnDef="vehicle">
              <th mat-header-cell *matHeaderCellDef>Vehicle</th>
              <td mat-cell *matCellDef="let t" class="mono">{{ t.vehicleId.slice(0, 8) }}…</td>
            </ng-container>
            <ng-container matColumnDef="started">
              <th mat-header-cell *matHeaderCellDef>Started</th>
              <td mat-cell *matCellDef="let t">{{ t.startedAtUtc ? (t.startedAtUtc | date: 'short') : '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let t" class="right">
                <a mat-stroked-button [routerLink]="['/tracking', t.id]"><mat-icon>my_location</mat-icon> Track</a>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="cols"></tr>
            <tr mat-row *matRowDef="let row; columns: cols"></tr>
          </table>
          <mat-paginator [length]="filtered().length" [pageSize]="pageSize()"
            [pageSizeOptions]="[5, 10, 25]" (page)="onPage($event)" showFirstLastButtons />
        }
      </div>
    </div>
  `,
  styles: [`.search { min-width: 300px; } .mono { font-family: ui-monospace, monospace; font-size: 0.8rem; color: var(--mat-sys-on-surface-variant); } .right { text-align: right; white-space: nowrap; }`],
})
export class Trips {
  private readonly api = inject(ApiService);
  protected readonly cols = ['id', 'status', 'driver', 'vehicle', 'started', 'actions'];
  protected readonly statuses = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

  protected readonly busy = signal(true);
  protected readonly all = signal<TripView[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal(-1);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const s = this.statusFilter();
    return this.all().filter(
      (t) => (s === -1 || t.status === s) &&
        (q === '' || t.id.includes(q) || t.driverProfileId.includes(q) || t.vehicleId.includes(q)),
    );
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected tripStatus = (v: number) => label(TRIP_STATUS, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onStatus(v: number) { this.statusFilter.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected load(): void {
    this.busy.set(true);
    this.api.get<TripView[]>('trips').subscribe({
      next: (t) => { this.all.set(t); this.busy.set(false); },
      error: () => this.busy.set(false),
    });
  }
}
