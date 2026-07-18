import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';

import { ApiService } from '../../core/api/api.service';
import { DocumentView } from '../../core/api/api.models';
import { DOCUMENT_OWNER, DOCUMENT_TYPE, VERIFICATION_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

@Component({
  selector: 'app-documents',
  imports: [
    FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Documents" subtitle="Operations review queue — approve or reject pending documents">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search type or owner</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!busy() && filtered().length === 0) {
          <rl-empty-state icon="task_alt" title="Inbox zero" message="No documents are awaiting review." />
        } @else {
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Document</th>
              <td mat-cell *matCellDef="let d">{{ docType(d.type) }}</td>
            </ng-container>
            <ng-container matColumnDef="owner">
              <th mat-header-cell *matHeaderCellDef>Owner</th>
              <td mat-cell *matCellDef="let d">{{ owner(d.ownerType) }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let d"><rl-status-chip [label]="verification(d.verificationStatus)" /></td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let d" class="right">
                <button mat-flat-button (click)="approve(d.id)"><mat-icon>check</mat-icon> Approve</button>
                <button mat-button (click)="reject(d.id)">Reject</button>
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
  styles: [`.search { min-width: 280px; } .right { text-align: right; white-space: nowrap; }`],
})
export class Documents {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);
  protected readonly cols = ['type', 'owner', 'status', 'actions'];

  protected readonly busy = signal(true);
  protected readonly all = signal<DocumentView[]>([]);
  protected readonly query = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (q === '') return this.all();
    return this.all().filter(
      (d) => this.docType(d.type).toLowerCase().includes(q) || this.owner(d.ownerType).toLowerCase().includes(q),
    );
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected docType = (v: number) => label(DOCUMENT_TYPE, v);
  protected owner = (v: number) => label(DOCUMENT_OWNER, v);
  protected verification = (v: number) => label(VERIFICATION_STATUS, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected approve(id: string): void {
    this.busy.set(true);
    this.api.post(`documents/${id}/approve`, {}).subscribe({
      next: () => { this.snack.open('Document approved.', 'OK', { duration: 2500 }); this.load(); },
      error: () => { this.snack.open('Approval failed.', 'OK', { duration: 3000 }); this.busy.set(false); },
    });
  }

  protected reject(id: string): void {
    this.busy.set(true);
    this.api.post(`documents/${id}/reject`, { reason: 'Rejected from console' }).subscribe({
      next: () => { this.snack.open('Document rejected.', 'OK', { duration: 2500 }); this.load(); },
      error: () => { this.snack.open('Rejection failed.', 'OK', { duration: 3000 }); this.busy.set(false); },
    });
  }

  protected load(): void {
    this.busy.set(true);
    this.api.get<DocumentView[]>('documents/pending').subscribe({
      next: (d) => { this.all.set(d); this.busy.set(false); },
      error: () => this.busy.set(false),
    });
  }
}
