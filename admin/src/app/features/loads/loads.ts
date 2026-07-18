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
import { LoadView } from '../../core/api/api.models';
import { CARGO_TYPE, LOAD_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

@Component({
  selector: 'app-loads',
  imports: [
    FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatProgressBarModule,
    PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Loads" subtitle="Loads posted to the marketplace">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search origin or destination</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Cargo</mat-label>
          <mat-select [ngModel]="cargoFilter()" (ngModelChange)="onCargo($event)">
            <mat-option [value]="-1">All</mat-option>
            @for (c of cargoTypes; track c) { <mat-option [value]="c">{{ cargo(c) }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!loading() && filtered().length === 0) {
          <rl-empty-state icon="inventory_2" title="No loads found" message="No posted loads match your filters." />
        } @else {
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="route">
              <th mat-header-cell *matHeaderCellDef>Route</th>
              <td mat-cell *matCellDef="let l">
                <div class="route"><mat-icon>trip_origin</mat-icon> {{ l.originAddress ?? '—' }}
                  <mat-icon class="arrow">arrow_forward</mat-icon> {{ l.destinationAddress ?? '—' }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="cargo">
              <th mat-header-cell *matHeaderCellDef>Cargo</th>
              <td mat-cell *matCellDef="let l">{{ cargo(l.cargoType) }}</td>
            </ng-container>
            <ng-container matColumnDef="weight">
              <th mat-header-cell *matHeaderCellDef>Weight</th>
              <td mat-cell *matCellDef="let l">{{ l.weightKg }} kg</td>
            </ng-container>
            <ng-container matColumnDef="price">
              <th mat-header-cell *matHeaderCellDef>Offered</th>
              <td mat-cell *matCellDef="let l">{{ l.offeredPriceInr ? '₹' + l.offeredPriceInr : '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let l"><rl-status-chip [label]="loadStatus(l.status)" /></td>
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
  styles: [
    `
      .search { min-width: 300px; }
      .route { display: flex; align-items: center; gap: 6px; }
      .route mat-icon { font-size: 16px; width: 16px; height: 16px; color: var(--mat-sys-primary); }
      .route .arrow { color: var(--mat-sys-on-surface-variant); }
    `,
  ],
})
export class Loads {
  private readonly api = inject(ApiService);
  protected readonly cols = ['route', 'cargo', 'weight', 'price', 'status'];
  protected readonly cargoTypes = [0, 1, 2, 3, 4, 5, 6];

  protected readonly loading = signal(true);
  protected readonly all = signal<LoadView[]>([]);
  protected readonly query = signal('');
  protected readonly cargoFilter = signal(-1);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const c = this.cargoFilter();
    return this.all().filter((l) => {
      const matchesCargo = c === -1 || l.cargoType === c;
      const hay = `${l.originAddress ?? ''} ${l.destinationAddress ?? ''}`.toLowerCase();
      return matchesCargo && (q === '' || hay.includes(q));
    });
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected cargo = (v: number) => label(CARGO_TYPE, v);
  protected loadStatus = (v: number) => label(LOAD_STATUS, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onCargo(v: number) { this.cargoFilter.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected load(): void {
    this.loading.set(true);
    this.api.get<LoadView[]>('loads/available').subscribe({
      next: (l) => { this.all.set(l); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
