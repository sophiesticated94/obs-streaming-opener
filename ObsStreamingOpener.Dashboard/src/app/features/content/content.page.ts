import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { catchError, forkJoin, of } from 'rxjs';
import {
  CurrentStatsDto,
  MonitoredAccountDto,
  MonitoredChannelDto,
  ProviderMessageDto,
  ProviderResourceDto,
  RecentEventDto,
  StatsSummaryDto,
  StreamEventAlertTraceDto,
  StreamSessionDto
} from '../../core/api/api.models';
import { DashboardApiService, DashboardFilters } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-content-page',
  imports: [FormsModule, RouterLink, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Resources" title="Channel content drilldown">
      <a class="button secondary" routerLink="/accounts">Sync accounts</a>
    </oso-page-header>
    <oso-status-banner [message]="message()" tone="error" />

    <section class="panel">
      <div class="search-row">
        <label>Channel
          <select [ngModel]="selectedChannelId()" (ngModelChange)="selectChannel($event)">
            @for (account of accounts(); track account.id) {
              <optgroup [label]="account.displayName">
                @for (channel of channelsFor(account.id); track channel.id) {
                  <option [value]="channel.id">{{ channel.displayName }}</option>
                }
              </optgroup>
            }
          </select>
        </label>
        @if (selectedChannel(); as channel) {
          <div class="actions">
            <a class="button secondary" [routerLink]="['/audience']" [queryParams]="{ channelId: channel.id }">Audience</a>
            <a class="button secondary" [routerLink]="['/alerts']">Alerts</a>
          </div>
        }
      </div>
      <div class="filter-chips">
        @if (selectedChannel(); as channel) { <span class="chip selected">{{ channel.displayName }}</span> }
        @if (selectedResource(); as resource) { <button type="button" class="chip selected" (click)="clearResource()">Resource: {{ resource.title || resource.externalResourceId }} x</button> }
        @if (audienceFilterId(); as audienceId) { <span class="chip selected">Audience: {{ audienceId }}</span> }
      </div>
    </section>

    @if (resources().length > 0) {
      <section class="resource-grid" aria-label="Channel resources">
        @for (resource of resources(); track resource.id) {
          <button type="button" class="resource-card" [class.selected]="selectedResource()?.id === resource.id" (click)="selectResource(resource)">
            @if (resource.thumbnailUrl) {
              <img [src]="resource.thumbnailUrl" [alt]="resource.title || resource.externalResourceId" loading="lazy">
            } @else {
              <div class="resource-placeholder">{{ resourceBadge(resource) }}</div>
            }
            <span class="resource-status">{{ resourceBadge(resource) }}</span>
            <div class="resource-body">
              <strong>{{ resource.title || resource.externalResourceId }}</strong>
              <small>{{ resourceTime(resource) }}</small>
              <small>{{ resource.observedKinds.join(' + ') || resource.resourceKind }} / {{ resource.status || 'synced' }}</small>
              <div class="metric-strip">
                <span>{{ cardStats(resource.id)?.likes ?? 0 }} likes</span>
                <span>{{ cardStats(resource.id)?.concurrentViewers ?? 0 }} viewers</span>
                <span>{{ resource.durationSeconds ? formatDuration(resource.durationSeconds) : 'n/a' }}</span>
              </div>
            </div>
          </button>
        }
      </section>
    } @else {
      <section class="panel empty-state">
        <h2>No synced resources</h2>
        <p>Sync a connected YouTube account to discover videos, broadcasts and streams for this channel.</p>
        <a class="button secondary" routerLink="/accounts">Open accounts</a>
      </section>
    }

    @if (selectedResource(); as resource) {
      <section class="grid two">
        <article class="panel">
          <div class="section-header">
            <div>
              <h2>{{ resource.title || resource.externalResourceId }}</h2>
              <p>{{ resource.url || resource.externalResourceId }}</p>
            </div>
            <div class="actions">
              @if (resource.url) { <a class="button secondary" [href]="resource.url" target="_blank" rel="noreferrer">Open YouTube</a> }
            </div>
          </div>
          <dl class="detail-list">
            <div><dt>Kind</dt><dd>{{ resource.resourceKind }}</dd></div>
            <div><dt>Observed</dt><dd>{{ resource.observedKinds.join(', ') }}</dd></div>
            <div><dt>Status</dt><dd>{{ resource.status || 'unknown' }}</dd></div>
            <div><dt>Published</dt><dd>{{ resource.publishedAt || 'n/a' }}</dd></div>
            <div><dt>Scheduled</dt><dd>{{ resource.scheduledStartAt || 'n/a' }}</dd></div>
            <div><dt>Actual</dt><dd>{{ resource.actualStartAt || 'n/a' }} - {{ resource.actualEndAt || 'n/a' }}</dd></div>
            <div><dt>Duration</dt><dd>{{ resource.durationSeconds ? formatDuration(resource.durationSeconds) : 'n/a' }}</dd></div>
          </dl>
        </article>

        <article class="panel">
          <h2>Metrics</h2>
          @if (stats(); as value) {
            <dl class="stats-list">
              <div><dt>Viewers</dt><dd>{{ value.concurrentViewers }}</dd></div>
              <div><dt>Likes</dt><dd>{{ value.likes }}</dd></div>
              <div><dt>Chat/min</dt><dd>{{ value.chatMessagesPerMinute }}</dd></div>
              <div><dt>Tips</dt><dd>{{ value.tipTotal }}</dd></div>
              <div><dt>Audience</dt><dd>{{ value.audienceMemberCount }}</dd></div>
              <div><dt>Updated</dt><dd>{{ value.lastUpdatedAt || 'n/a' }}</dd></div>
            </dl>
          }
          @if (summary(); as value) {
            <h3>Summary</h3>
            <dl class="detail-list">
              <div><dt>Peak viewers</dt><dd>{{ value.peakViewers }}</dd></div>
              <div><dt>Average viewers</dt><dd>{{ value.averageViewers }}</dd></div>
              <div><dt>Messages</dt><dd>{{ value.chatMessages }}</dd></div>
              <div><dt>Events</dt><dd>{{ value.eventCount }}</dd></div>
            </dl>
          }
        </article>
      </section>

      <section class="grid two">
        <article class="panel">
          <h2>Messages</h2>
          <div class="list compact">
            @for (item of messages(); track item.id) {
              <div class="list-row">
                <span>{{ item.authorDisplayName || 'Viewer' }}</span>
                <small>{{ item.messageText || item.source }}</small>
                <small>{{ item.likeCount ?? 0 }} likes</small>
              </div>
            } @empty {
              <p>No messages for this context.</p>
            }
          </div>
        </article>

        <article class="panel">
          <h2>Events</h2>
          <div class="list compact">
            @for (event of events(); track event.id) {
              <div class="list-row">
                <span>{{ event.title || event.actorName || event.eventType }}</span>
                <small>{{ event.message || event.occurredAt }}</small>
                <small>{{ event.amount ? event.amount + ' ' + (event.currency || '') : event.provider }}</small>
              </div>
            } @empty {
              <p>No events for this context.</p>
            }
          </div>
        </article>
      </section>

      <section class="grid two">
        <article class="panel">
          <h2>Alert trace</h2>
          <div class="list compact">
            @for (trace of traces(); track trace.eventId) {
              <div class="list-row">
                <span>{{ trace.title || trace.eventType }}</span>
                <small>{{ trace.message || trace.occurredAt }}</small>
                <span class="badge" [class.badge-ok]="trace.alertId" [class.badge-warn]="!trace.alertId">{{ trace.alertStatus }}</span>
              </div>
            } @empty {
              <p>No alert trace for this context.</p>
            }
          </div>
        </article>

        <article class="panel">
          <div class="section-header">
            <div>
              <h2>Widget preview</h2>
              <p>{{ selectedWidgetLabel() }}</p>
            </div>
            <select [ngModel]="selectedWidgetKey()" (ngModelChange)="selectedWidgetKey.set($event)">
              @for (widget of widgetOptions; track widget.key) {
                <option [value]="widget.key">{{ widget.label }}</option>
              }
            </select>
          </div>
          @if (widgetPreviewUrl(); as previewUrl) {
            <iframe class="widget-preview" [src]="previewUrl" title="Widget preview"></iframe>
          } @else {
            <p>Alert widget preview requires an active stream session.</p>
          }
        </article>
      </section>

      <section class="panel">
        <h2>Patch history</h2>
        <div class="list compact">
          @for (patch of resource.patchHistory.slice().reverse(); track patch.capturedAtUtc) {
            <div class="list-row">
              <span>{{ patch.capturedAtUtc }}</span>
              <small>
                @for (field of patch.fields; track field.field) {
                  {{ field.field }}: {{ field.oldValue || 'null' }} -> {{ field.newValue || 'null' }};
                }
              </small>
            </div>
          } @empty {
            <p>No changes recorded.</p>
          }
        </div>
      </section>
    }
  `
})
export class ContentPage implements OnInit {
  readonly accounts = signal<MonitoredAccountDto[]>([]);
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly resources = signal<ProviderResourceDto[]>([]);
  readonly resourceStats = signal<Record<string, CurrentStatsDto | null>>({});
  readonly selectedChannelId = signal<string | null>(null);
  readonly selectedResource = signal<ProviderResourceDto | null>(null);
  readonly currentStream = signal<StreamSessionDto | null>(null);
  readonly stats = signal<CurrentStatsDto | null>(null);
  readonly summary = signal<StatsSummaryDto | null>(null);
  readonly messages = signal<ProviderMessageDto[]>([]);
  readonly events = signal<RecentEventDto[]>([]);
  readonly traces = signal<StreamEventAlertTraceDto[]>([]);
  readonly message = signal<string | null>(null);
  readonly audienceFilterId = signal<string | null>(null);
  readonly selectedWidgetKey = signal('stats');
  readonly widgetOptions = [
    { key: 'stats', label: 'Stats' },
    { key: 'recent-events', label: 'Recent events' },
    { key: 'goal', label: 'Goal' },
    { key: 'audience', label: 'Audience' },
    { key: 'comment-explorer', label: 'Comment explorer' },
    { key: 'alerts', label: 'Alerts' }
  ];
  readonly selectedChannel = computed(() => this.channels().find((channel) => channel.id === this.selectedChannelId()) ?? null);

  constructor(
    private readonly api: DashboardApiService,
    private readonly route: ActivatedRoute,
    private readonly sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    forkJoin({ accounts: this.api.getAccounts(), channels: this.api.getChannels() }).subscribe({
      next: ({ accounts, channels }) => {
        this.accounts.set(accounts);
        this.channels.set(channels);
        this.audienceFilterId.set(this.route.snapshot.queryParamMap.get('audienceMemberId'));
        this.selectChannel(this.route.snapshot.queryParamMap.get('channelId') ?? channels[0]?.id ?? null);
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }

  channelsFor(accountId: string): MonitoredChannelDto[] {
    return this.channels().filter((channel) => channel.monitoredAccountId === accountId);
  }

  selectChannel(channelId: string | null): void {
    this.selectedChannelId.set(channelId);
    this.selectedResource.set(null);
    this.resources.set([]);
    this.resourceStats.set({});
    this.stats.set(null);
    this.summary.set(null);
    this.messages.set([]);
    this.events.set([]);
    this.traces.set([]);
    this.currentStream.set(null);
    if (!channelId) {
      return;
    }

    forkJoin({
      stream: this.api.getCurrentStream(channelId).pipe(catchError(() => of(null))),
      resources: this.api.getChannelRecentContent(channelId, 100)
    }).subscribe({
      next: ({ stream, resources }) => {
        this.currentStream.set(stream);
        this.resources.set(resources);
        this.loadCardStats(channelId, resources);
        const queryResource = this.route.snapshot.queryParamMap.get('providerResourceId');
        const selected = resources.find((resource) => resource.id === queryResource) ?? resources[0] ?? null;
        if (selected) {
          this.selectResource(selected);
        }
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }

  selectResource(resource: ProviderResourceDto): void {
    this.api.getChannelContent(resource.monitoredChannelId, resource.id).subscribe({
      next: (fresh) => {
        this.selectedResource.set(fresh);
        this.loadResourceData(fresh);
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }

  clearResource(): void {
    this.selectedResource.set(null);
    this.stats.set(null);
    this.summary.set(null);
    this.messages.set([]);
    this.events.set([]);
    this.traces.set([]);
  }

  cardStats(resourceId: string): CurrentStatsDto | null {
    return this.resourceStats()[resourceId] ?? null;
  }

  selectedWidgetLabel(): string {
    return this.widgetOptions.find((widget) => widget.key === this.selectedWidgetKey())?.label ?? 'Widget';
  }

  widgetPreviewUrl(): SafeResourceUrl | null {
    const channelId = this.selectedChannelId();
    if (!channelId) {
      return null;
    }

    const key = this.selectedWidgetKey();
    const params = new URLSearchParams({ channelId });
    if (key === 'alerts') {
      const streamSessionId = this.stats()?.streamSessionId ?? this.currentStream()?.id;
      if (!streamSessionId) {
        return null;
      }

      params.set('streamSessionId', streamSessionId);
      params.set('preview', 'true');
      return this.sanitizer.bypassSecurityTrustResourceUrl(`/widgets/alerts/index.html?${params}`);
    }

    if (key === 'comment-explorer') {
      return this.sanitizer.bypassSecurityTrustResourceUrl(`/widgets/comment-explorer/index.html?${params}`);
    }

    return this.sanitizer.bypassSecurityTrustResourceUrl(`/widgets/${key}.html?${params}`);
  }

  resourceBadge(resource: ProviderResourceDto): string {
    if (resource.resourceKind === 'LiveBroadcast') {
      if (resource.actualEndAt || resource.status?.toLowerCase() === 'complete') {
        return 'Completed';
      }
      if (resource.actualStartAt || resource.status?.toLowerCase() === 'active') {
        return 'Live';
      }
      return 'Upcoming';
    }

    return resource.resourceKind;
  }

  resourceTime(resource: ProviderResourceDto): string {
    return resource.actualStartAt
      ?? resource.scheduledStartAt
      ?? resource.publishedAt
      ?? resource.lastSyncedAt;
  }

  formatDuration(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const rest = seconds % 60;
    return hours > 0
      ? `${hours}:${String(minutes).padStart(2, '0')}:${String(rest).padStart(2, '0')}`
      : `${minutes}:${String(rest).padStart(2, '0')}`;
  }

  private loadCardStats(channelId: string, resources: ProviderResourceDto[]): void {
    this.resourceStats.set({});
    for (const resource of resources) {
      this.api.getChannelStats(channelId, { providerResourceId: resource.id }).subscribe({
        next: (stats) => this.resourceStats.update((current) => ({ ...current, [resource.id]: stats })),
        error: () => this.resourceStats.update((current) => ({ ...current, [resource.id]: null }))
      });
    }
  }

  private loadResourceData(resource: ProviderResourceDto): void {
    const filters: DashboardFilters = {
      providerResourceId: resource.id,
      audienceMemberId: this.audienceFilterId()
    };

    forkJoin({
      stats: this.api.getChannelStats(resource.monitoredChannelId, filters),
      summary: this.api.getChannelStatsSummary(resource.monitoredChannelId, filters),
      messages: this.api.getChannelRecentMessages(resource.monitoredChannelId, 50, filters),
      events: this.api.getChannelRecentEvents(resource.monitoredChannelId, 50, filters),
      traces: this.api.getEventAlertTrace(resource.monitoredChannelId, undefined, 50, filters)
    }).subscribe({
      next: ({ stats, summary, messages, events, traces }) => {
        this.stats.set(stats);
        this.summary.set(summary);
        this.messages.set(messages);
        this.events.set(events);
        this.traces.set(traces);
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }
}
