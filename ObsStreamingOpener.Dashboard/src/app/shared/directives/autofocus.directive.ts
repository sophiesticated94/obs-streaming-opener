import { AfterViewInit, Directive, ElementRef } from '@angular/core';

@Directive({
  selector: '[osoAutofocus]'
})
export class AutofocusDirective implements AfterViewInit {
  constructor(private readonly elementRef: ElementRef<HTMLElement>) {}

  ngAfterViewInit(): void {
    queueMicrotask(() => this.elementRef.nativeElement.focus());
  }
}
