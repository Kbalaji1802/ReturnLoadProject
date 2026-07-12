import { Component, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavItem {
  readonly label: string;
  readonly icon: string;
  readonly route: string;
}

/**
 * The operations-console frame: a persistent side navigation and toolbar around
 * a router outlet. Navigation is signal-driven so future role-based menus can
 * update reactively.
 */
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
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.scss',
})
export class MainLayout {
  protected readonly navItems = signal<readonly NavItem[]>([
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
  ]);
}
