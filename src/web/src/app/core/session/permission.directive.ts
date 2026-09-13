import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { SessionStore } from './session.store';

/** Renders its content only when the session holds the permission (or the `Admin` role). */
@Directive({ selector: '[appHasPermission]' })
export class HasPermissionDirective {
  readonly appHasPermission = input.required<string>();

  private readonly session = inject(SessionStore);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);

  constructor() {
    effect(() => {
      const allowed = this.session.hasPermission(this.appHasPermission());
      this.viewContainer.clear();
      if (allowed) {
        this.viewContainer.createEmbeddedView(this.template);
      }
    });
  }
}
