import { Component, input } from '@angular/core';

@Component({
  selector: 'oso-page-header',
  template: `
    <header class="page-header">
      <div>
        <p>{{ eyebrow() }}</p>
        <h1>{{ title() }}</h1>
      </div>
      <ng-content />
    </header>
  `
})
export class PageHeaderComponent {
  readonly eyebrow = input('');
  readonly title = input.required<string>();
}
