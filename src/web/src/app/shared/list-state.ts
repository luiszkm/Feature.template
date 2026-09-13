import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Problem } from './problem-details';
import { ListStatus } from './list-store';

/** The loading, empty and error states every list screen renders the same way. */
@Component({
  selector: 'app-list-state',
  imports: [MatButtonModule, MatProgressBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (status()) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" data-testid="list-loading" />
      }
      @case ('empty') {
        <div class="list-state" data-testid="list-empty">
          <p>{{ emptyMessage() }}</p>
          <ng-content select="[emptyAction]" />
        </div>
      }
      @case ('error') {
        <div class="list-state" data-testid="list-error">
          <p data-testid="list-error-title">{{ problem()?.title }}</p>
          <button mat-stroked-button type="button" data-testid="list-retry" (click)="retry.emit()">
            Tentar de novo
          </button>
        </div>
      }
      @default {
        <ng-content />
      }
    }
  `,
  styles: `
    .list-state {
      padding: 2rem;
      text-align: center;
    }
  `,
})
export class ListState {
  readonly status = input.required<ListStatus>();
  readonly emptyMessage = input.required<string>();
  readonly problem = input<Problem | null>(null);
  readonly retry = output<void>();
}
