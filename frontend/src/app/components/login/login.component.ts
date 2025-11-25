import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly mode = signal<'login' | 'register'>('login');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    username: ['admin', [Validators.required, Validators.minLength(3)]],
    password: ['admin123', [Validators.required, Validators.minLength(6)]]
  });

  toggleMode() {
    this.mode.set(this.mode() === 'login' ? 'register' : 'login');
    this.error.set(null);
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const { username, password } = this.form.getRawValue();
    const action =
      this.mode() === 'login'
        ? this.authService.login(username, password)
        : this.authService.register(username, password);

    action.subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/board']);
      },
      error: (err) => {
        this.loading.set(false);
        const message = typeof err?.error === 'string' ? err.error : 'No se pudo completar la solicitud';
        this.error.set(message);
      }
    });
  }
}
