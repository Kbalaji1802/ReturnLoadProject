import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
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
import { BOOKING_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

interface BookingRequestView {
  readonly id: string;
  readonly loadId: string;
  readonly vehicleId: string;
  readonly status: number;
  readonly createdAtUtc: string;
  readonly driverName: string | null;
  readonly vehicleRegistration: string | null;
}

@Component({
  selector: 'app-bookings',
  imports: [
    DatePipe, FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Booking requests" subtitle="Every driver request across the marketplace">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search driver or vehicle</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select [ngModel]="statusFilter()" (ngModelChange)="onStatus($event)">
            <mat-option [value]="-1">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ bookingStatus(s) }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!busy() && filtered().length === 0) {
          <rl-empty-state icon="how_to_reg" title="No booking requests" message="Requests appear as drivers apply for loads." />
        } @else {
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="driver">
              <th mat-header-cell *matHeaderCellDef>Driver</th>
              <td mat-cell *matCellDef="let b">{{ b.driverName || '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="vehicle">
              <th mat-header-cell *matHeaderCellDef>Vehicle</th>
              <td mat-cell *matCellDef="let b">{{ b.vehicleRegistration || '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="requested">
              <th mat-header-cell *matHeaderCellDef>Requested</th>
              <td mat-cell *matCellDef="let b">{{ b.createdAtUtc | date: 'short' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let b"><rl-status-chip [label]="bookingStatus(b.status)" /></td>
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
  styles: [`.search { min-width: 280px; }`],
})
export class Bookings {
  private readonly api = inject(ApiService);
  protected readonly cols = ['driver', 'vehicle', 'requested', 'status'];
  protected readonly statuses = [0, 1, 2, 3];

  protected readonly busy = signal(true);
  protected readonly all = signal<BookingRequestView[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal(-1);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const s = this.statusFilter();
    return this.all().filter(
      (b) => (s === -1 || b.status === s) &&
        (q === '' || (b.driverName ?? '').toLowerCase().includes(q) || (b.vehicleRegistration ?? '').toLowerCase().includes(q)),
    );
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected bookingStatus = (v: number) => label(BOOKING_STATUS, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onStatus(v: number) { this.statusFilter.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected load(): void {
    this.busy.set(true);
    this.api.get<BookingRequestView[]>('bookings').subscribe({
      next: (b) => { this.all.set(b); this.busy.set(false); },
      error: () => this.busy.set(false),
    });
  }
}
