import { HttpErrorResponse } from '@angular/common/http';

export interface Problem {
  status: number;
  title: string;
  detail: string;
  errors: Record<string, string[]>;
}

const FALLBACK_TITLE = 'Erro inesperado';

export const NOT_FOUND_MESSAGE = 'Registo não encontrado';

export function parseProblem(error: unknown): Problem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, title: FALLBACK_TITLE, detail: '', errors: {} };
  }

  const body = (error.error ?? {}) as Record<string, unknown>;
  const rawErrors = body['errors'];

  return {
    status: error.status,
    title: typeof body['title'] === 'string' ? body['title'] : FALLBACK_TITLE,
    detail: typeof body['detail'] === 'string' ? body['detail'] : '',
    errors: isFieldErrors(rawErrors) ? normalizeFieldErrors(rawErrors) : {},
  };
}

/** First message for a form field, matching ValidationProblemDetails casing from the API. */
export function fieldError(problem: Problem, field: string): string | null {
  const key = Object.keys(problem.errors).find(
    (candidate) => candidate.toLowerCase() === field.toLowerCase(),
  );
  return key ? (problem.errors[key][0] ?? null) : null;
}

/** What a list or detail screen renders when a read fails. */
export type ProblemKind = 'not-found' | 'forbidden' | 'error';

export function problemKind(problem: Problem): ProblemKind {
  if (problem.status === 404) {
    return 'not-found';
  }
  if (problem.status === 403) {
    return 'forbidden';
  }
  return 'error';
}

/**
 * A `404` on a mutation means the row is gone: tell the user and reload the list rather than
 * leaving a stale row on screen. Returns the message shown, or null when the caller owns it.
 */
export function handleMutationError(error: unknown, reload: () => void): string | null {
  const problem = parseProblem(error);
  if (problem.status === 404) {
    reload();
    return NOT_FOUND_MESSAGE;
  }
  return null;
}

function isFieldErrors(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function normalizeFieldErrors(raw: Record<string, unknown>): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(raw)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((m): m is string => typeof m === 'string');
    } else if (typeof messages === 'string') {
      result[field] = [messages];
    }
  }
  return result;
}
