import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { ApiService } from '../../core/api/api.service';
import { DocumentView } from '../../core/api/api.models';

/** Operations review queue: approve pending documents (verifying the driver on licence approval). */
@Component({
  selector: 'app-documents',
  imports: [MatCardModule, MatButtonModule],
  template: `
    <mat-card>
      <mat-card-header><mat-card-title>Pending documents</mat-card-title></mat-card-header>
      <mat-card-content>
        @if (docs().length === 0) {
          <p>No documents awaiting review.</p>
        } @else {
          <table class="grid">
            <thead><tr><th>Type</th><th>Owner</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (d of docs(); track d.id) {
                <tr>
                  <td>{{ d.type }}</td>
                  <td>{{ d.ownerType }}</td>
                  <td>{{ d.verificationStatus }}</td>
                  <td>
                    <button mat-flat-button color="primary" (click)="approve(d.id)">Approve</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`.grid { width: 100%; border-collapse: collapse; } .grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid rgba(0,0,0,0.08); }`],
})
export class Documents {
  private readonly api = inject(ApiService);
  protected readonly docs = signal<DocumentView[]>([]);

  constructor() {
    this.load();
  }

  protected approve(id: string): void {
    this.api.post(`documents/${id}/approve`, {}).subscribe(() => this.load());
  }

  private load(): void {
    this.api.get<DocumentView[]>('documents/pending').subscribe((d) => this.docs.set(d));
  }
}
