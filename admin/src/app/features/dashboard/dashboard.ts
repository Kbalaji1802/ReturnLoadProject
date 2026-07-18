import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

/** Landing overview with quick links into the operational areas. */
@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, MatButtonModule, RouterLink],
  template: `
    <h1>Dashboard</h1>
    <div class="tiles">
      <mat-card>
        <mat-card-header><mat-card-title>Drivers</mat-card-title></mat-card-header>
        <mat-card-content>Review drivers and verification status.</mat-card-content>
        <mat-card-actions><a mat-button routerLink="/drivers">Open</a></mat-card-actions>
      </mat-card>
      <mat-card>
        <mat-card-header><mat-card-title>Documents</mat-card-title></mat-card-header>
        <mat-card-content>Approve pending KYC / RC / licence documents.</mat-card-content>
        <mat-card-actions><a mat-button routerLink="/documents">Open</a></mat-card-actions>
      </mat-card>
      <mat-card>
        <mat-card-header><mat-card-title>Loads</mat-card-title></mat-card-header>
        <mat-card-content>Browse loads posted to the marketplace.</mat-card-content>
        <mat-card-actions><a mat-button routerLink="/loads">Open</a></mat-card-actions>
      </mat-card>
      <mat-card>
        <mat-card-header><mat-card-title>Trips</mat-card-title></mat-card-header>
        <mat-card-content>Track and complete trips.</mat-card-content>
        <mat-card-actions><a mat-button routerLink="/trips">Open</a></mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: [`h1 { margin: 0 0 1rem; } .tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 1rem; }`],
})
export class Dashboard {}
