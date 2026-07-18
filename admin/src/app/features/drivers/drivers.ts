import { Component, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

import { ApiService } from '../../core/api/api.service';
import { DriverSummary } from '../../core/api/api.models';

/** Operations view of drivers and their verification status. */
@Component({
  selector: 'app-drivers',
  imports: [MatCardModule],
  template: `
    <mat-card>
      <mat-card-header><mat-card-title>Drivers</mat-card-title></mat-card-header>
      <mat-card-content>
        @if (drivers().length === 0) {
          <p>No drivers registered yet.</p>
        } @else {
          <table class="grid">
            <thead><tr><th>Licence</th><th>Status</th></tr></thead>
            <tbody>
              @for (d of drivers(); track d.id) {
                <tr><td>{{ d.licence }}</td><td>{{ d.status }}</td></tr>
              }
            </tbody>
          </table>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`.grid { width: 100%; border-collapse: collapse; } .grid th, .grid td { text-align: left; padding: 0.5rem; border-bottom: 1px solid rgba(0,0,0,0.08); }`],
})
export class Drivers {
  private readonly api = inject(ApiService);
  protected readonly drivers = signal<DriverSummary[]>([]);

  constructor() {
    this.api.get<DriverSummary[]>('drivers').subscribe((d) => this.drivers.set(d));
  }
}
