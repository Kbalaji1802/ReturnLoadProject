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
 * Minimum length of a rejection reason. A driver has to act on this text to fix their document,
 * and "no" or "bad" tells them nothing — the floor forces an actionable sentence. Mirrored by the
 * API so it holds regardless of client (RC-2 Part 1).
 */
export const REJECT_REASON_MIN_LENGTH = 20;

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
        <mat-hint [class.short]="tooShort()">
          {{ trimmed().length }} / {{ minLength }} characters minimum
        </mat-hint>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()">Cancel</button>
      <button mat-flat-button class="danger" [disabled]="tooShort()" (click)="submit()">Reject</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full { width: 100%; min-width: 360px; }
    .hint { color: var(--mat-sys-on-surface-variant); font-size: 0.85rem; margin: 0 0 12px; }
    .danger { --mdc-filled-button-container-color: var(--mat-sys-error); }
    .short { color: var(--mat-sys-error); }
  `],
})
export class RejectDialog {
  protected readonly ref = inject(MatDialogRef<RejectDialog, string>);
  protected readonly data = inject<RejectData>(MAT_DIALOG_DATA);
  protected readonly reason = signal('');
  protected readonly minLength = REJECT_REASON_MIN_LENGTH;

  protected readonly trimmed = () => this.reason().trim();
  protected readonly tooShort = () => this.trimmed().length < REJECT_REASON_MIN_LENGTH;

  protected submit(): void {
    if (!this.tooShort()) this.ref.close(this.trimmed());
  }
}
