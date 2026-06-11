import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  AlertRuleDto,
  AlertWidgetSettingsDto,
  ManualAlertRequest,
  MonitoredChannelDto,
  SaveAlertRuleRequest,
  StreamAlertDto,
  StreamEventAlertTraceDto,
  StreamSessionDto
} from '../../core/api/api.models';
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
      <div class="section-header">
        <div>
          <h2>Channel</h2>
          <p>{{ currentStream() ? 'Active stream detected.' : 'No active stream for this channel.' }}</p>
        </div>
        <button type="button" class="secondary" (click)="refresh()">Refresh</button>
      </div>
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
            <label>Style
              <select name="manualStyle" [(ngModel)]="manual.visualStyle">
                @for (style of visualStyles; track style) {
                  <option [value]="style">{{ style }}</option>
                }
              </select>
            </label>
            <label>Duration <input name="duration" type="number" min="1" max="60" [(ngModel)]="manual.durationSeconds"></label>
            <label>Media URL <input name="mediaUrl" [(ngModel)]="manual.mediaUrl"></label>
            <label>Sound URL <input name="soundUrl" [(ngModel)]="manual.soundUrl"></label>
            <button type="submit" [disabled]="!currentStream()">Post test alert</button>
          </form>
        </article>

        <article class="panel">
          <div class="section-header">
            <div>
              <h2>Widget preview</h2>
              <p>{{ previewUrl(channel.id) }}</p>
            </div>
            <a class="button secondary" [href]="previewUrl(channel.id)" target="_blank" rel="noreferrer">Open</a>
          </div>
          @if (currentStream(); as stream) {
            <iframe class="widget-preview" [src]="previewUrl(channel.id, stream.id)" title="Alert widget preview"></iframe>
          } @else {
            <p>No active stream. Manual alerts and alert preview require a stream session.</p>
          }
        </article>
      </section>

      <section class="grid two">
        <article class="panel">
          <h2>Widget behavior</h2>
          <form class="form-grid" (ngSubmit)="saveSettings()">
            <label>Theme <input name="theme" [(ngModel)]="settings.theme" required></label>
            <label>Queue
              <select name="queueOrdering" [(ngModel)]="settings.queueOrdering">
                <option value="shortest-first">shortest-first</option>
                <option value="oldest-first">oldest-first</option>
              </select>
            </label>
            <label>Min duration ms <input name="minDurationMs" type="number" min="500" max="60000" [(ngModel)]="settings.minDurationMs"></label>
            <label>Max duration ms <input name="maxDurationMs" type="number" min="500" max="60000" [(ngModel)]="settings.maxDurationMs"></label>
            <label>Animation <input name="animationPreset" [(ngModel)]="settings.animationPreset"></label>
            <label>Volume <input name="volume" type="number" min="0" max="1" step="0.05" [(ngModel)]="settings.volume"></label>
            <label>Default media URL <input name="defaultMediaUrl" [(ngModel)]="settings.defaultMediaUrl"></label>
            <label>Default sound URL <input name="defaultSoundUrl" [(ngModel)]="settings.defaultSoundUrl"></label>
            <label class="inline"><input name="autoAck" type="checkbox" [(ngModel)]="settings.autoAck"> Auto ack in OBS mode</label>
            <button type="submit">Save widget settings</button>
          </form>
        </article>

        <article class="panel">
          <h2>Recent alerts</h2>
          <div class="list compact">
            @for (alert of alerts(); track alert.id) {
              <div class="list-row">
                <span>{{ alert.title }}</span>
                <small>{{ alert.sourceEventType || 'manual' }} / {{ alert.visualStyle }} / {{ alert.displayFromUtc }}</small>
              </div>
            } @empty {
              <p>No recent alerts.</p>
            }
          </div>
        </article>
      </section>

      <section class="panel">
        <h2>Rules</h2>
        <div class="rule-grid">
          @for (rule of ruleForms(); track rule.eventType) {
            <form class="rule-card" (ngSubmit)="saveRule(rule)">
              <div class="section-header">
                <div>
                  <h3>{{ rule.eventType }}</h3>
                  <p>{{ rule.enabled ? 'Enabled' : 'Disabled' }}</p>
                </div>
                <label class="inline"><input [name]="'enabled-' + rule.eventType" type="checkbox" [(ngModel)]="rule.enabled"> Enabled</label>
              </div>
              <label>Minimum amount <input [name]="'min-' + rule.eventType" type="number" step="0.01" [(ngModel)]="rule.minimumAmount"></label>
              <label>Duration <input [name]="'duration-' + rule.eventType" type="number" min="1" max="60" [(ngModel)]="rule.durationSeconds"></label>
              <label>Style
                <select [name]="'style-' + rule.eventType" [(ngModel)]="rule.visualStyle">
                  @for (style of visualStyles; track style) {
                    <option [value]="style">{{ style }}</option>
                  }
                </select>
              </label>
              <label>Title template <input [name]="'title-' + rule.eventType" [(ngModel)]="rule.titleTemplate"></label>
              <label>Message template <input [name]="'message-' + rule.eventType" [(ngModel)]="rule.messageTemplate"></label>
              <label>Media URL <input [name]="'media-' + rule.eventType" [(ngModel)]="rule.mediaUrl"></label>
              <label>Sound URL <input [name]="'sound-' + rule.eventType" [(ngModel)]="rule.soundUrl"></label>
              <button type="submit">Save rule</button>
            </form>
          }
        </div>
      </section>

      <section class="panel">
        <h2>Event alert trace</h2>
        <div class="list compact">
          @for (trace of traces(); track trace.eventId) {
            <div class="list-row">
              <span>{{ trace.title || trace.eventType }}</span>
              <small>{{ trace.alertStatus }}</small>
            </div>
          } @empty {
            <p>No stream events yet.</p>
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
  readonly alerts = signal<StreamAlertDto[]>([]);
  readonly traces = signal<StreamEventAlertTraceDto[]>([]);
  readonly ruleForms = signal<SaveAlertRuleRequest[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');
  readonly visualStyles = ['system', 'fireworks', 'meme', 'tip', 'comment', 'audience', 'spark'];
  readonly eventTypes = ['Tip', 'CommentCreated', 'ChatMessage', 'AudienceRelationshipStarted', 'AudienceRelationshipRenewed', 'AudienceRelationshipEnded'];

  settings: AlertWidgetSettingsDto = {
    theme: 'default',
    queueOrdering: 'shortest-first',
    minDurationMs: 1000,
    maxDurationMs: 60000,
    defaultSoundUrl: null,
    defaultMediaUrl: null,
    animationPreset: 'sparkles',
    volume: 0.8,
    autoAck: true
  };
  manual: ManualAlertRequest = {
    streamSessionId: null,
    title: 'Test alert',
    message: 'Dashboard preview',
    visualStyle: 'spark',
    durationSeconds: 7,
    mediaUrl: null,
    soundUrl: null
  };

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    forkJoin({
      channels: this.api.getChannels(),
      settings: this.api.getAlertWidgetSettings()
    }).subscribe({
      next: ({ channels, settings }) => {
        this.channels.set(channels);
        this.settings = { ...settings };
        const selected = this.selectedChannel() ?? channels[0] ?? null;
        if (selected) {
          this.selectChannel(selected);
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
        this.alerts.set(alerts);
        this.traces.set(traces);
        this.currentStream.set(stream);
        this.manual = { ...this.manual, streamSessionId: stream?.id ?? null };
        this.ruleForms.set(this.buildRuleForms(channel.id, rules));
      },
      error: (error: Error) => this.fail(error)
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

  saveSettings(): void {
    this.api.upsertAlertWidgetSettings(this.settings).subscribe({
      next: (settings) => {
        this.settings = { ...settings };
        this.messageTone.set('info');
        this.message.set('Alert widget settings saved.');
      },
      error: (error: Error) => this.fail(error)
    });
  }

  saveRule(rule: SaveAlertRuleRequest): void {
    this.api.upsertAlertRule(this.normalizeRule(rule)).subscribe({
      next: () => {
        this.messageTone.set('info');
        this.message.set(`${rule.eventType} rule saved.`);
        const channel = this.selectedChannel();
        if (channel) {
          this.selectChannel(channel);
        }
      },
      error: (error: Error) => this.fail(error)
    });
  }

  previewUrl(channelId: string, streamSessionId?: string): string {
    const effectiveStreamSessionId = streamSessionId ?? this.currentStream()?.id;
    if (!effectiveStreamSessionId) {
      return '#';
    }

    return `/widgets/alerts/index.html?channelId=${channelId}&streamSessionId=${effectiveStreamSessionId}&preview=true`;
  }

  private buildRuleForms(channelId: string, rules: AlertRuleDto[]): SaveAlertRuleRequest[] {
    return this.eventTypes.map((eventType) => {
      const rule = rules.find((item) => item.eventType === eventType);
      return {
        monitoredChannelId: channelId,
        eventType,
        enabled: rule?.enabled ?? false,
        minimumAmount: rule?.minimumAmount ?? null,
        durationSeconds: rule?.durationSeconds ?? 6,
        visualStyle: rule?.visualStyle ?? 'system',
        titleTemplate: rule?.titleTemplate ?? null,
        messageTemplate: rule?.messageTemplate ?? null,
        mediaUrl: rule?.mediaUrl ?? null,
        soundUrl: rule?.soundUrl ?? null
      };
    });
  }

  private normalizeRule(rule: SaveAlertRuleRequest): SaveAlertRuleRequest {
    return {
      ...rule,
      minimumAmount: rule.minimumAmount === null || Number.isNaN(Number(rule.minimumAmount)) ? null : Number(rule.minimumAmount),
      durationSeconds: Number(rule.durationSeconds)
    };
  }

  private fail(error: Error): void {
    this.messageTone.set('error');
    this.message.set(error.message);
  }
}
