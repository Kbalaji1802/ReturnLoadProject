import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ApiService, apiErrorMessage } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { PendingDocumentView } from '../../core/api/api.models';
import { DOCUMENT_TYPE, VERIFICATION_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { RejectDialog } from '../../shared/ui/reject-dialog';
import { DocPreviewDialog, DocPreviewItem } from '../../shared/ui/doc-preview-dialog';

type SortKey = 'driverName' | 'company' | 'vehicle' | 'type' | 'uploadedAtUtc' | 'expiresOn' | 'status';

@Component({
  selector: 'app-documents',
  imports: [
    DatePipe, FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule, PageHeader, StatusChip, EmptyState,
  ],
  template: `
    <div class="rl-page">
      <rl-page-header title="Documents" subtitle="Operations review queue — everything you need to decide in one row">
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      <div class="rl-toolbar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search driver, company, vehicle or type</mat-label>
          <input matInput [ngModel]="query()" (ngModelChange)="onQuery($event)" />
        </mat-form-field>
      </div>

      <div class="rl-surface">
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        @if (!busy() && filtered().length === 0) {
          <rl-empty-state icon="task_alt" title="Inbox zero" message="No documents are awaiting review." />
        } @else {
          <div class="scroll-x">
          <table mat-table [dataSource]="paged()" class="rl-table">
            <ng-container matColumnDef="driver">
              <th mat-header-cell *matHeaderCellDef (click)="sort('driverName')" class="sortable">Driver {{ arrow('driverName') }}</th>
              <td mat-cell *matCellDef="let d">
                <div class="who">
                  @if (d.driverPhotoUrl) { <img class="avatar" [src]="d.driverPhotoUrl" alt="" /> }
                  @else { <span class="avatar ph"><mat-icon>person</mat-icon></span> }
                  <span>{{ d.driverName ?? '—' }}</span>
                </div>
              </td>
            </ng-container>
            <ng-container matColumnDef="company">
              <th mat-header-cell *matHeaderCellDef (click)="sort('company')" class="sortable">Company {{ arrow('company') }}</th>
              <td mat-cell *matCellDef="let d">{{ d.company ?? '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="vehicle">
              <th mat-header-cell *matHeaderCellDef (click)="sort('vehicle')" class="sortable">Vehicle {{ arrow('vehicle') }}</th>
              <td mat-cell *matCellDef="let d">{{ d.vehicleRegistration ?? '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef (click)="sort('type')" class="sortable">Document {{ arrow('type') }}</th>
              <td mat-cell *matCellDef="let d">{{ docType(d.type) }}</td>
            </ng-container>
            <ng-container matColumnDef="number">
              <th mat-header-cell *matHeaderCellDef>Doc no.</th>
              <td mat-cell *matCellDef="let d">{{ d.documentNumber ?? '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="uploaded">
              <th mat-header-cell *matHeaderCellDef (click)="sort('uploadedAtUtc')" class="sortable">Uploaded {{ arrow('uploadedAtUtc') }}</th>
              <td mat-cell *matCellDef="let d">{{ d.uploadedAtUtc | date:'d MMM y' }}</td>
            </ng-container>
            <ng-container matColumnDef="expiry">
              <th mat-header-cell *matHeaderCellDef (click)="sort('expiresOn')" class="sortable">Expiry {{ arrow('expiresOn') }}</th>
              <td mat-cell *matCellDef="let d">{{ d.expiresOn ? (d.expiresOn | date:'d MMM y') : '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef (click)="sort('status')" class="sortable">Status {{ arrow('status') }}</th>
              <td mat-cell *matCellDef="let d"><rl-status-chip [label]="verification(d.verificationStatus)" /></td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef class="right">Actions</th>
              <td mat-cell *matCellDef="let d" class="right nowrap">
                <button mat-icon-button matTooltip="View" (click)="view(d)"><mat-icon>visibility</mat-icon></button>
                <button mat-icon-button matTooltip="Download" (click)="download(d)"><mat-icon>download</mat-icon></button>
                @if (canVerify()) {
                  <button mat-icon-button matTooltip="Approve" (click)="approve(d)"><mat-icon color="primary">check_circle</mat-icon></button>
                  <button mat-icon-button matTooltip="Reject" (click)="reject(d)"><mat-icon color="warn">cancel</mat-icon></button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="cols"></tr>
            <tr mat-row *matRowDef="let row; columns: cols"></tr>
          </table>
          </div>
          <mat-paginator [length]="filtered().length" [pageSize]="pageSize()"
            [pageSizeOptions]="[5, 10, 25]" (page)="onPage($event)" showFirstLastButtons />
        }
      </div>
    </div>
  `,
  styles: [`
    .search { min-width: 320px; }
    .right { text-align: right; } .nowrap { white-space: nowrap; }
    .scroll-x { overflow-x: auto; }
    .sortable { cursor: pointer; user-select: none; }
    .who { display: flex; align-items: center; gap: 10px; }
    .avatar { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; }
    .avatar.ph { display: inline-flex; align-items: center; justify-content: center;
      background: var(--mat-sys-surface-container-high); color: var(--mat-sys-on-surface-variant); }
    .avatar.ph mat-icon { font-size: 20px; height: 20px; width: 20px; }
  `],
})
export class Documents {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  protected readonly cols = ['driver', 'company', 'vehicle', 'type', 'number', 'uploaded', 'expiry', 'status', 'actions'];
  protected readonly canVerify = this.auth.canVerifyDocuments;

  protected readonly busy = signal(true);
  protected readonly all = signal<PendingDocumentView[]>([]);
  protected readonly query = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);
  protected readonly sortKey = signal<SortKey>('uploadedAtUtc');
  protected readonly sortAsc = signal(true);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const rows = q === '' ? this.all() : this.all().filter((d) =>
      [d.driverName, d.company, d.vehicleRegistration, this.docType(d.type)]
        .some((f) => (f ?? '').toLowerCase().includes(q)));
    return this.sortRows(rows);
  });

  protected readonly paged = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.load();
  }

  protected docType = (v: number) => label(DOCUMENT_TYPE, v);
  protected verification = (v: number) => label(VERIFICATION_STATUS, v);
  protected onQuery(v: string) { this.query.set(v); this.pageIndex.set(0); }
  protected onPage(e: PageEvent) { this.pageIndex.set(e.pageIndex); this.pageSize.set(e.pageSize); }

  protected sort(key: SortKey): void {
    if (this.sortKey() === key) this.sortAsc.set(!this.sortAsc());
    else { this.sortKey.set(key); this.sortAsc.set(true); }
  }
  protected arrow(key: SortKey): string {
    return this.sortKey() === key ? (this.sortAsc() ? '▲' : '▼') : '';
  }

  private sortRows(rows: PendingDocumentView[]): PendingDocumentView[] {
    const k = this.sortKey();
    const dir = this.sortAsc() ? 1 : -1;
    const val = (d: PendingDocumentView): string | number => {
      switch (k) {
        case 'driverName': return (d.driverName ?? '').toLowerCase();
        case 'company': return (d.company ?? '').toLowerCase();
        case 'vehicle': return (d.vehicleRegistration ?? '').toLowerCase();
        case 'type': return this.docType(d.type).toLowerCase();
        case 'uploadedAtUtc': return d.uploadedAtUtc ?? '';
        case 'expiresOn': return d.expiresOn ?? '';
        case 'status': return d.verificationStatus;
      }
    };
    return [...rows].sort((a, b) => (val(a) < val(b) ? -1 : val(a) > val(b) ? 1 : 0) * dir);
  }

  protected view(d: PendingDocumentView): void {
    // Hand the dialog the whole filtered queue so a reviewer can step through the backlog with
    // the arrows instead of closing and reopening for every row. Sorted/filtered order is what
    // they see in the table, so it is the order they expect to walk.
    const queue = this.filtered().map((row) => this.toPreviewItem(row));
    const index = Math.max(0, this.filtered().findIndex((row) => row.id === d.id));

    this.dialog.open(DocPreviewDialog, {
      data: { ...this.toPreviewItem(d), queue, index },
      width: '900px', maxWidth: '95vw',
    });
  }

  private toPreviewItem(d: PendingDocumentView): DocPreviewItem {
    return {
      documentId: d.id,
      title: this.docType(d.type) + (d.driverName ? ` · ${d.driverName}` : ''),
    };
  }

  protected download(d: PendingDocumentView): void {
    this.api.getBlob(`documents/${d.id}/file`).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${this.docType(d.type)}_${d.driverName ?? d.id}`.replace(/\s+/g, '_');
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (e) => this.snack.open(apiErrorMessage(e, 'Could not download the file.'), 'OK', { duration: 5000 }),
    });
  }

  protected approve(d: PendingDocumentView): void {
    this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Approve document?',
        message: `Approve the ${this.docType(d.type)} for ${d.driverName ?? 'this owner'}? Approving a driving licence also verifies the driver.`,
        confirmLabel: 'Approve',
      },
    }).afterClosed().subscribe((ok) => {
      if (!ok) return;
      this.busy.set(true);
      this.api.post(`documents/${d.id}/approve`, {}).subscribe({
        next: () => { this.snack.open('Document approved.', 'OK', { duration: 2500 }); this.load(); },
        error: (e) => { this.snack.open(apiErrorMessage(e, 'Approval failed.'), 'OK', { duration: 5000 }); this.busy.set(false); },
      });
    });
  }

  protected reject(d: PendingDocumentView): void {
    this.dialog.open(RejectDialog, { data: { title: `Reject ${this.docType(d.type)}?` } })
      .afterClosed().subscribe((reason: string | undefined) => {
        if (!reason) return; // cancelled or empty — never reject without a reason
        this.busy.set(true);
        this.api.post(`documents/${d.id}/reject`, { reason }).subscribe({
          next: () => { this.snack.open('Document rejected — the driver was notified.', 'OK', { duration: 2500 }); this.load(); },
          error: (e) => { this.snack.open(apiErrorMessage(e, 'Rejection failed.'), 'OK', { duration: 5000 }); this.busy.set(false); },
        });
      });
  }

  protected load(): void {
    this.busy.set(true);
    this.api.get<PendingDocumentView[]>('documents/pending').subscribe({
      next: (d) => { this.all.set(d); this.busy.set(false); },
      // A failed load must not look like an empty queue — "Inbox zero" and "the request died"
      // are the same screen otherwise.
      error: (e) => {
        this.busy.set(false);
        this.snack.open(apiErrorMessage(e, 'Could not load the review queue.'), 'OK', { duration: 5000 });
      },
    });
  }
}
