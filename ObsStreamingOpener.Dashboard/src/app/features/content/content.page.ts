import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MonitoredChannelDto, ProviderResourceDto } from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-content-page',
  imports: [RouterLink, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Drilldown" title="Content and streams" />
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
        <h2>Streams and videos</h2>
        <div class="list compact">
          @for (resource of resources(); track resource.id) {
            <button type="button" class="list-row clickable" [class.selected]="selectedResource()?.id === resource.id" (click)="selectResource(resource)">
              <span>{{ resource.title || resource.externalResourceId }}</span>
              <small>{{ resource.observedKinds.join(' + ') || resource.resourceKind }} / {{ resource.status || 'synced' }}</small>
            </button>
          } @empty {
            <p>No synced content yet.</p>
          }
        </div>
      </article>

      @if (selectedResource(); as resource) {
        <article class="panel">
          <div class="section-header">
            <div>
              <h2>{{ resource.title || resource.externalResourceId }}</h2>
              <p>{{ resource.url || resource.externalResourceId }}</p>
            </div>
            <a class="button secondary" [routerLink]="['/activity']" [queryParams]="{ channelId: resource.monitoredChannelId, providerResourceId: resource.id }">Open activity</a>
          </div>
          <dl class="detail-list">
            <div><dt>Kind</dt><dd>{{ resource.resourceKind }}</dd></div>
            <div><dt>Observed</dt><dd>{{ resource.observedKinds.join(', ') }}</dd></div>
            <div><dt>Status</dt><dd>{{ resource.status || 'unknown' }}</dd></div>
            <div><dt>Published</dt><dd>{{ resource.publishedAt || 'n/a' }}</dd></div>
            <div><dt>Scheduled</dt><dd>{{ resource.scheduledStartAt || 'n/a' }}</dd></div>
            <div><dt>Actual</dt><dd>{{ resource.actualStartAt || 'n/a' }} - {{ resource.actualEndAt || 'n/a' }}</dd></div>
          </dl>
          <h3>Patch history</h3>
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
        </article>
      }
    </section>
  `
})
export class ContentPage implements OnInit {
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly resources = signal<ProviderResourceDto[]>([]);
  readonly selectedChannelId = signal<string | null>(null);
  readonly selectedResource = signal<ProviderResourceDto | null>(null);
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
    this.selectedResource.set(null);
    if (!channelId) {
      this.resources.set([]);
      return;
    }

    this.api.getChannelRecentContent(channelId, 50).subscribe({
      next: (resources) => {
        this.resources.set(resources);
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
      next: (fresh) => this.selectedResource.set(fresh),
      error: (error: Error) => this.message.set(error.message)
    });
  }
}
