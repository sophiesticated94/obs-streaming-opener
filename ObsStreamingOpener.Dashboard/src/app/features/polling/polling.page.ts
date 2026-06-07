import { Component, OnInit, signal } from '@angular/core';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { PollingConfigurationDto } from '../../core/api/api.models';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-polling-page',
  imports: [PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Runtime" title="Polling">
      <button type="button" (click)="load()">Refresh</button>
    </oso-page-header>
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="panel">
      <h2>Effective polling configuration</h2>
      @if (polling(); as value) {
        <dl class="details-list">
          <div><dt>Stream data polling</dt><dd>{{ value.enableStreamDataPolling ? 'Enabled' : 'Disabled' }}</dd></div>
          <div><dt>Stream interval</dt><dd>{{ value.streamDataPollingSeconds }} seconds</dd></div>
          <div><dt>Stream schedule</dt><dd>{{ value.streamDataSchedule }}</dd></div>
          <div><dt>Account schedule</dt><dd>{{ value.accountDataSchedule }}</dd></div>
        </dl>
      }
      <p class="muted">Polling values are read from app configuration in v1.</p>
      <div class="actions">
        <a class="button" href="/hangfire" target="_blank" rel="noreferrer">Open Hangfire dashboard</a>
      </div>
    </section>

    <section class="panel">
      <h2>Recurring jobs</h2>
      <dl class="details-list">
        <div><dt>stream-data-sync</dt><dd>Active stream metrics/events</dd></div>
        <div><dt>account-data-sync</dt><dd>Between-stream account metrics/events</dd></div>
      </dl>
    </section>
  `
})
export class PollingPage implements OnInit {
  readonly polling = signal<PollingConfigurationDto | null>(null);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.getPolling().subscribe({
      next: (polling) => {
        this.polling.set(polling);
        this.message.set(null);
      },
      error: (error: Error) => {
        this.messageTone.set('error');
        this.message.set(error.message);
      }
    });
  }
}
