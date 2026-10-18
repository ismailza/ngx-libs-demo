import { inject, Service } from '@angular/core';
import { ApiService } from '@ismailza/ngx-api-client';
import { Observable } from 'rxjs';

export interface Note {
  readonly id: string;
  readonly text: string;
  readonly createdAt: string;
}

/** `/notes` → `/api/v1/notes`, signed by authTokenInterceptor. */
@Service()
export class NotesApi {
  private readonly api = inject(ApiService);

  list(): Observable<Note[]> {
    return this.api.get<Note[]>('/notes');
  }

  create(text: string): Observable<Note> {
    return this.api.post<Note>('/notes', { text }, { successMessage: 'Note added.' });
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/notes/${id}`, { successMessage: 'Note deleted.' });
  }
}
