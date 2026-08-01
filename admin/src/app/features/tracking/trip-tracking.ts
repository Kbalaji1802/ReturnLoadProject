import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';

import { ApiService } from '../../core/api/api.service';
import { TRIP_STATUS, label } from '../../core/api/enums';
import { PageHeader } from '../../shared/ui/page-header';
import { StatusChip } from '../../shared/ui/status-chip';
import { EmptyState } from '../../shared/ui/empty-state';

interface LiveView {
  tripId: string; status: number; hasLocation: boolean;
  latitude: number | null; longitude: number | null; lastUpdateUtc: string | null;
  distanceRemainingKm: number | null; etaMinutes: number | null;
}
interface Point { id: string; latitude: number; longitude: number; capturedAtUtc: string; speedKph: number | null; }
interface Summary { distanceKm: number; durationMinutes: number; avgSpeedKph: number; maxSpeedKph: number; idleMinutes: number; pointCount: number; }

@Component({
  selector: 'app-trip-tracking',
  imports: [DatePipe, DecimalPipe, RouterLink, MatTableModule, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, StatusChip, EmptyState],
  template: `
    <div class="rl-page">
      <rl-page-header title="Live tracking" [subtitle]="'Trip ' + tripId.slice(0, 8) + '…'">
        <a actions mat-stroked-button routerLink="/trips"><mat-icon>arrow_back</mat-icon> Trips</a>
        <button actions mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </rl-page-header>

      @if (busy()) { <mat-progress-bar mode="indeterminate" /> }

      @if (live(); as l) {
        <div class="rl-surface detail">
          <div class="row"><rl-status-chip [label]="tripStatus(l.status)" /></div>
          @if (l.hasLocation) {
            <div class="grid">
              <div><div class="muted">Current position</div><div class="mono">{{ l.latitude }}, {{ l.longitude }}</div></div>
              <div><div class="muted">Last update</div><div>{{ l.lastUpdateUtc | date: 'medium' }}</div></div>
              <div><div class="muted">Distance remaining</div><div>{{ l.distanceRemainingKm ?? '—' }} km</div></div>
              <div><div class="muted">ETA</div><div>{{ l.etaMinutes != null ? l.etaMinutes + ' min' : '—' }}</div></div>
            </div>
            <p class="note"><mat-icon>info</mat-icon> Map rendering uses the production maps adapter; live coordinates are shown here.</p>
          } @else {
            <rl-empty-state icon="gps_off" title="No location yet" message="The driver has not shared live location for this trip." />
          }
        </div>

        @if (summary(); as s) {
          <div class="rl-surface detail">
            <div class="grid">
              <div><div class="muted">Distance travelled</div><div>{{ s.distanceKm }} km</div></div>
              <div><div class="muted">Duration</div><div>{{ s.durationMinutes }} min</div></div>
              <div><div class="muted">Avg speed</div><div>{{ s.avgSpeedKph }} kph</div></div>
              <div><div class="muted">Max speed</div><div>{{ s.maxSpeedKph }} kph</div></div>
              <div><div class="muted">Idle</div><div>{{ s.idleMinutes }} min</div></div>
              <div><div class="muted">Points</div><div>{{ s.pointCount }}</div></div>
            </div>
          </div>
        }

        <div class="rl-surface">
          @if (points().length === 0) {
            <rl-empty-state icon="timeline" title="No breadcrumb" message="Tracking points appear as the driver shares location." />
          } @else {
            <table mat-table [dataSource]="points()" class="rl-table">
              <ng-container matColumnDef="time">
                <th mat-header-cell *matHeaderCellDef>Time</th>
                <td mat-cell *matCellDef="let p">{{ p.capturedAtUtc | date: 'medium' }}</td>
              </ng-container>
              <ng-container matColumnDef="coord">
                <th mat-header-cell *matHeaderCellDef>Coordinates</th>
                <td mat-cell *matCellDef="let p" class="mono">{{ p.latitude }}, {{ p.longitude }}</td>
              </ng-container>
              <ng-container matColumnDef="speed">
                <th mat-header-cell *matHeaderCellDef>Speed</th>
                <td mat-cell *matCellDef="let p">{{ p.speedKph != null ? (p.speedKph | number: '1.0-0') + ' kph' : '—' }}</td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="cols"></tr>
              <tr mat-row *matRowDef="let row; columns: cols"></tr>
            </table>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .detail { padding: 20px; margin-bottom: 16px; }
    .row { margin-bottom: 12px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 16px; }
    .muted { color: var(--mat-sys-on-surface-variant); font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; }
    .mono { font-family: ui-monospace, monospace; font-size: 0.85rem; }
    .note { display: flex; align-items: center; gap: 8px; color: var(--mat-sys-on-surface-variant); font-size: 0.85rem; margin: 16px 0 0; }
    .note mat-icon { font-size: 18px; width: 18px; height: 18px; }
  `],
})
export class TripTracking {
  private readonly api = inject(ApiService);
  protected readonly tripId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly cols = ['time', 'coord', 'speed'];

  protected readonly busy = signal(true);
  protected readonly live = signal<LiveView | null>(null);
  protected readonly summary = signal<Summary | null>(null);
  protected readonly points = signal<Point[]>([]);

  protected tripStatus = (v: number) => label(TRIP_STATUS, v);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.busy.set(true);
    this.api.get<LiveView>(`trips/${this.tripId}/tracking/live`).subscribe({
      next: (l) => { this.live.set(l); this.busy.set(false); },
      error: () => this.busy.set(false),
    });
    this.api.get<Summary>(`trips/${this.tripId}/tracking/summary`).subscribe({ next: (s) => this.summary.set(s) });
    this.api.get<Point[]>(`trips/${this.tripId}/tracking`).subscribe({ next: (p) => this.points.set(p) });
  }
}
