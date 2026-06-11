import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { catchError, forkJoin, of } from 'rxjs';
import {
  MonitoredAccountDto,
  MonitoredChannelDto,
  SaveWidgetConfigurationRequest,
  StreamSessionDto,
  WidgetConfigurationDto
} from '../../core/api/api.models';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-widgets-page',
  imports: [FormsModule, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="OBS" title="Widget preview and configuration" />
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="panel">
      <div class="section-header">
        <div>
          <h2>Preview</h2>
          <p>Use the same widget URLs that OBS browser sources will use.</p>
        </div>
        <div class="actions">
          <a class="button secondary" [href]="previewUrlText()" target="_blank" rel="noreferrer">Open widget</a>
        </div>
      </div>
      <div class="search-row">
        <label>Channel
          <select name="previewChannel" [(ngModel)]="selectedChannelId" (ngModelChange)="loadCurrentStream()">
            @for (account of accounts(); track account.id) {
              <optgroup [label]="account.displayName">
                @for (channel of channelsFor(account.id); track channel.id) {
                  <option [value]="channel.id">{{ channel.displayName }}</option>
                }
              </optgroup>
            }
          </select>
        </label>
        <label>Widget
          <select name="previewWidget" [(ngModel)]="selectedWidgetKey">
            @for (widget of previewWidgets; track widget.key) {
              <option [value]="widget.key">{{ widget.label }}</option>
            }
          </select>
        </label>
      </div>
      @if (previewUrl(); as url) {
        <iframe class="widget-preview" [src]="url" title="Widget preview"></iframe>
      } @else {
        <p>Alert widget preview requires an active stream session. Other widgets can be previewed without a live stream.</p>
      }
    </section>

    <section class="panel">
      <h2>Widget configuration</h2>
      <form class="form-grid" (ngSubmit)="save()">
        <label>Widget key
          <input name="widgetKey" [(ngModel)]="form.widgetKey" required>
        </label>
        <label>Widget type
          <input name="widgetType" [(ngModel)]="form.widgetType" required>
        </label>
        <label>Theme
          <input name="theme" [(ngModel)]="form.theme" required>
        </label>
        <label class="wide">Settings JSON
          <textarea name="settingsJson" rows="8" [(ngModel)]="form.settingsJson" required></textarea>
        </label>
        <div class="actions">
          <button type="submit">Save widget</button>
          <button type="button" class="secondary" (click)="reset()">Clear</button>
        </div>
      </form>
    </section>

    <section class="panel">
      <h2>Saved widgets</h2>
      <div class="list">
        @for (widget of widgets(); track widget.id) {
          <div class="list-row">
            <span>{{ widget.widgetKey }}</span>
            <small>{{ widget.widgetType }} / {{ widget.theme }}</small>
            <a [href]="'/widgets/' + widget.widgetKey + '.html'" target="_blank">Open</a>
            <button type="button" class="secondary" (click)="edit(widget)">Edit</button>
          </div>
        } @empty {
          <p>No saved widget configuration yet.</p>
        }
      </div>
    </section>
  `
})
export class WidgetsPage implements OnInit {
  readonly widgets = signal<WidgetConfigurationDto[]>([]);
  readonly accounts = signal<MonitoredAccountDto[]>([]);
  readonly channels = signal<MonitoredChannelDto[]>([]);
  readonly currentStream = signal<StreamSessionDto | null>(null);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');
  readonly previewWidgets = [
    { key: 'stats', label: 'Stats' },
    { key: 'recent-events', label: 'Recent events' },
    { key: 'goal', label: 'Goal' },
    { key: 'audience', label: 'Audience' },
    { key: 'comment-explorer', label: 'Comment explorer' },
    { key: 'alerts', label: 'Alerts' }
  ];
  selectedChannelId = '';
  selectedWidgetKey = 'stats';
  form: SaveWidgetConfigurationRequest = this.emptyForm();

  constructor(private readonly api: DashboardApiService, private readonly sanitizer: DomSanitizer) {}

  ngOnInit(): void {
    this.load();
  }

  channelsFor(accountId: string): MonitoredChannelDto[] {
    return this.channels().filter((channel) => channel.monitoredAccountId === accountId);
  }

  load(): void {
    forkJoin({
      widgets: this.api.getWidgets(),
      accounts: this.api.getAccounts(),
      channels: this.api.getChannels()
    }).subscribe({
      next: ({ widgets, accounts, channels }) => {
        this.widgets.set(widgets);
        this.accounts.set(accounts);
        this.channels.set(channels);
        this.selectedChannelId ||= channels[0]?.id ?? '';
        this.loadCurrentStream();
      },
      error: (error: Error) => this.fail(error)
    });
  }

  loadCurrentStream(): void {
    if (!this.selectedChannelId) {
      this.currentStream.set(null);
      return;
    }

    this.api.getCurrentStream(this.selectedChannelId).pipe(catchError(() => of(null))).subscribe({
      next: (stream) => this.currentStream.set(stream),
      error: (error: Error) => this.fail(error)
    });
  }

  previewUrlText(): string {
    if (!this.selectedChannelId) {
      return '#';
    }

    const params = new URLSearchParams({ channelId: this.selectedChannelId });
    if (this.selectedWidgetKey === 'alerts') {
      const streamSessionId = this.currentStream()?.id;
      if (!streamSessionId) {
        return '#';
      }

      params.set('streamSessionId', streamSessionId);
      params.set('preview', 'true');
      return `/widgets/alerts/index.html?${params}`;
    }

    if (this.selectedWidgetKey === 'comment-explorer') {
      return `/widgets/comment-explorer/index.html?${params}`;
    }

    return `/widgets/${this.selectedWidgetKey}.html?${params}`;
  }

  previewUrl(): SafeResourceUrl | null {
    const url = this.previewUrlText();
    return url === '#' ? null : this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  save(): void {
    this.api.upsertWidget(this.form).subscribe({
      next: () => {
        this.messageTone.set('info');
        this.message.set('Saved.');
        this.reset();
        this.load();
      },
      error: (error: Error) => this.fail(error)
    });
  }

  edit(widget: WidgetConfigurationDto): void {
    this.form = {
      widgetKey: widget.widgetKey,
      widgetType: widget.widgetType,
      theme: widget.theme,
      settingsJson: widget.settingsJson
    };
  }

  reset(): void {
    this.form = this.emptyForm();
  }

  private emptyForm(): SaveWidgetConfigurationRequest {
    return { widgetKey: '', widgetType: '', theme: 'default', settingsJson: '{}' };
  }

  private fail(error: Error): void {
    this.messageTone.set('error');
    this.message.set(error.message);
  }
}
