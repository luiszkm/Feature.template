import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, PaginatedList } from '../../core/api';
import {
  CompareModelsRequest,
  ComparisonOutput,
  ComparisonSummaryOutput,
  ModelOutput,
} from './ai.contracts';

/**
 * HTTP client for the model catalog and comparisons. The comparison screens land in
 * `.tasks/comparar-modelos-telas.md`; the agent form already reads the catalog from here.
 */
@Injectable({ providedIn: 'root' })
export class ComparisonsClient {
  private readonly http = inject(HttpClient);

  listModels(): Observable<ModelOutput[]> {
    return this.http.get<ModelOutput[]>(`${API_BASE}/ai/models`);
  }

  compare(request: CompareModelsRequest): Observable<ComparisonOutput> {
    return this.http.post<ComparisonOutput>(`${API_BASE}/ai/comparisons`, request);
  }

  list(pageNumber = 1, pageSize = 20): Observable<PaginatedList<ComparisonSummaryOutput>> {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<PaginatedList<ComparisonSummaryOutput>>(`${API_BASE}/ai/comparisons`, { params });
  }

  get(comparisonId: string): Observable<ComparisonOutput> {
    return this.http.get<ComparisonOutput>(`${API_BASE}/ai/comparisons/${comparisonId}`);
  }
}
