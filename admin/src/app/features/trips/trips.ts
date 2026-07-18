import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { ApiService } from '../../core/api/api.service';

interface TripView {
  id: string;
  status: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
}

/** Look up a trip by id and view/advance its status. */
@Component({
  selector: 'app-trips',
  imports: [FormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <mat-card>
      <mat-card-header><mat-card-title>Trips</mat-card-title></mat-card-header>
      <mat-card-content>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Trip id</mat-label>
          <input matInput [(ngModel)]="tripId" placeholder="paste a trip GUID" />
        </mat-form-field>
        <button mat-flat-button color="primary" (click)="load()" [disabled]="!tripId">View</button>
        @if (trip(); as t) {
          <div class="detail">
            <p><strong>Status:</strong> {{ t.status }}</p>
            <p><strong>Started:</strong> {{ t.startedAtUtc ?? '—' }}</p>
            <p><strong>Completed:</strong> {{ t.completedAtUtc ?? '—' }}</p>
            <button mat-stroked-button (click)="complete()">Mark completed</button>
          </div>
        }
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`.full { width: 100%; } .detail { margin-top: 1rem; } .error { color: #b3261e; }`],
})
export class Trips {
  private readonly api = inject(ApiService);
  protected tripId = '';
  protected readonly trip = signal<TripView | null>(null);
  protected readonly error = signal<string | null>(null);

  protected load(): void {
    this.error.set(null);
    this.api.get<TripView>(`trips/${this.tripId}`).subscribe({
      next: (t) => this.trip.set(t),
      error: () => this.error.set('Trip not found.'),
    });
  }

  protected complete(): void {
    this.api.post(`trips/${this.tripId}/status/Completed`, {}).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Could not complete the trip in its current state.'),
    });
  }
}
