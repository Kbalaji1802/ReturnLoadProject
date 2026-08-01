import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface RejectData {
  title: string;
}

/**
 * A rejection dialog that REQUIRES a reason (Trust & Safety §5) — closes with the trimmed reason,
 * never with an empty/canned string. Replaces the old window.prompt.
 */
@Component({
  selector: 'rl-reject-dialog',
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p class="hint">The reason is stored in the audit trail and shown to the driver so they can re-upload.</p>
      <mat-form-field appearance="outline" class="full">
        <mat-label>Reason for rejection</mat-label>
        <textarea matInput rows="3" [ngModel]="reason()" (ngModelChange)="reason.set($event)"
          placeholder="e.g. Document is blurry / expired / does not match the driver"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-flat-button class="danger" [disabled]="reason().trim().length === 0" (click)="submit()">Reject</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full { width: 100%; min-width: 360px; }
    .hint { color: var(--mat-sys-on-surface-variant); font-size: 0.85rem; margin: 0 0 12px; }
    .danger { --mdc-filled-button-container-color: var(--mat-sys-error); }
  `],
})
export class RejectDialog {
  protected readonly ref = inject(MatDialogRef<RejectDialog, string>);
  protected readonly data = inject<RejectData>(MAT_DIALOG_DATA);
  protected readonly reason = signal('');

  protected submit(): void {
    const r = this.reason().trim();
    if (r.length > 0) this.ref.close(r);
  }
}
