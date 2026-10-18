import { JsonPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { isApiError } from '@ismailza/ngx-api-client';
import { firstValueFrom } from 'rxjs';
import { PlaygroundApi } from './playground-api';
import { type Scenario, SCENARIO_GROUPS } from './playground-scenarios';

interface Outcome {
  readonly scenario: string;
  readonly ok: boolean;
  readonly elapsedMs: number;
  readonly body: unknown;
}

/**
 * Buttons that hit misbehaving endpoints, to watch ngx-api-client's retry,
 * error normalisation, loading signal and versioning — with every attempt
 * still signed by ngx-auth-client.
 */
@Component({
  selector: 'app-playground',
  imports: [JsonPipe],
  templateUrl: './playground.html',
  styleUrl: './playground.css',
})
export default class Playground {
  private readonly api = inject(PlaygroundApi);

  protected readonly groups = SCENARIO_GROUPS;
  protected readonly running = signal<string | null>(null);
  protected readonly outcome = signal<Outcome | null>(null);

  protected async run(scenario: Scenario): Promise<void> {
    this.running.set(scenario.id);
    const key = `${scenario.id}-${Math.random().toString(36).slice(2, 10)}`;
    const started = performance.now();
    try {
      const body = await firstValueFrom(scenario.call(this.api, key));
      this.record(scenario, true, started, body);
    } catch (error) {
      this.record(scenario, false, started, isApiError(error) ? error : String(error));
    } finally {
      this.running.set(null);
    }
  }

  private record(scenario: Scenario, ok: boolean, started: number, body: unknown): void {
    this.outcome.set({
      scenario: scenario.label,
      ok,
      elapsedMs: Math.round(performance.now() - started),
      body,
    });
  }
}
