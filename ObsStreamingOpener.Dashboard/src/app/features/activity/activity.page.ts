import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AudienceRelationshipPeriodDto, MonitoredChannelDto, ProviderMessageDto, ProviderResourceDto, RecentEventDto } from '../../core/api/api.models';
import { DashboardApiService, DashboardFilters } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-activity-page',
  imports: [PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Activity" title="Events, comments and filtered context" />
    <oso-status-banner [message]="message()" tone="error" />

    <section class="panel">
      <div class="filter-chips">
        @if (selectedChannel(); as channel) { <span class="chip selected">{{ channel.displayName }}</span> }
        @if (selectedResource(); as resource) { <button type="button" class="chip selected" (click)="clearResource()">Resource: {{ resource.title || resource.externalResourceId }} x</button> }
        @if (selectedAudience(); as person) { <button type="button" class="chip selected" (click)="clearAudience()">Audience: {{ person.audienceDisplayName || person.audienceMemberId }} x</button> }
      </div>
    </section>

    <section class="grid two">
      <article class="panel">
        <h2>Resources</h2>
        <div class="list compact">
          @for (resource of resources(); track resource.id) {
            <button type="button" class="list-row clickable" [class.selected]="selectedResource()?.id === resource.id" (click)="toggleResource(resource)">
              <span>{{ resource.title || resource.externalResourceId }}</span>
              <small>{{ resource.observedKinds.join(' + ') || resource.resourceKind }}</small>
            </button>
          }
        </div>
      </article>
      <article class="panel">
        <h2>Audience</h2>
        <div class="list compact">
          @for (person of audience(); track person.id) {
            <button type="button" class="list-row clickable" [class.selected]="selectedAudience()?.audienceMemberId === person.audienceMemberId" (click)="toggleAudience(person)">
              <span>{{ person.audienceDisplayName || 'Audience member' }}</span>
              <small>{{ person.isPatron ? 'Patron' : 'Free' }} / {{ person.latestCurrencyTotal ?? 0 }} {{ person.latestCurrency || '' }}</small>
            </button>
          }
        </div>
      </article>
    </section>

    <section class="grid two">
      <article class="panel">
        <h2>Messages</h2>
        <div class="list compact">
          @for (message of messages(); track message.id) {
            <div class="list-row"><span>{{ message.authorDisplayName || 'Viewer' }}</span><small>{{ message.messageText || message.source }}</small></div>
          } @empty { <p>No messages for current filters.</p> }
        </div>
      </article>
      <article class="panel">
        <h2>Events</h2>
        <div class="list compact">
          @for (event of events(); track event.id) {
            <div class="list-row"><span>{{ event.title || event.actorName || event.eventType }}</span><small>{{ event.message || event.occurredAt }}</small></div>
          } @empty { <p>No events for current filters.</p> }
        </div>
      </article>
    </section>
  `
})
export class ActivityPage implements OnInit {
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly resources = signal<ProviderResourceDto[]>([]);
  readonly audience = signal<AudienceRelationshipPeriodDto[]>([]);
  readonly messages = signal<ProviderMessageDto[]>([]);
  readonly events = signal<RecentEventDto[]>([]);
  readonly selectedChannel = signal<MonitoredChannelDto | null>(null);
  readonly selectedResource = signal<ProviderResourceDto | null>(null);
  readonly selectedAudience = signal<AudienceRelationshipPeriodDto | null>(null);
  readonly message = signal<string | null>(null);

  constructor(private readonly api: DashboardApiService, private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    this.api.getChannels().subscribe({
      next: (channels) => {
        this.channels.set(channels);
        const channel = channels.find((item) => item.id === this.route.snapshot.queryParamMap.get('channelId')) ?? channels[0] ?? null;
        this.selectedChannel.set(channel);
        this.loadContext();
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }

  toggleResource(resource: ProviderResourceDto): void {
    this.selectedResource.set(this.selectedResource()?.id === resource.id ? null : resource);
    this.loadActivity();
  }

  toggleAudience(person: AudienceRelationshipPeriodDto): void {
    this.selectedAudience.set(this.selectedAudience()?.audienceMemberId === person.audienceMemberId ? null : person);
    this.loadActivity();
  }

  clearResource(): void {
    this.selectedResource.set(null);
    this.loadActivity();
  }

  clearAudience(): void {
    this.selectedAudience.set(null);
    this.loadActivity();
  }

  private loadContext(): void {
    const channel = this.selectedChannel();
    if (!channel) {
      return;
    }

    forkJoin({
      resources: this.api.getChannelRecentContent(channel.id, 50),
      audience: this.api.getRecentAudience(channel.id, 50, true)
    }).subscribe({
      next: ({ resources, audience }) => {
        this.resources.set(resources);
        this.audience.set(audience);
        const resourceId = this.route.snapshot.queryParamMap.get('providerResourceId');
        const audienceId = this.route.snapshot.queryParamMap.get('audienceMemberId');
        this.selectedResource.set(resources.find((item) => item.id === resourceId) ?? null);
        this.selectedAudience.set(audience.find((item) => item.audienceMemberId === audienceId) ?? null);
        this.loadActivity();
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }

  private loadActivity(): void {
    const channel = this.selectedChannel();
    if (!channel) {
      return;
    }

    const filters: DashboardFilters = {
      providerResourceId: this.selectedResource()?.id,
      audienceMemberId: this.selectedAudience()?.audienceMemberId
    };
    forkJoin({
      messages: this.api.getChannelRecentMessages(channel.id, 50, filters),
      events: this.api.getChannelRecentEvents(channel.id, 50, filters)
    }).subscribe({
      next: ({ messages, events }) => {
        this.messages.set(messages);
        this.events.set(events);
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }
}
