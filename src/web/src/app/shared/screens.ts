import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Location } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [MatButtonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="screen" data-testid="not-found">
      <h1 data-testid="not-found-title">{{ title() }}</h1>
      <a mat-stroked-button routerLink="/users">Voltar</a>
    </section>
  `,
  styles: `
    .screen {
      padding: 3rem;
      text-align: center;
    }
  `,
})
export class NotFound {
  readonly title = input('Registo não encontrado');
}

@Component({
  selector: 'app-forbidden',
  imports: [MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="screen" data-testid="forbidden">
      <h1>Sem permissão para esta operação</h1>
      <button mat-stroked-button type="button" data-testid="forbidden-back" (click)="back()">
        Voltar
      </button>
    </section>
  `,
  styles: `
    .screen {
      padding: 3rem;
      text-align: center;
    }
  `,
})
export class Forbidden {
  private readonly location = inject(Location);

  back(): void {
    this.location.back();
  }
}
