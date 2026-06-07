import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { CurrentStatsDto, MonitoredAccountDto, MonitoredChannelDto, SaveChannelSettingsRequest } from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-channels-page',
  imports: [FormsModule, RouterLink, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="YouTube" title="Discovered channels">
      <a class="button secondary" routerLink="/accounts">Connect or sync account</a>
    </oso-page-header>
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="grid two">
      @for (account of accounts(); track account.id) {
        <article class="panel">
          <h2>{{ account.displayName }}</h2>
          <div class="list compact">
            @for (channel of channelsFor(account.id); track channel.id) {
              <button type="button" class="list-row clickable" [class.selected]="selectedChannel()?.id === channel.id" (click)="selectChannel(channel)">
                <span>{{ channel.displayName }}</span>
                <small>{{ channel.provider }} / {{ channel.externalChannelId }}</small>
              </button>
            } @empty {
              <p>No channels discovered for this account.</p>
            }
          </div>
        </article>
      } @empty {
        <article class="panel">
          <h2>No accounts</h2>
          <p>Connect Google/YouTube account first.</p>
        </article>
      }
    </section>

    @if (selectedChannel(); as channel) {
      <section class="panel">
        <div class="section-header">
          <div>
            <h2>{{ channel.displayName }}</h2>
            <p>{{ channel.url || channel.externalChannelId }}</p>
          </div>
          <div class="actions">
            <a class="button secondary" [routerLink]="['/content']" [queryParams]="{ channelId: channel.id }">Content</a>
            <a class="button secondary" [routerLink]="['/audience']" [queryParams]="{ channelId: channel.id }">Audience</a>
            <a class="button secondary" [routerLink]="['/activity']" [queryParams]="{ channelId: channel.id }">Activity</a>
          </div>
        </div>

        @if (stats(); as value) {
          <dl class="stats-list">
            <button type="button" class="metric-tile" [routerLink]="['/activity']" [queryParams]="{ channelId: channel.id }">
              <dt>Viewers</dt><dd>{{ value.concurrentViewers ?? 0 }}</dd>
            </button>
            <button type="button" class="metric-tile" [routerLink]="['/activity']" [queryParams]="{ channelId: channel.id }">
              <dt>Likes</dt><dd>{{ value.likes ?? 0 }}</dd>
            </button>
            <button type="button" class="metric-tile" [routerLink]="['/audience']" [queryParams]="{ channelId: channel.id }">
              <dt>Audience</dt><dd>{{ value.audienceMemberCount ?? 0 }}</dd>
            </button>
            <button type="button" class="metric-tile" [routerLink]="['/revenue']">
              <dt>Tips</dt><dd>{{ value.tipTotal }}</dd>
            </button>
          </dl>
        }

        <h3>Local channel settings</h3>
        <form class="form-grid" (ngSubmit)="save(channel)">
          <label>Display name <input name="displayName" [(ngModel)]="form.displayName" required></label>
          <label>URL <input name="url" [(ngModel)]="form.url"></label>
          <label class="checkbox"><input type="checkbox" name="default" [(ngModel)]="form.isDefault"> Default</label>
          <label class="checkbox"><input type="checkbox" name="enabled" [(ngModel)]="form.isEnabled"> Enabled</label>
          <div class="actions">
            <button type="submit">Save settings</button>
            <a class="button secondary" [href]="channel.url || '#'" target="_blank" rel="noreferrer">Open</a>
          </div>
        </form>
      </section>
    }
  `
})
export class ChannelsPage implements OnInit {
  readonly accounts = signal<MonitoredAccountDto[]>([]);
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly selectedChannel = signal<MonitoredChannelDto | null>(null);
  readonly stats = signal<CurrentStatsDto | null>(null);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');
  form: SaveChannelSettingsRequest = { displayName: '', url: null, isDefault: false, isEnabled: true };

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.load();
  }

  channelsFor(accountId: string): MonitoredChannelDto[] {
    return this.channels().filter((channel) => channel.monitoredAccountId === accountId);
  }

  selectChannel(channel: MonitoredChannelDto): void {
    this.selectedChannel.set(channel);
    this.form = {
      displayName: channel.displayName,
      url: channel.url,
      isDefault: channel.isDefault,
      isEnabled: channel.isEnabled
    };
    this.api.getChannelStats(channel.id).subscribe({ next: (stats) => this.stats.set(stats), error: (error: Error) => this.fail(error) });
  }

  save(channel: MonitoredChannelDto): void {
    this.api.updateChannel(channel.id, this.form).subscribe({
      next: (updated) => {
        this.messageTone.set('info');
        this.message.set('Saved.');
        this.selectedChannel.set(updated);
        this.load();
      },
      error: (error: Error) => this.fail(error)
    });
  }

  private load(): void {
    forkJoin({ accounts: this.api.getAccounts(), channels: this.api.getChannels() }).subscribe({
      next: ({ accounts, channels }) => {
        this.accounts.set(accounts);
        this.channels.set(channels);
        const selected = this.selectedChannel();
        const next = selected ? channels.find((channel) => channel.id === selected.id) : channels[0];
        if (next) {
          this.selectChannel(next);
        }
      },
      error: (error: Error) => this.fail(error)
    });
  }

  private fail(error: Error): void {
    this.messageTone.set('error');
    this.message.set(error.message);
  }
}
