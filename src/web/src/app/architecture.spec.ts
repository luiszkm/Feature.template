import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const WEB_ROOT = resolve(process.cwd());
const REPO_ROOT = resolve(WEB_ROOT, '..', '..');
const APP_ROOT = join(WEB_ROOT, 'src', 'app');

function filesUnder(dir: string, extension = '.ts'): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    return statSync(path).isDirectory()
      ? filesUnder(path, extension)
      : path.endsWith(extension)
        ? [path]
        : [];
  });
}

describe('arquitetura do front', () => {
  it('todas as rotas de features.json tem cliente', () => {
    const features = JSON.parse(readFileSync(join(REPO_ROOT, 'features.json'), 'utf8')) as Record<
      string,
      { route: string }
    >;

    const sources = filesUnder(APP_ROOT)
      .filter((path) => !path.endsWith('.spec.ts'))
      .map((path) => readFileSync(path, 'utf8'))
      .join('\n');

    const missing = Object.entries(features)
      .map(([slice, { route }]) => ({ slice, path: route.split(' ')[1] }))
      .filter(({ path }) => {
        const pattern = new RegExp(
          '\\$\\{API_BASE\\}' +
            path
              .replace('/api/v1', '')
              .replace(/[.*+?^$()|[\]\\]/g, '\\$&')
              .replace(/\{[^}]+\}/g, '\\$\\{[^}]+\\}'),
        );
        return !pattern.test(sources);
      })
      .map(({ slice, path }) => `${slice} (${path})`);

    expect(missing).toEqual([]);
  });

  it('clientes so chamam rotas documentadas', () => {
    const document = JSON.parse(
      readFileSync(join(REPO_ROOT, 'src', 'Api', 'openapi.json'), 'utf8'),
    ) as { paths: Record<string, unknown> };

    // `${id}` in a client and `{tenantId}` in the document name the same segment.
    const anonymize = (path: string) => path.replace(/\{[^}]+\}/g, '{param}');
    const documented = new Set(Object.keys(document.paths).map(anonymize));

    const called = filesUnder(APP_ROOT)
      .filter((path) => !path.endsWith('.spec.ts'))
      .flatMap((path) => [...readFileSync(path, 'utf8').matchAll(/\$\{API_BASE\}([^`'"?]*)/g)])
      .map((match) => anonymize(`/api/v1${match[1]}`.replace(/\$\{[^}]+\}/g, '{param}')))
      .filter((path, index, all) => all.indexOf(path) === index);

    expect(called.length).toBeGreaterThan(0);
    expect(called.filter((path) => !documented.has(path))).toEqual([]);
  });

  it('sem pastas de camada', () => {
    const forbidden = ['services', 'components', 'models', 'pages', 'dtos', 'interfaces'];
    const featuresRoot = join(APP_ROOT, 'features');

    const walk = (dir: string): string[] =>
      readdirSync(dir).flatMap((entry) => {
        const path = join(dir, entry);
        if (!statSync(path).isDirectory()) {
          return [];
        }
        return [...(forbidden.includes(entry) ? [path] : []), ...walk(path)];
      });

    expect(walk(featuresRoot)).toEqual([]);
  });

  it('tsconfig strict', () => {
    const tsconfig = JSON.parse(
      readFileSync(join(WEB_ROOT, 'tsconfig.json'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, ''),
    ) as { compilerOptions: { strict?: boolean } };

    expect(tsconfig.compilerOptions.strict).toBe(true);
  });

  it('web-e2e publica o relatorio', () => {
    const workflow = readFileSync(join(REPO_ROOT, '.github', 'workflows', 'ci.yml'), 'utf8');
    const job = workflow.slice(workflow.indexOf('  web-e2e:'));

    expect(job).toContain('actions/upload-artifact');
    expect(job).toContain('playwright-report');
    expect(job).toContain('if: always()');
  });

  it('providers http em todas as montagens', () => {
    const appConfig = readFileSync(join(APP_ROOT, 'app.config.ts'), 'utf8');
    const testProviders = readFileSync(join(WEB_ROOT, 'src', 'test-providers.ts'), 'utf8');
    const playwright = readFileSync(join(WEB_ROOT, 'playwright.config.ts'), 'utf8');

    for (const assembly of [appConfig, testProviders]) {
      expect(assembly).toContain('provideHttpClient(withInterceptors([apiInterceptor]))');
    }

    // The e2e assembly is the real application: Playwright serves what `ng serve` bootstraps.
    expect(playwright).toContain('npm run start');
  });
});
