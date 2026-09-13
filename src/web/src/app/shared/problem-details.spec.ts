import { el, maybeEl, settle, text } from '../../testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { NotFound } from './screens';
import {
  NOT_FOUND_MESSAGE,
  Problem,
  fieldError,
  handleMutationError,
  parseProblem,
  problemKind,
} from './problem-details';

function validationError(): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 400,
    error: {
      title: 'Validation failed',
      status: 400,
      errors: {
        Email: ["'Email' is not a valid email address."],
        Password: ["'Password' must not be empty."],
      },
    },
  });
}

@Component({
  imports: [],
  template: `
    <label for="email">Email</label>
    <input id="email" />
    @if (message('email'); as text) {
      <p data-testid="email-error">{{ text }}</p>
    }

    <label for="password">Password</label>
    <input id="password" />
    @if (message('password'); as text) {
      <p data-testid="password-error">{{ text }}</p>
    }
  `,
})
class FormHost {
  readonly problem = signal<Problem | null>(null);

  message(field: string): string | null {
    const problem = this.problem();
    return problem ? fieldError(problem, field) : null;
  }
}

describe('problem details', () => {
  it('mapeia errors para os campos do formulario', async () => {
    const fixture = TestBed.createComponent(FormHost);
    await settle(fixture, 1);

    expect(maybeEl(fixture, 'email-error')).toBeNull();

    fixture.componentInstance.problem.set(parseProblem(validationError()));
    await settle(fixture, 1);

    expect(text(fixture, 'email-error')).toBe("'Email' is not a valid email address.");
    expect(text(fixture, 'password-error')).toBe("'Password' must not be empty.");
  });

  it('404 renderiza not found', async () => {
    const problem = parseProblem(
      new HttpErrorResponse({
        status: 404,
        error: { title: 'Not found', detail: "User 'x' was not found.", status: 404 },
      }),
    );

    expect(problemKind(problem)).toBe('not-found');

    const fixture = TestBed.createComponent(NotFound);
    fixture.componentRef.setInput('title', problem.title);
    await settle(fixture, 1);

    expect(text(fixture, 'not-found-title')).toBe('Not found');
    expect(el(fixture, 'not-found')).toBeDefined();
  });

  it('404 em mutacao recarrega a lista', () => {
    const reload = vi.fn();
    const notFound = new HttpErrorResponse({ status: 404, error: { title: 'Not found' } });

    expect(handleMutationError(notFound, reload)).toBe(NOT_FOUND_MESSAGE);
    expect(reload).toHaveBeenCalledTimes(1);

    const conflict = new HttpErrorResponse({ status: 409, error: { title: 'Conflict' } });

    expect(handleMutationError(conflict, reload)).toBeNull();
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
