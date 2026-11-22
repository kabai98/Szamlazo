import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import type { Dashboard, Expense, ImportPreview, Session } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  readonly email = signal<string | null>(null);
  readonly categories = signal<string[]>([]);
  private csrf = '';

  async restoreSession(): Promise<void> {
    const session = await this.request<Session>('GET', '/session');
    this.email.set(session.email);
    this.categories.set(session.categories);
    this.csrf = session.csrf;
  }

  async authenticate(email: string, password: string, register: boolean): Promise<void> {
    await this.restoreSession();
    await this.request('POST', register ? '/register' : '/login', { email: email.trim(), password });
    await this.restoreSession();
  }

  async logout(): Promise<void> {
    await this.request('POST', '/logout');
    this.email.set(null);
    this.csrf = '';
    await this.restoreSession();
  }

  dashboard(month: string): Promise<Dashboard> {
    return this.request('GET', '/dashboard?month=' + encodeURIComponent(month));
  }

  preview(file: File): Promise<ImportPreview> {
    const data = new FormData();
    data.set('file', file, file.name);
    return this.request('POST', '/import', data);
  }

  save(expenses: Expense[]): Promise<void> {
    return this.request('POST', '/expenses', expenses);
  }

  private async request<T = void>(method: string, path: string, body?: unknown): Promise<T> {
    const headers: Record<string, string> = {};
    if (method !== 'GET') headers['X-Szamlazo-Csrf'] = this.csrf;
    const multipart = body instanceof FormData;
    if (body !== undefined && !multipart) headers['Content-Type'] = 'application/json';
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 150000);
    try {
      // On Android CapacitorHttp patches fetch, including multipart and native cookies.
      // Browser development uses the same-origin Angular proxy.
      const response = await fetch(environment.apiUrl + path, {
        method, headers, credentials: 'include', signal: controller.signal,
        body: body === undefined ? undefined : multipart ? body : JSON.stringify(body),
      });
      const text = await response.text();
      let data: Record<string, unknown> | undefined;
      try { data = text ? JSON.parse(text) : undefined; } catch { /* Non-JSON proxy errors get a useful message below. */ }
      if (response.status === 401) {
        this.email.set(null);
        this.csrf = '';
        throw new Error(path === '/login' ? 'Az e-mail-cím vagy a jelszó hibás.' : 'A munkamenet lejárt. Lépj be újra.');
      }
      if (!response.ok) throw new Error(String(data?.['error'] ?? data?.['detail'] ?? data?.['title'] ?? 'A kérés nem sikerült. Próbáld újra.'));
      return data as T;
    } catch (error) {
      if (error instanceof TypeError) throw new Error('A szerver nem érhető el. Ellenőrizd a kapcsolatot és a backend címét.');
      if (error instanceof DOMException && error.name === 'AbortError') throw new Error('A feldolgozás túl sokáig tartott. Próbálj kisebb képet.');
      throw error;
    } finally {
      clearTimeout(timeout);
    }
  }
}
