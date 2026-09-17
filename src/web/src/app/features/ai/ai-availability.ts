import { Injectable, signal } from '@angular/core';

export const AI_DISABLED_TITLE = 'Feature disabled';

/**
 * `EnableAI` is a server-side flag; the front learns it is off from the `404` the gate returns
 * and stops offering the screen for the rest of the session.
 */
@Injectable({ providedIn: 'root' })
export class AiAvailability {
  readonly available = signal(true);

  disable(): void {
    this.available.set(false);
  }

  learnFrom(problem: { status: number; title: string } | null): void {
    if (problem?.status === 404 && problem.title === AI_DISABLED_TITLE) {
      this.disable();
    }
  }
}
