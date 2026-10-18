import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, FormField, FormRoot, maxLength, validate } from '@angular/forms/signals';
import { isApiError } from '@ismailza/ngx-api-client';
import { firstValueFrom } from 'rxjs';
import { type Note, NotesApi } from './notes-api';

/**
 * Per-user data from a protected API. Failures are reported by the global
 * ApiErrorHandler, so this page has no error-toast code of its own.
 */
@Component({
  selector: 'app-notes',
  imports: [DatePipe, FormField, FormRoot],
  templateUrl: './notes.html',
  styleUrl: './notes.css',
})
export default class Notes {
  private readonly api = inject(NotesApi);

  protected readonly notes = rxResource({ stream: () => this.api.list(), defaultValue: [] });

  private readonly model = signal({ text: '' });
  protected readonly noteForm = form(
    this.model,
    (path) => {
      // Like the server: a note of only spaces counts as empty.
      validate(path.text, ({ value }) =>
        value().trim() ? null : { kind: 'required', message: 'Enter some text.' },
      );
      maxLength(path.text, 500);
    },
    {
      submission: {
        action: async (field) => {
          try {
            await firstValueFrom(this.api.create(field.text().value()));
          } catch (error) {
            // The global handler leaves field errors to the form; the only field is the text.
            if (isApiError(error) && error.errors?.length) {
              return error.errors.map(({ message }) => ({
                kind: 'server',
                message,
                fieldTree: field.text,
              }));
            }
            throw error;
          }
          field().reset({ text: '' });
          this.notes.reload();
          return;
        },
      },
    },
  );

  /** Client-side rules disable the button instead; only the server's answer is listed. */
  protected readonly serverErrors = computed(() =>
    this.noteForm
      .text()
      .errors()
      .filter((error) => error.kind === 'server'),
  );

  protected async remove(note: Note): Promise<void> {
    await firstValueFrom(this.api.delete(note.id));
    this.notes.reload();
  }
}
