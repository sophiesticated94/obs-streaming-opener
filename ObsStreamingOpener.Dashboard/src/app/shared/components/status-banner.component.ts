import { Component, input } from '@angular/core';

@Component({
  selector: 'oso-status-banner',
  template: `
    @if (message()) {
      <div class="status" [class.error]="tone() === 'error'">{{ message() }}</div>
    }
  `
})
export class StatusBannerComponent {
  readonly message = input<string | null>(null);
  readonly tone = input<'info' | 'error'>('info');
}
