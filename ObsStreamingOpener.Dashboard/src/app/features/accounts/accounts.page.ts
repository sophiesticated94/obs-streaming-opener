import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { ConnectedAccountDto } from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-accounts-page',
  imports: [DatePipe, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Google OAuth" title="Accounts">
      <button type="button" (click)="connectGoogle()">Connect Google/YouTube account</button>
    </oso-page-header>
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="panel">
      <h2>Connected accounts</h2>
      <div class="list">
        @for (account of accounts(); track account.accountId) {
          <div class="list-row account-row">
            <div>
              <strong>{{ account.displayName }}</strong>
              <small>{{ account.email || account.externalAccountId || 'No Google identity stored' }}</small>
            </div>
            <div>
              <span class="badge" [class.badge-ok]="state(account) === 'Connected'" [class.badge-warn]="state(account) !== 'Connected'">
                {{ state(account) }}
              </span>
              <small>{{ account.channelCount }} channel{{ account.channelCount === 1 ? '' : 's' }}</small>
            </div>
            <div>
              <small>Expires</small>
              <span>{{ account.accessTokenExpiresAt ? (account.accessTokenExpiresAt | date:'short') : 'No access token' }}</span>
            </div>
            <div class="actions">
              <button type="button" class="secondary" (click)="relogin(account)">Re-login</button>
              <button type="button" class="secondary" [disabled]="!account.hasRefreshToken" (click)="refresh(account)">Refresh token</button>
              <button type="button" class="secondary" [disabled]="!account.isLoggedIn" (click)="sync(account)">Sync channels</button>
              <button type="button" class="danger" [disabled]="account.disconnectedAt !== null" (click)="disconnect(account)">Disconnect</button>
            </div>
            <div class="token-details">
              <small>Scopes</small>
              <span>{{ account.scopes || 'No scopes stored' }}</span>
              @if (account.lastRefreshedAt) {
                <small>Last refreshed {{ account.lastRefreshedAt | date:'short' }}</small>
              }
            </div>
          </div>
        } @empty {
          <p>No Google/YouTube accounts connected.</p>
        }
      </div>
    </section>
  `
})
export class AccountsPage implements OnInit {
  readonly accounts = signal<ConnectedAccountDto[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    const params = new URLSearchParams(window.location.search);
    if (params.get('connected') === 'success') {
      this.messageTone.set('info');
      this.message.set('Google/YouTube account connected.');
      window.history.replaceState({}, '', '/dashboard/accounts');
    }

    this.load();
  }

  load(): void {
    this.api.getConnectedAccounts().subscribe({
      next: (accounts) => this.accounts.set(accounts),
      error: (error: Error) => this.fail(error)
    });
  }

  connectGoogle(): void {
    this.api.startYouTubeLogin().subscribe({
      next: (response) => window.location.assign(response.authorizationUrl),
      error: (error: Error) => this.fail(error)
    });
  }

  relogin(account: ConnectedAccountDto): void {
    this.api.reloginYouTube(account.accountId).subscribe({
      next: (response) => window.location.assign(response.authorizationUrl),
      error: (error: Error) => this.fail(error)
    });
  }

  refresh(account: ConnectedAccountDto): void {
    this.api.refreshYouTubeToken(account.accountId).subscribe({
      next: () => this.saved('Token refreshed.'),
      error: (error: Error) => this.fail(error)
    });
  }

  sync(account: ConnectedAccountDto): void {
    this.api.syncYouTubeAccount(account.accountId).subscribe({
      next: () => this.saved('Channels synced from YouTube.'),
      error: (error: Error) => this.fail(error)
    });
  }

  disconnect(account: ConnectedAccountDto): void {
    this.api.disconnectYouTube(account.accountId).subscribe({
      next: () => this.saved('Account disconnected. Historical data is preserved.'),
      error: (error: Error) => this.fail(error)
    });
  }

  state(account: ConnectedAccountDto): 'Connected' | 'Expired' | 'Needs re-login' | 'Disconnected' {
    if (account.disconnectedAt) {
      return 'Disconnected';
    }

    if (account.isLoggedIn && !account.isExpired) {
      return 'Connected';
    }

    if (account.isExpired && account.hasRefreshToken) {
      return 'Expired';
    }

    return 'Needs re-login';
  }

  private saved(message: string): void {
    this.messageTone.set('info');
    this.message.set(message);
    this.load();
  }

  private fail(error: Error): void {
    this.messageTone.set('error');
    this.message.set(error.message);
  }
}
