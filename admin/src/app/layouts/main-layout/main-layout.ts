import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

interface NavItem {
  readonly label: string;
  readonly icon: string;
  readonly route: string;
}

/** The operations-console frame: persistent side navigation + toolbar around a router outlet. */
@Component({
  selector: 'app-main-layout',
  imports: [
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
  ],
  template: `
    <mat-toolbar color="primary">
      <span>ReturnLoad Admin</span>
      <span class="spacer"></span>
      <button mat-button (click)="logout()">Sign out</button>
    </mat-toolbar>
    <mat-sidenav-container class="frame">
      <mat-sidenav mode="side" opened class="nav">
        <mat-nav-list>
          @for (item of navItems(); track item.route) {
            <a mat-list-item [routerLink]="item.route" routerLinkActive="active">
              <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content class="content">
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [
    `
      .spacer { flex: 1 1 auto; }
      .frame { position: absolute; top: 64px; bottom: 0; left: 0; right: 0; }
      .nav { width: 240px; }
      .content { padding: 1.5rem; }
      .active { background: rgba(0, 0, 0, 0.06); }
    `,
  ],
})
export class MainLayout {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly navItems = signal<readonly NavItem[]>([
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Drivers', icon: 'person', route: '/drivers' },
    { label: 'Documents', icon: 'description', route: '/documents' },
    { label: 'Loads', icon: 'inventory', route: '/loads' },
    { label: 'Trips', icon: 'local_shipping', route: '/trips' },
    { label: 'Settings', icon: 'settings', route: '/settings' },
  ]);

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
