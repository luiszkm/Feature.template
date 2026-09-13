import { Injectable, signal } from '@angular/core';

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
}
