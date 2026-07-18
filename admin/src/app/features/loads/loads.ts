import { Component, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

import { ApiService } from '../../core/api/api.service';
import { LoadView } from '../../core/api/api.models';

/** Available (posted) loads on the marketplace. */
@Component({
  selector: 'app-loads',
  imports: [MatCardModule],
  template: `
    <mat-card>
      <mat-card-header><mat-card-title>Available loads</mat-card-title></mat-card-header>
      <mat-card-content>
        @if (loads().length === 0) {
          <p>No loads posted yet.</p>
        } @else {
          <table class="grid">
            <thead><tr><th>From</th><th>To</th><th>Cargo</th><th>Weight (kg)</th><th>Price (₹)</th><th>Status</th></tr></thead>
            <tbody>
              @for (l of loads(); track l.id) {
                <tr>
                  <td>{{ l.originAddress ?? '—' }}</td>
                  <td>{{ l.destinationAddress ?? '—' }}</td>
                  <td>{{ l.cargoType }}</td>
                  <td>{{ l.weightKg }}</td>
                  <td>{{ l.offeredPriceInr ?? '—' }}</td>
                  <td>{{ l.status }}</td>
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
export class Loads {
  private readonly api = inject(ApiService);
  protected readonly loads = signal<LoadView[]>([]);

  constructor() {
    this.api.get<LoadView[]>('loads/available').subscribe((l) => this.loads.set(l));
  }
}
