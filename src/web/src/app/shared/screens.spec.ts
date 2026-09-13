import { el, settle, text } from '../../testing';
import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { Forbidden, NotFound } from './screens';

describe('Forbidden', () => {
  it('mostra a mensagem de sem permissao e volta atras', async () => {
    const fixture = TestBed.createComponent(Forbidden);
    await settle(fixture, 1);

    expect(text(fixture, 'forbidden')).toContain('Sem permissão para esta operação');

    const back = vi.spyOn(TestBed.inject(Location), 'back');
    el(fixture, 'forbidden-back').click();
    await settle(fixture, 1);

    expect(back).toHaveBeenCalledTimes(1);
  });
});

describe('NotFound', () => {
  it('mostra o titulo recebido e liga de volta a lista', async () => {
    const fixture = TestBed.createComponent(NotFound);
    fixture.componentRef.setInput('title', 'Not found');
    await settle(fixture, 1);

    expect(text(fixture, 'not-found-title')).toBe('Not found');
    expect(
      el<HTMLAnchorElement>(fixture, 'not-found').querySelector('a')?.textContent?.trim(),
    ).toBe('Voltar');
  });
});
