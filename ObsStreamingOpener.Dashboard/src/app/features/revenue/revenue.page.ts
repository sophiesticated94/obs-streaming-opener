import { Component, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import {
  ForecastSummaryDto,
  ProviderSyncResult,
  RevenueProviderStatusDto,
  RevenueRankingEntryDto,
  RevenueSummaryDto
} from '../../core/api/api.models';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-revenue-page',
  imports: [PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="Support" title="Revenue">
      <button type="button" (click)="sync()">Sync providers</button>
      <button type="button" class="secondary" (click)="load()">Refresh</button>
    </oso-page-header>
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

    <section class="grid two">
      <article class="panel">
        <h2>Summary</h2>
        @for (currency of summary()?.currencies ?? []; track currency.currency) {
          <dl class="stats-list">
            <div><dt>Currency</dt><dd>{{ currency.currency }}</dd></div>
            <div><dt>Gross</dt><dd>{{ currency.gross }}</dd></div>
            <div><dt>Estimated net</dt><dd>{{ currency.estimatedNet }}</dd></div>
            <div><dt>Fees</dt><dd>{{ currency.platformFees + currency.processorFees + currency.payoutFees }}</dd></div>
          </dl>
        } @empty {
          <p>No revenue recorded.</p>
        }
      </article>

      <article class="panel">
        <h2>Forecast</h2>
        @for (currency of forecast()?.currencies ?? []; track currency.currency) {
          <dl class="stats-list">
            <div><dt>Currency</dt><dd>{{ currency.currency }}</dd></div>
            <div><dt>Next 30 days</dt><dd>{{ currency.estimatedGross }}</dd></div>
            <div><dt>Active supports</dt><dd>{{ currency.activeSupportCount }}</dd></div>
          </dl>
        } @empty {
          <p>No active recurring support forecast.</p>
        }
      </article>
    </section>

    <section class="grid two">
      <article class="panel">
        <h2>Top supporters</h2>
        <div class="list">
          @for (entry of ranking(); track entry.supporterKey + entry.currency) {
            <div class="list-row">
              <span>{{ entry.displayName }}</span>
              <small>{{ entry.total }} {{ entry.currency }} / {{ entry.count }} events</small>
            </div>
          } @empty {
            <p>No supporters yet.</p>
          }
        </div>
      </article>

      <article class="panel">
        <h2>Provider status</h2>
        <div class="list">
          @for (provider of providers(); track provider.provider) {
            <div class="list-row">
              <span>{{ provider.provider }}</span>
              <small>{{ provider.enabled ? provider.status : 'Disabled' }}</small>
              <small>{{ provider.lastError ?? provider.lastSyncedAt ?? '' }}</small>
            </div>
          } @empty {
            <p>No support providers configured.</p>
          }
        </div>
      </article>
    </section>

    @if (lastSync().length) {
      <section class="panel">
        <h2>Last sync</h2>
        <div class="list">
          @for (result of lastSync(); track result.provider) {
            <div class="list-row">
              <span>{{ result.provider }}</span>
              <small>{{ result.success ? 'ok' : result.error }}</small>
              <small>{{ result.tipsProcessed }} tips / {{ result.patronsProcessed }} patrons</small>
            </div>
          }
        </div>
      </section>
    }
  `
})
export class RevenuePage implements OnInit {
  readonly summary = signal<RevenueSummaryDto | null>(null);
  readonly ranking = signal<RevenueRankingEntryDto[]>([]);
  readonly forecast = signal<ForecastSummaryDto | null>(null);
  readonly providers = signal<RevenueProviderStatusDto[]>([]);
  readonly lastSync = signal<ProviderSyncResult[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    forkJoin({
      summary: this.api.getRevenueSummary(),
      ranking: this.api.getRevenueRanking(),
      forecast: this.api.getRevenueForecast(30),
      providers: this.api.getRevenueProviderStatus()
    }).subscribe({
      next: ({ summary, ranking, forecast, providers }) => {
        this.summary.set(summary);
        this.ranking.set(ranking);
        this.forecast.set(forecast);
        this.providers.set(providers);
      },
      error: (error) => this.fail(error)
    });
  }

  sync(): void {
    this.api.syncRevenue().subscribe({
      next: (result) => {
        this.lastSync.set(result);
        this.messageTone.set('info');
        this.message.set('Revenue sync finished.');
        this.load();
      },
      error: (error) => this.fail(error)
    });
  }

  private fail(error: Error): void {
    this.messageTone.set('error');
    this.message.set(error.message);
  }
}
