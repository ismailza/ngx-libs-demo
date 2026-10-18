import { Component, inject, signal } from '@angular/core';
import {
  email,
  form,
  FormField,
  FormRoot,
  maxLength,
  minLength,
  required,
} from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { isApiError } from '@ismailza/ngx-api-client';
import { AuthService } from '@ismailza/ngx-auth-client';
import { firstValueFrom } from 'rxjs';
import { AccountApi } from './account-api';

const FIELDS = [
  { id: 'username', label: 'Username', type: 'text', autocomplete: 'username' },
  { id: 'email', label: 'Email', type: 'email', autocomplete: 'email' },
  { id: 'password', label: 'Password', type: 'password', autocomplete: 'new-password' },
] as const;

type FieldId = (typeof FIELDS)[number]['id'];

/** Every field the form has, including the optional name fields. */
type ModelField = FieldId | 'firstName' | 'lastName';

const MODEL_FIELDS: readonly string[] = [
  ...FIELDS.map((field) => field.id),
  'firstName',
  'lastName',
];

const isModelField = (name: string): name is ModelField => MODEL_FIELDS.includes(name);

/**
 * The app's own sign-up page, for the Local provider. Once the account exists,
 * `signIn()` starts the session as usual.
 */
@Component({
  selector: 'app-register',
  imports: [FormField, FormRoot, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export default class Register {
  private readonly accounts = inject(AccountApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly fields = FIELDS;
  private readonly model = signal({
    username: '',
    email: '',
    password: '',
    firstName: '',
    lastName: '',
  });

  protected readonly registerForm = form(
    this.model,
    (path) => {
      required(path.username, { message: 'Choose a username.' });
      minLength(path.username, 3, { message: 'At least 3 characters.' });
      required(path.email, { message: 'Enter your email.' });
      email(path.email, { message: 'Enter a valid email.' });
      required(path.password, { message: 'Choose a password.' });
      minLength(path.password, 8, { message: 'At least 8 characters.' });
      maxLength(path.firstName, 100, { message: 'At most 100 characters.' });
      maxLength(path.lastName, 100, { message: 'At most 100 characters.' });
    },
    {
      submission: {
        action: async (field) => {
          const value = field().value();
          try {
            await firstValueFrom(this.accounts.register(value));
          } catch (error) {
            // Field errors belong to the form: route each { field, message } to its field.
            // Anything else lands on the form itself, which the template shows above the button.
            if (isApiError(error) && error.errors?.length) {
              return error.errors.map(({ field: name, message }) => ({
                kind: 'server',
                message,
                fieldTree: isModelField(name) ? field[name] : field,
              }));
            }
            throw error;
          }

          // signIn() bypasses the HTTP interceptors, so nothing else reports its failure.
          // The account exists by now: say so, or a second submit reports "username taken".
          try {
            await this.auth.signIn({ username: value.username, password: value.password });
          } catch {
            return {
              kind: 'server',
              message: 'Your account was created, but signing in failed. Please sign in.',
              fieldTree: field,
            };
          }
          await this.router.navigateByUrl('/notes');
          return;
        },
      },
    },
  );

  protected showErrors(id: ModelField): boolean {
    const field = this.registerForm[id]();
    return field.touched() && field.invalid();
  }
}
