import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DashboardApiService } from '../../core/api/dashboard-api.service';
import { SaveWidgetConfigurationRequest, WidgetConfigurationDto } from '../../core/api/api.models';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { StatusBannerComponent } from '../../shared/components/status-banner.component';

@Component({
  selector: 'oso-widgets-page',
  imports: [FormsModule, PageHeaderComponent, StatusBannerComponent],
  template: `
    <oso-page-header eyebrow="OBS" title="Widgets" />
    <oso-status-banner [message]="message()" [tone]="messageTone()" />

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
      <h2>Widgets</h2>
      <div class="list">
        @for (widget of widgets(); track widget.id) {
          <div class="list-row">
            <span>{{ widget.widgetKey }}</span>
            <small>{{ widget.widgetType }} / {{ widget.theme }}</small>
            <a [href]="'/widgets/' + widget.widgetKey + '.html'" target="_blank">Open</a>
            <button type="button" class="secondary" (click)="edit(widget)">Edit</button>
          </div>
        }
      </div>
    </section>
  `
})
export class WidgetsPage implements OnInit {
  readonly widgets = signal<WidgetConfigurationDto[]>([]);
  readonly message = signal<string | null>(null);
  readonly messageTone = signal<'info' | 'error'>('info');
  form: SaveWidgetConfigurationRequest = this.emptyForm();

  constructor(private readonly api: DashboardApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.getWidgets().subscribe({ next: (widgets) => this.widgets.set(widgets), error: (error: Error) => this.fail(error) });
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
