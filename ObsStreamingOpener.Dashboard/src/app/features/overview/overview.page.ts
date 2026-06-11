import { Component, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { ConnectedAccountDto, CurrentStatsDto, MonitoredChannelDto, RecentEventDto } from '../../core/api/api.models';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-overview-page',
  imports: [PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Configuration" title="Overview">
      <button type="button" (click)="load()">Refresh</button>
    </oso-page-header>
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="grid two">
      <article class="panel">
        <h2>Current Stats</h2>
        @if (stats(); as value) {
          <dl class="stats-list">
            <div><dt>Viewers</dt><dd>{{ value.concurrentViewers }}</dd></div>
            <div><dt>Likes</dt><dd>{{ value.likes }}</dd></div>
            <div><dt>Tips</dt><dd>{{ value.tipTotal }}</dd></div>
            <div><dt>Audience</dt><dd>{{ value.audienceMemberCount }}</dd></div>
          </dl>
        }
      </article>

      <article class="panel">
        <h2>Connected Accounts</h2>
        <div class="list">
          @for (account of accounts(); track account.accountId) {
            <div class="list-row">
              <span>{{ account.displayName }}</span>
              <small>{{ account.email || account.externalAccountId || account.provider }}</small>
              <span>{{ account.isLoggedIn && !account.isExpired ? 'Ready' : account.hasRefreshToken ? 'Refreshable' : 'Needs re-login' }}</span>
            </div>
          } @empty {
            <p>No accounts connected.</p>
          }
        </div>
      </article>
    </section>

    <section class="panel">
      <h2>Accounts and Channels</h2>
      <div class="list">
        @for (account of accounts(); track account.accountId) {
          <article class="subpanel">
            <div class="section-header">
              <div>
                <h3>{{ account.displayName }}</h3>
                <p>{{ account.email || account.provider }} / {{ account.isLoggedIn && !account.isExpired ? 'connected' : 'needs attention' }}</p>
              </div>
              <span class="badge" [class.badge-ok]="account.isLoggedIn && !account.isExpired" [class.badge-warn]="!account.isLoggedIn || account.isExpired">
                {{ account.channelCount }} channels
              </span>
            </div>
            <div class="list compact">
              @for (channel of channelsForAccount(account.accountId); track channel.id) {
                <div class="list-row">
                  <span>{{ channel.displayName }}</span>
                  <small>{{ channel.provider }} / {{ channel.externalChannelId }}</small>
                  <small>Audience {{ channelMetric(channel.id, 'audienceMemberCount') }}</small>
                  <small>Views {{ channelMetric(channel.id, 'concurrentViewers') }}</small>
                </div>
              } @empty {
                <p>No channels discovered yet.</p>
              }
            </div>
          </article>
        } @empty {
          <p>No accounts connected.</p>
        }
      </div>
    </section>

    <section class="panel">
      <h2>Recent Events</h2>
      <div class="list">
        @for (event of events(); track event.id) {
          <div class="list-row">
            <span>{{ event.title ?? event.actorName ?? 'Event' }}</span>
            <small>{{ event.message ?? event.occurredAt }}</small>
          </div>
        } @empty {
          <p>No events recorded.</p>
        }
      </div>
    </section>
  `
})
export class OverviewPage implements OnInit {
  readonly stats = signal<CurrentStatsDto | null>(null);
  readonly accounts = signal<ConnectedAccountDto[]>([]);
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly channelStats = signal<Record<string, CurrentStatsDto>>({});
  readonly events = signal<RecentEventDto[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    forkJoin({
      stats: this.api.getCurrentStats(),
      accounts: this.api.getConnectedAccounts(),
      channels: this.api.getChannels(),
      events: this.api.getRecentEvents()
    }).subscribe({
      next: ({ stats, accounts, channels, events }) => {
        this.stats.set(stats);
        this.accounts.set(accounts);
        this.channels.set(channels);
        this.events.set(events);
        this.message.set(null);
        this.channelStats.set({});
        for (const channel of channels) {
          this.api.getChannelStats(channel.id).subscribe({
            next: (stats) => this.channelStats.update((current) => ({ ...current, [channel.id]: stats })),
            error: () => undefined
          });
        }
      },
      error: (error: Error) => {
        this.messageTone.set('error');
        this.message.set(error.message);
      }
    });
  }

  channelsForAccount(accountId: string): MonitoredChannelDto[] {
    return this.channels().filter((channel) => channel.monitoredAccountId === accountId);
  }

  channelMetric(channelId: string, key: keyof CurrentStatsDto): number {
    const value = this.channelStats()[channelId]?.[key];
    return typeof value === 'number' ? value : 0;
  }
}
