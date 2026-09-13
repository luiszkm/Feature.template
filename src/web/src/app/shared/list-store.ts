import { signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { DEFAULT_PAGE_SIZE, ListQuery, PaginatedList } from '../core/api';
import { Problem, parseProblem } from './problem-details';

export type ListStatus = 'loading' | 'ready' | 'empty' | 'error';

/** Shared shape behind the four list screens: query in, status + rows out. */
export abstract class ListStore<T> {
  readonly items = signal<readonly T[]>([]);
  readonly total = signal(0);
  readonly status = signal<ListStatus>('loading');
  readonly problem = signal<Problem | null>(null);
  readonly query = signal<ListQuery>({ pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE });

  protected abstract fetch(query: ListQuery): Observable<PaginatedList<T>>;

  async load(patch: Partial<ListQuery> = {}): Promise<void> {
    const query = { ...this.query(), ...patch };
    this.query.set(query);
    this.status.set('loading');
    this.problem.set(null);

    try {
      const page = await firstValueFrom(this.fetch(query));
      this.items.set(page.data);
      this.total.set(page.totalCount);
      this.status.set(page.totalCount === 0 ? 'empty' : 'ready');
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
      this.status.set('error');
    }
  }

  /** Re-runs the query that failed, unchanged - the retry button's only job. */
  retry(): Promise<void> {
    return this.load();
  }

  protected replace(item: T, matches: (candidate: T) => boolean): void {
    this.items.update((items) => items.map((candidate) => (matches(candidate) ? item : candidate)));
  }

  protected removeWhere(matches: (candidate: T) => boolean): void {
    this.items.update((items) => items.filter((candidate) => !matches(candidate)));
    this.total.update((total) => Math.max(0, total - 1));
    if (this.items().length === 0) {
      this.status.set('empty');
    }
  }

  protected append(item: T): void {
    this.items.update((items) => [...items, item]);
    this.total.update((total) => total + 1);
    this.status.set('ready');
  }
}
