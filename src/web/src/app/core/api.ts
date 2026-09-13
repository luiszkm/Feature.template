import { HttpContextToken, HttpParams } from '@angular/common/http';

/** Every route the front consumes lives under this prefix; the interceptor keys off it. */
export const API_BASE = '/api/v1';

/** Opt a single request out of the global 403 -> forbidden screen redirect. */
export const LOCAL_403 = new HttpContextToken<boolean>(() => false);

export type SortDirection = 'asc' | 'desc';

export interface ListQuery {
  pageNumber: number;
  pageSize: number;
  searchTerm?: string;
  /** One of the fields the endpoint's `ApplySort` accepts; anything else falls back to its default. */
  sortBy?: string;
  sortDirection?: SortDirection;
}

export interface PaginatedList<T> {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  data: T[];
}

export const DEFAULT_PAGE_SIZE = 20;

export function listParams(query: ListQuery): HttpParams {
  let params = new HttpParams().set('pageNumber', query.pageNumber).set('pageSize', query.pageSize);

  if (query.searchTerm) {
    params = params.set('searchTerm', query.searchTerm);
  }

  if (query.sortBy) {
    params = params.set('sortBy', query.sortBy).set('sortDirection', query.sortDirection ?? 'asc');
  }

  return params;
}
