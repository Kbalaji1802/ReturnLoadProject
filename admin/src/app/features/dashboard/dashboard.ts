import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { ApiEnvelope, HealthStatus } from '../../core/api/api.models';
import { ApiService } from '../../core/api/api.service';

interface Kpi {
  readonly label: string;
  readonly value: number;
}

/**
 * Foundation dashboard. Demonstrates the console's building blocks — Angular
 * Signals for reactive state and a typed API call — without any business logic.
 * The live KPI wiring arrives with the observability task (T-050).
 */
@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly api = inject(ApiService);

  protected readonly kpis = signal<readonly Kpi[]>([
    { label: 'Active drivers', value: 0 },
    { label: 'Open loads', value: 0 },
    { label: 'Matches today', value: 0 },
  ]);

  /** A computed signal, to show derived reactive state end-to-end. */
  protected readonly metricsTracked = computed(() => this.kpis().length);

  protected readonly loading = signal(false);
  protected readonly health = signal<HealthStatus | null>(null);
  protected readonly error = signal<string | null>(null);

  protected checkApiHealth(): void {
    this.loading.set(true);
    this.health.set(null);
    this.error.set(null);

    this.api.get<ApiEnvelope<HealthStatus>>('health').subscribe({
      next: (response) => {
        this.health.set(response.data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('The API is unreachable. Start the backend and try again.');
        this.loading.set(false);
      },
    });
  }
}
