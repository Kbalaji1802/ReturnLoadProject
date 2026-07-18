import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { environment } from '../../../environments/environment';

/** Console settings + sign-out. */
@Component({
  selector: 'app-settings',
  imports: [MatCardModule, MatButtonModule],
  template: `
    <mat-card>
      <mat-card-header><mat-card-title>Settings</mat-card-title></mat-card-header>
      <mat-card-content>
        <p><strong>API base URL:</strong> {{ apiBaseUrl }}</p>
        <p>Maps, SMS/OTP and payment integrations are pending external service configuration.</p>
        <button mat-flat-button color="warn" (click)="logout()">Sign out</button>
      </mat-card-content>
    </mat-card>
  `,
})
export class Settings {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly apiBaseUrl = environment.apiBaseUrl;

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
