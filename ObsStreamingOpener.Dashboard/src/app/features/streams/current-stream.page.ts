import { Component, OnInit, computed, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  CurrentStatsDto,
  ManualAlertRequest,
  MonitoredChannelDto,
  StreamAlertDto,
  StreamEventAlertTraceDto,
  StreamSessionDto
} from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-current-stream-page',
  imports: [FormsModule, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="OBS" title="Active Stream">
      <button type="button" (click)="load()">Refresh</button>
    </oso-page-header>
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="panel">
      <div class="search-row">
        <label>Channel
          <select name="channel" [(ngModel)]="selectedChannelId" (ngModelChange)="loadStream()">
            @for (channel of channels(); track channel.id) {
              <option [value]="channel.id">{{ channel.displayName }}</option>
            }
          </select>
        </label>
      </div>
    </section>

    @if (currentStream(); as stream) {
      <section class="grid two">
        <article class="panel">
          <h2>{{ stream.title }}</h2>
          <dl class="detail-list">
            <div><dt>Session</dt><dd>{{ stream.externalSessionId || stream.id }}</dd></div>
            <div><dt>Started</dt><dd>{{ stream.actualStartAt || stream.startedAt }}</dd></div>
            <div><dt>Live chat</dt><dd>{{ stream.externalLiveChatId || 'n/a' }}</dd></div>
          </dl>
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
          <h2>Manual alert</h2>
          <form class="form-grid" (ngSubmit)="sendManualAlert()">
            <label>Title <input name="title" [(ngModel)]="manualAlert.title" required></label>
            <label>Message <input name="alertMessage" [(ngModel)]="manualAlert.message"></label>
            <label>Style
              <select name="style" [(ngModel)]="manualAlert.visualStyle">
                <option value="system">System</option>
                <option value="fireworks">Fireworks</option>
                <option value="meme">Meme</option>
                <option value="tip">Tip</option>
                <option value="comment">Comment</option>
                <option value="audience">Audience</option>
              </select>
            </label>
            <label>Duration <input type="number" min="1" max="60" name="duration" [(ngModel)]="manualAlert.durationSeconds"></label>
            <div class="actions">
              <button type="submit">Show test alert</button>
            </div>
          </form>
        </article>
      </section>

      <section class="panel">
        <div class="section-header">
          <div>
            <h2>Widget preview</h2>
            <p>{{ previewUrlText() }}</p>
          </div>
          <a class="button secondary" [href]="previewUrlText()" target="_blank" rel="noreferrer">Open widget</a>
        </div>
        <iframe class="widget-preview" [src]="previewUrl()" title="Alert widget preview"></iframe>
      </section>

      <section class="grid two">
        <article class="panel">
          <h2>Active alerts</h2>
          <div class="list compact">
            @for (alert of activeAlerts(); track alert.id) {
              <div class="list-row">
                <span>{{ alert.title }}</span>
                <small>{{ alert.visualStyle }} / {{ alert.displayUntilUtc }}</small>
                <button type="button" class="secondary" (click)="ack(alert)">Ack</button>
              </div>
            } @empty {
              <p>No active alerts.</p>
            }
          </div>
        </article>

        <article class="panel">
          <h2>Recent alerts</h2>
          <div class="list compact">
            @for (alert of recentAlerts(); track alert.id) {
              <div class="list-row">
                <span>{{ alert.title }}</span>
                <small>{{ alert.isSystemAlert ? 'System' : alert.sourceEventType }} / {{ alert.createdAtUtc }}</small>
              </div>
            } @empty {
              <p>No recent alerts.</p>
            }
          </div>
        </article>
      </section>

      <section class="panel">
        <h2>Event alert trace</h2>
        <div class="list compact">
          @for (event of trace(); track event.eventId) {
            <div class="list-row">
              <span>{{ event.title || event.actorName || event.eventType }}</span>
              <small>{{ event.message || event.occurredAt }}</small>
              <span class="badge" [class.badge-ok]="event.alertId" [class.badge-warn]="!event.alertId">{{ event.alertStatus }}</span>
            </div>
          } @empty {
            <p>No stream events yet.</p>
          }
        </div>
      </section>
    } @else {
      <section class="panel">
        <h2>No active stream</h2>
        <p>Manual alerts and widget preview require an active stream session.</p>
      </section>
    }
  `
})
export class CurrentStreamPage implements OnInit {
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly currentStream = signal<StreamSessionDto | null>(null);
  readonly stats = signal<CurrentStatsDto | null>(null);
  readonly activeAlerts = signal<StreamAlertDto[]>([]);
  readonly recentAlerts = signal<StreamAlertDto[]>([]);
  readonly trace = signal<StreamEventAlertTraceDto[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');
  selectedChannelId = '';
  manualAlert: ManualAlertRequest = {
    streamSessionId: null,
    title: 'Test alert',
    message: 'This is how the stream alert looks.',
    visualStyle: 'fireworks',
    durationSeconds: 7,
    mediaUrl: null,
    soundUrl: null
  };

  readonly previewUrlText = computed(() => {
    const stream = this.currentStream();
    return stream
      ? `/widgets/alerts/index.html?channelId=${stream.monitoredChannelId}&streamSessionId=${stream.id}&preview=true`
      : '';
  });

  readonly previewUrl = computed<SafeResourceUrl>(() => this.sanitizer.bypassSecurityTrustResourceUrl(this.previewUrlText()));

  constructor(private readonly api: DashboardApiService, private readonly sanitizer: DomSanitizer) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.getChannels().subscribe({
      next: (channels) => {
        this.channels.set(channels);
        this.selectedChannelId ||= channels[0]?.id ?? '';
        this.loadStream();
      },
      error: (error: Error) => this.fail(error)
    });
  }

  loadStream(): void {
    if (!this.selectedChannelId) {
      return;
    }

    this.api.getCurrentStream(this.selectedChannelId).pipe(catchError(() => of(null))).subscribe({
      next: (stream) => {
        this.currentStream.set(stream);
        if (stream) {
          this.manualAlert = { ...this.manualAlert, streamSessionId: stream.id };
          this.loadStreamData(stream);
        }
      },
      error: (error: Error) => this.fail(error)
    });
  }

  loadStreamData(stream: StreamSessionDto): void {
    forkJoin({
      stats: this.api.getChannelStats(stream.monitoredChannelId),
      activeAlerts: this.api.getActiveAlerts(stream.monitoredChannelId, stream.id),
      recentAlerts: this.api.getRecentAlerts(stream.monitoredChannelId, stream.id),
      trace: this.api.getEventAlertTrace(stream.monitoredChannelId, stream.id)
    }).subscribe({
      next: ({ stats, activeAlerts, recentAlerts, trace }) => {
        this.stats.set(stats);
        this.activeAlerts.set(activeAlerts);
        this.recentAlerts.set(recentAlerts);
        this.trace.set(trace);
        this.message.set(null);
      },
      error: (error: Error) => this.fail(error)
    });
  }

  sendManualAlert(): void {
    const stream = this.currentStream();
    if (!stream) {
      return;
    }

    this.api.createManualAlert(stream.monitoredChannelId, this.manualAlert).subscribe({
      next: () => {
        this.messageTone.set('info');
        this.message.set('Manual alert queued.');
        this.loadStreamData(stream);
      },
      error: (error: Error) => this.fail(error)
    });
  }

  ack(alert: StreamAlertDto): void {
    this.api.acknowledgeAlert(alert.monitoredChannelId, alert.id).subscribe({
      next: () => {
        const stream = this.currentStream();
        if (stream) {
          this.loadStreamData(stream);
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
