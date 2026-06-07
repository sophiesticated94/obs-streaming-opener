import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="app-shell">
      <aside class="sidebar">
        <a class="brand" routerLink="/overview">
          <span class="brand-mark">OSO</span>
          <span>
            <strong>OBS Streaming Opener</strong>
            <small>Dashboard</small>
          </span>
        </a>
        <nav>
          <a routerLink="/overview" routerLinkActive="active">Overview</a>
          <a routerLink="/accounts" routerLinkActive="active">Accounts</a>
          <a routerLink="/channels" routerLinkActive="active">Channels</a>
          <a routerLink="/content" routerLinkActive="active">Content</a>
          <a routerLink="/audience" routerLinkActive="active">Audience</a>
          <a routerLink="/activity" routerLinkActive="active">Activity</a>
          <a routerLink="/alerts" routerLinkActive="active">Alerts</a>
          <a routerLink="/revenue" routerLinkActive="active">Revenue</a>
          <a routerLink="/widgets" routerLinkActive="active">Widgets</a>
          <a routerLink="/polling" routerLinkActive="active">Polling</a>
        </nav>
      </aside>
      <main class="content">
        <router-outlet />
      </main>
    </div>
  `
})
export class AppComponent {}
