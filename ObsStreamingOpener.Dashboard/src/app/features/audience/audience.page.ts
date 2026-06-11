import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AudienceActivityDto, AudienceRelationshipPeriodDto, MonitoredChannelDto } from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-audience-page',
  imports: [RouterLink, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Audience" title="Subscribers and patrons" />
    <oso-status-banner [message]="message()" tone="error" />

    <section class="panel">
      <div class="filter-chips">
        @for (channel of channels(); track channel.id) {
          <button type="button" class="chip" [class.selected]="selectedChannelId() === channel.id" (click)="selectChannel(channel.id)">
            {{ channel.displayName }}
          </button>
        }
      </div>
    </section>

    <section class="grid two">
      <article class="panel">
        <h2>Audience</h2>
        <div class="tile-grid">
          @for (person of audience(); track person.id) {
            <button type="button" class="person-card" [class.selected]="selectedAudienceId() === person.audienceMemberId" (click)="selectAudience(person)">
              <span class="corner-amount">{{ amountLabel(person) }}</span>
              <strong>{{ person.audienceDisplayName || 'Audience member' }}</strong>
              <small>{{ person.relationshipKind }} / {{ person.isPatron ? 'Patron' : 'Free' }}</small>
              @if (person.patronTierName) { <small>{{ person.patronTierName }}</small> }
            </button>
          } @empty {
            <p>No visible audience relationships yet.</p>
          }
        </div>
      </article>

      @if (activity(); as value) {
        <article class="panel">
          <div class="section-header">
            <div>
              <h2>Audience activity</h2>
              <p>{{ value.revenue.latestCurrencyTotal ?? 0 }} {{ value.revenue.latestCurrency || '' }}</p>
            </div>
            <a class="button secondary" [routerLink]="['/content']" [queryParams]="{ channelId: selectedChannelId(), audienceMemberId: value.audienceMemberId }">Open filtered resources</a>
          </div>
          <h3>Relationships</h3>
          <div class="list compact">
            @for (relationship of value.relationships; track relationship.id) {
              <div class="list-row"><span>{{ relationship.relationshipKind }}</span><small>{{ relationship.startedAt }} - {{ relationship.endedAt || 'active' }}</small></div>
            }
          </div>
          <h3>Payments</h3>
          <div class="filter-chips">
            @for (total of value.revenue.currencyTotals; track total.currency) {
              <span class="chip selected">{{ total.total }} {{ total.currency }}</span>
            } @empty {
              <span class="chip">No payments</span>
            }
          </div>
          <h3>Comments and events</h3>
          <div class="list compact">
            @for (message of value.messages; track message.id) {
              <div class="list-row"><span>{{ message.authorDisplayName || 'Viewer' }}</span><small>{{ message.messageText }}</small></div>
            }
            @for (event of value.events; track event.id) {
              <div class="list-row"><span>{{ event.title || event.eventType }}</span><small>{{ event.message || event.occurredAt }}</small></div>
            }
          </div>
        </article>
      }
    </section>
  `
})
export class AudiencePage implements OnInit {
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly audience = signal<AudienceRelationshipPeriodDto[]>([]);
  readonly selectedChannelId = signal<string | null>(null);
  readonly selectedAudienceId = signal<string | null>(null);
  readonly activity = signal<AudienceActivityDto | null>(null);
  readonly message = signal<string | null>(null);

  constructor(private readonly api: DashboardApiService, private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    this.api.getChannels().subscribe({
      next: (channels) => {
        this.channels.set(channels);
        this.selectChannel(this.route.snapshot.queryParamMap.get('channelId') ?? channels[0]?.id ?? null);
      },
      error: (error: Error) => this.message.set(error.message)
    });
  }

  selectChannel(channelId: string | null): void {
    this.selectedChannelId.set(channelId);
    this.selectedAudienceId.set(null);
    this.activity.set(null);
    if (!channelId) {
      this.audience.set([]);
      return;
    }

    this.api.getRecentAudience(channelId).subscribe({
      next: (audience) => this.audience.set(audience),
      error: (error: Error) => this.message.set(error.message)
    });
  }

  selectAudience(person: AudienceRelationshipPeriodDto): void {
    const channelId = this.selectedChannelId();
    if (!channelId) {
      return;
    }

    this.selectedAudienceId.set(person.audienceMemberId);
    this.api.getAudienceActivity(channelId, person.audienceMemberId).subscribe({
      next: (activity) => this.activity.set(activity),
      error: (error: Error) => this.message.set(error.message)
    });
  }

  amountLabel(person: AudienceRelationshipPeriodDto): string {
    return person.latestCurrency ? `${person.latestCurrencyTotal ?? 0} ${person.latestCurrency}` : '';
  }
}
