import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';

import { ApiService, apiErrorMessage } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { VEHICLE_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';

interface VehicleView {
  readonly id: string;
  readonly carrierId: string;
  readonly registrationNumber: string;
  readonly type: number;
  readonly maxPayloadKg: number;
  readonly status: number;
}

const VEHICLE_TYPE: Record<number, string> = {
  0: 'Open body', 1: 'Closed container', 2: 'Flatbed', 3: 'Reefer', 4: 'Tanker',
  5: 'Tipper', 6: 'Light commercial', 7: 'Trailer', 99: 'Other',
};

@Component({
  selector: 'app-vehicles',
  imports: [
    FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Vehicles" subtitle="Approve pending vehicles so drivers can be matched">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search registration</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select [ngModel]="statusFilter()" (ngModelChange)="onStatus($event)">
            <mat-option [value]="-1">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ vehicleStatus(s) }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!busy() && filtered().length === 0) {
          <rl-empty-state icon="local_shipping" title="No vehicles" message="Nothing matches the current filter." />
        } @else {
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="reg">
              <th mat-header-cell *matHeaderCellDef>Registration</th>
              <td mat-cell *matCellDef="let v">{{ v.registrationNumber }}</td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let v">{{ vehicleType(v.type) }}</td>
            </ng-container>
            <ng-container matColumnDef="payload">
              <th mat-header-cell *matHeaderCellDef>Payload</th>
              <td mat-cell *matCellDef="let v">{{ v.maxPayloadKg }} kg</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let v"><rl-status-chip [label]="vehicleStatus(v.status)" /></td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef class="right">Actions</th>
              <td mat-cell *matCellDef="let v" class="right">
                @if (v.status === 0 && canManageFleet()) {
                  <button mat-flat-button (click)="approve(v); $event.stopPropagation()"><mat-icon>check</mat-icon> Approve</button>
                }
                <mat-icon class="chev">chevron_right</mat-icon>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="cols"></tr>
            <tr mat-row *matRowDef="let row; columns: cols" class="clickable" (click)="open(row.id)"></tr>
          </table>
          <mat-paginator [length]="filtered().length" [pageSize]="pageSize()"
            [pageSizeOptions]="[5, 10, 25]" (page)="onPage($event)" showFirstLastButtons />
        }
      </div>
    </div>
  `,
  styles: [`
    .search { min-width: 280px; } .right { text-align: right; white-space: nowrap; }
    .chev { vertical-align: middle; color: var(--mat-sys-on-surface-variant); margin-left: 4px; }
    tr.clickable { cursor: pointer; } tr.clickable:hover td { background: var(--mat-sys-surface-container-high); }
  `],
})
export class Vehicles {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly canManageFleet = this.auth.canManageFleet;
  protected readonly cols = ['reg', 'type', 'payload', 'status', 'actions'];
  protected readonly statuses = [0, 1, 2, 3];

  protected readonly busy = signal(true);
  protected readonly all = signal<VehicleView[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal(0); // default to the pending queue
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const s = this.statusFilter();
    return this.all().filter(
      (v) => (s === -1 || v.status === s) && (q === '' || v.registrationNumber.toLowerCase().includes(q)),
    );
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected vehicleStatus = (v: number) => label(VEHICLE_STATUS, v);
  protected vehicleType = (v: number) => label(VEHICLE_TYPE, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onStatus(v: number) { this.statusFilter.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected open(id: string) { this.router.navigate(['/vehicles', id]); }

  protected approve(v: VehicleView): void {
    this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Approve vehicle?',
        message: `Activate ${v.registrationNumber} for matching? Only approve once its RC, insurance and permit are verified.`,
        confirmLabel: 'Approve',
      },
    }).afterClosed().subscribe((ok) => {
      if (!ok) return;
      this.busy.set(true);
      this.api.post(`vehicles/${v.id}/activate?documentsValid=true`, {}).subscribe({
        next: () => { this.snack.open('Vehicle approved.', 'OK', { duration: 2500 }); this.load(); },
        error: (e) => { this.snack.open(apiErrorMessage(e, 'Approval failed.'), 'OK', { duration: 5000 }); this.busy.set(false); },
      });
    });
  }

  protected load(): void {
    this.busy.set(true);
    this.api.get<VehicleView[]>('vehicles').subscribe({
      next: (v) => { this.all.set(v); this.busy.set(false); },
      // Without this a failed request renders the "No vehicles" empty state, indistinguishable
      // from a genuinely empty fleet — which is exactly how a real outage went unnoticed.
      error: (e) => {
        this.busy.set(false);
        this.snack.open(apiErrorMessage(e, 'Could not load vehicles.'), 'OK', { duration: 5000 });
      },
    });
  }
}
