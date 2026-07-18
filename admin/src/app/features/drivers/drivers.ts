import { Component, computed, inject, signal } from '@angular/core';
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
import { DriverSummary } from '../../core/api/api.models';
import { DRIVER_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

@Component({
  selector: 'app-drivers',
  imports: [
    FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatProgressBarModule,
    PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Drivers" subtitle="Registered drivers and verification status">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search licence or id</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select [ngModel]="statusFilter()" (ngModelChange)="onStatus($event)">
            <mat-option [value]="-1">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ driverStatus(s) }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!loading() && filtered().length === 0) {
          <rl-empty-state icon="badge" title="No drivers found" message="Try clearing the search or filter." />
        } @else {
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="licence">
              <th mat-header-cell *matHeaderCellDef>Licence</th>
              <td mat-cell *matCellDef="let d">{{ d.licence }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let d"><rl-status-chip [label]="driverStatus(d.status)" /></td>
            </ng-container>
            <ng-container matColumnDef="id">
              <th mat-header-cell *matHeaderCellDef>Driver id</th>
              <td mat-cell *matCellDef="let d" class="mono">{{ d.id }}</td>
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
  styles: [`.search { min-width: 280px; } .mono { font-family: ui-monospace, monospace; font-size: 0.8rem; color: var(--mat-sys-on-surface-variant); }`],
})
export class Drivers {
  private readonly api = inject(ApiService);
  protected readonly cols = ['licence', 'status', 'id'];
  protected readonly statuses = [0, 1, 2, 3];

  protected readonly loading = signal(true);
  protected readonly all = signal<DriverSummary[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal(-1);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const s = this.statusFilter();
    return this.all().filter(
      (d) => (s === -1 || d.status === s) && (q === '' || d.licence.toLowerCase().includes(q) || d.id.includes(q)),
    );
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected driverStatus = (v: number) => label(DRIVER_STATUS, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onStatus(v: number) { this.statusFilter.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected load(): void {
    this.loading.set(true);
    this.api.get<DriverSummary[]>('drivers').subscribe({
      next: (d) => { this.all.set(d); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
