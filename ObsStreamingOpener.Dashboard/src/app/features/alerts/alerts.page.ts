import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AlertRuleDto, ManualAlertRequest, MonitoredChannelDto, StreamAlertDto, StreamEventAlertTraceDto, StreamSessionDto } from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-alerts-page',
  imports: [FormsModule, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Stream" title="Alerts management" />
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="panel">
      <div class="filter-chips">
        @for (channel of channels(); track channel.id) {
          <button type="button" class="chip" [class.selected]="selectedChannel()?.id === channel.id" (click)="selectChannel(channel)">
            {{ channel.displayName }}
          </button>
        }
      </div>
    </section>

    @if (selectedChannel(); as channel) {
      <section class="grid two">
        <article class="panel">
          <h2>Manual alert</h2>
          <form class="form-grid" (ngSubmit)="createManual(channel)">
            <label>Title <input name="title" [(ngModel)]="manual.title" required></label>
            <label>Message <input name="message" [(ngModel)]="manual.message"></label>
            <label>Style <input name="style" [(ngModel)]="manual.visualStyle"></label>
            <label>Duration <input name="duration" type="number" min="1" max="60" [(ngModel)]="manual.durationSeconds"></label>
            <button type="submit" [disabled]="!currentStream()">Send test alert</button>
          </form>
        </article>
        <article class="panel">
          <h2>Widget preview</h2>
          @if (currentStream(); as stream) {
            <iframe class="widget-preview" [src]="previewUrl(channel.id, stream.id)" title="Alert widget preview"></iframe>
          } @else {
            <p>No active stream. Manual alerts require a stream session.</p>
          }
        </article>
      </section>

      <section class="grid two">
        <article class="panel">
          <h2>Rules</h2>
          <div class="list compact">
            @for (rule of rules(); track rule.id) {
              <div class="list-row">
                <span>{{ rule.eventType }}</span>
                <small>{{ rule.enabled ? 'Enabled' : 'Disabled' }} / {{ rule.visualStyle }} / {{ rule.durationSeconds }}s</small>
              </div>
            } @empty { <p>No rules configured.</p> }
          </div>
        </article>
        <article class="panel">
          <h2>Recent alerts</h2>
          <div class="list compact">
            @for (alert of alerts(); track alert.id) {
              <div class="list-row"><span>{{ alert.title }}</span><small>{{ alert.sourceEventType || 'manual' }} / {{ alert.displayFromUtc }}</small></div>
            }
          </div>
        </article>
      </section>

      <section class="panel">
        <h2>Event alert trace</h2>
        <div class="list compact">
          @for (trace of traces(); track trace.eventId) {
            <div class="list-row"><span>{{ trace.title || trace.eventType }}</span><small>{{ trace.alertStatus }}</small></div>
          }
        </div>
      </section>
    }
  `
})
export class AlertsPage implements OnInit {
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly selectedChannel = signal<MonitoredChannelDto | null>(null);
  readonly currentStream = signal<StreamSessionDto | null>(null);
  readonly rules = signal<AlertRuleDto[]>([]);
  readonly alerts = signal<StreamAlertDto[]>([]);
  readonly traces = signal<StreamEventAlertTraceDto[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');
  manual: ManualAlertRequest = { streamSessionId: null, title: 'Test alert', message: 'Dashboard preview', visualStyle: 'spark', durationSeconds: 7, mediaUrl: null, soundUrl: null };

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.api.getChannels().subscribe({
      next: (channels) => {
        this.channels.set(channels);
        if (channels[0]) {
          this.selectChannel(channels[0]);
        }
      },
      error: (error: Error) => this.fail(error)
    });
  }

  selectChannel(channel: MonitoredChannelDto): void {
    this.selectedChannel.set(channel);
    forkJoin({
      rules: this.api.getAlertRules(channel.id),
      alerts: this.api.getRecentAlerts(channel.id),
      traces: this.api.getEventAlertTrace(channel.id),
      stream: this.api.getCurrentStream(channel.id)
    }).subscribe({
      next: ({ rules, alerts, traces, stream }) => {
        this.rules.set(rules);
        this.alerts.set(alerts);
        this.traces.set(traces);
        this.currentStream.set(stream);
        this.manual = { ...this.manual, streamSessionId: stream.id };
      },
      error: () => {
        this.rules.set([]);
        this.alerts.set([]);
        this.traces.set([]);
        this.currentStream.set(null);
      }
    });
  }

  createManual(channel: MonitoredChannelDto): void {
    this.api.createManualAlert(channel.id, this.manual).subscribe({
      next: () => {
        this.messageTone.set('info');
        this.message.set('Alert queued.');
        this.selectChannel(channel);
      },
      error: (error: Error) => this.fail(error)
    });
  }

  previewUrl(channelId: string, streamSessionId: string): string {
    return `/widgets/alerts/index.html?channelId=${channelId}&streamSessionId=${streamSessionId}&preview=true`;
  }

  private fail(error: Error): void {
    this.messageTone.set('error');
    this.message.set(error.message);
  }
}
