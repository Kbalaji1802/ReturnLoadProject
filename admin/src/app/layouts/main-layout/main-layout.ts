import { Component, inject, signal } from '@angular/core';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';

interface NavItem {
  readonly label: string;
  readonly icon: string;
  readonly route: string;
}

/** The operations-console frame: top app bar + a collapsible, responsive side navigation. */
@Component({
  selector: 'app-main-layout',
  imports: [
    MatToolbarModule, MatSidenavModule, MatListModule, MatIconModule, MatButtonModule,
    MatMenuModule, MatTooltipModule, MatDividerModule, RouterLink, RouterLinkActive, RouterOutlet,
  ],
  template: `
    <mat-toolbar class="topbar">
      <button mat-icon-button (click)="drawer.toggle()" aria-label="Toggle navigation">
        <mat-icon>menu</mat-icon>
      </button>
      <div class="brand">
        <div class="logo"><mat-icon>local_shipping</mat-icon></div>
        <div class="brand-text">
          <span class="name">ReturnLoad</span>
          <span class="tag">Logistics Console</span>
        </div>
      </div>
      <span class="rl-spacer"></span>
      <button mat-icon-button (click)="theme.toggle()" [matTooltip]="theme.mode() === 'dark' ? 'Light mode' : 'Dark mode'">
        <mat-icon>{{ theme.mode() === 'dark' ? 'light_mode' : 'dark_mode' }}</mat-icon>
      </button>
      <button mat-icon-button [matMenuTriggerFor]="userMenu" aria-label="Account">
        <mat-icon>account_circle</mat-icon>
      </button>
      <mat-menu #userMenu="matMenu">
        <div class="menu-head">
          <div class="menu-name">Operations</div>
          <div class="menu-mail">admin&#64;returnload.test</div>
        </div>
        <mat-divider />
        <button mat-menu-item routerLink="/settings"><mat-icon>settings</mat-icon> Settings</button>
        <button mat-menu-item (click)="logout()"><mat-icon>logout</mat-icon> Sign out</button>
      </mat-menu>
    </mat-toolbar>

    <mat-sidenav-container class="frame">
      <mat-sidenav #drawer class="nav" [mode]="isHandset() ? 'over' : 'side'" [opened]="!isHandset()">
        <mat-nav-list>
          @for (item of navItems; track item.route) {
            <a mat-list-item [routerLink]="item.route" routerLinkActive="active"
               (click)="isHandset() && drawer.close()">
              <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>
        <div class="nav-footer">v0.4 · MVP</div>
      </mat-sidenav>
      <mat-sidenav-content class="content">
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [
    `
      .topbar {
        position: sticky; top: 0; z-index: 5; gap: 4px;
        background: var(--mat-sys-surface); color: var(--mat-sys-on-surface);
        border-bottom: 1px solid var(--mat-sys-outline-variant); box-shadow: var(--rl-shadow-sm);
      }
      .brand { display: flex; align-items: center; gap: 10px; margin-left: 4px; }
      .logo {
        width: 36px; height: 36px; border-radius: 10px; display: grid; place-items: center;
        background: var(--mat-sys-primary); color: var(--mat-sys-on-primary);
      }
      .logo mat-icon { font-size: 22px; width: 22px; height: 22px; }
      .brand-text { display: flex; flex-direction: column; line-height: 1.1; }
      .brand-text .name { font-weight: 700; letter-spacing: -0.01em; }
      .brand-text .tag { font-size: 0.7rem; color: var(--mat-sys-on-surface-variant); }
      .menu-head { padding: 12px 16px; }
      .menu-name { font-weight: 600; }
      .menu-mail { font-size: 0.8rem; color: var(--mat-sys-on-surface-variant); }
      .frame { position: absolute; top: 64px; bottom: 0; left: 0; right: 0; background: var(--mat-sys-surface-container-low); }
      .nav { width: 248px; border-right: 1px solid var(--mat-sys-outline-variant); padding: 8px; }
      .nav a.active { background: color-mix(in srgb, var(--mat-sys-primary) 14%, transparent); border-radius: 10px; color: var(--mat-sys-primary); }
      .nav a.active mat-icon { color: var(--mat-sys-primary); }
      .nav-footer { padding: 12px 16px; font-size: 0.72rem; color: var(--mat-sys-on-surface-variant); }
      .content { display: block; }
    `,
  ],
})
export class MainLayout {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly theme = inject(ThemeService);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe(Breakpoints.Handset).pipe(map((r) => r.matches)),
    { initialValue: false },
  );

  protected readonly navItems: readonly NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Drivers', icon: 'badge', route: '/drivers' },
    { label: 'Documents', icon: 'verified', route: '/documents' },
    { label: 'Loads', icon: 'inventory_2', route: '/loads' },
    { label: 'Trips', icon: 'local_shipping', route: '/trips' },
    { label: 'Settings', icon: 'settings', route: '/settings' },
  ];

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
