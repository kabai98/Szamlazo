import { Component, ChangeDetectorRef, ElementRef, ViewChild, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { bootstrapApplication } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';

type Source = 'manual' | 'pdf' | 'image' | 'spreadsheet';
type View = 'overview' | 'expenses' | 'analysis' | 'products';
type Dialog = 'expense' | 'import' | 'budget';
interface Item { name: string; amount: number | null; quantity?: number; unit?: string; unitPrice?: number; }
interface Expense {
  id: string;
  merchant: string;
  date: string;
  amount: number | null;
  category: string;
  source: Source;
  items: Item[];
  paymentMethod?: string;
  transactionTime?: string;
}
interface ProductLine {
  item: Item;
  expense: Expense;
}
interface ProductReport {
  rows: ProductLine[];
  total: number;
  stores: { name: string; amount: number }[];
}
interface Summary {
  budget: number | null;
  total: number;
  previous: number;
  expenses: Expense[];
  days: { day: number; amount: number }[];
  weekdays: { index: number; average: number }[];
  categories: { name: string; amount: number }[];
}

@Component({
  selector: 'app-root',
  imports: [FormsModule, NgTemplateOutlet],
  templateUrl: './app.html',
})
class App {
  @ViewChild('editor', { static: true }) editor!: ElementRef<HTMLDialogElement>;
  private readonly change = inject(ChangeDetectorRef);
  private csrf = '';
  private readonly format = new Intl.NumberFormat('hu-HU', { maximumFractionDigits: 2 });
  private readonly monthFormat = new Intl.DateTimeFormat('hu-HU', { year: 'numeric', month: 'long', timeZone: 'UTC' });
  private readonly dateFormat = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Budapest' });

  ready = false;
  busy = false;
  loading = false;
  email: string | null = null;
  authMode: 'login' | 'register' = 'login';
  credentials = { email: '', password: '' };
  categories: string[] = [];
  summary: Summary | null = null;
  month = this.dateFormat.format(new Date()).slice(0, 7);
  view: View = 'overview';
  dialog: Dialog = 'expense';
  editing = false;
  deleteTarget: Expense | null = null;
  rows: Expense[] = [];
  warnings: string[] = [];
  extracted = '';
  photoUrl = '';
  fileName = '';
  budget: number | null = null;
  error = '';
  message = '';
  query = '';
  productQuery = '';
  productMerchant = '';
  readonly weekdays = ['Hétfő', 'Kedd', 'Szerda', 'Csütörtök', 'Péntek', 'Szombat', 'Vasárnap'];
  readonly sources: Record<Source, string> = { manual: 'Kézi', pdf: 'PDF', image: 'Fotó', spreadsheet: 'Táblázat' };

  constructor() {
    void this.start();
  }

  get monthLabel(): string {
    return this.monthFormat.format(new Date(this.month + '-01T00:00:00Z'));
  }

  get shownExpenses(): Expense[] {
    const query = this.query.trim().toLocaleLowerCase('hu');
    const list = this.summary?.expenses ?? [];
    if (this.view === 'overview') return list.slice(0, 5);
    return query ? list.filter(row => [row.merchant, row.category]
      .some(value => value.toLocaleLowerCase('hu').includes(query))) : list;
  }

  get productMerchants(): string[] {
    const merchants = (this.summary?.expenses ?? []).filter(expense => expense.items.length)
      .map(expense => expense.merchant);
    return [...new Set(merchants)].sort((left, right) => left.localeCompare(right, 'hu'));
  }

  get productReport(): ProductReport {
    return this.buildProductReport(this.productWords(this.productQuery), this.productMerchant);
  }

  get expenseProductReport(): ProductReport | null {
    const words = this.productWords(this.query);
    if (!words.length) return null;
    const report = this.buildProductReport(words, '');
    return report.rows.length ? report : null;
  }

  private buildProductReport(words: string[], merchant: string): ProductReport {
    const rows: ProductLine[] = [];
    const stores = new Map<string, number>();
    let total = 0;
    for (const expense of this.summary?.expenses ?? []) {
      if (merchant && merchant !== expense.merchant) continue;
      for (const item of expense.items) {
        const itemWords = this.productWords(item.name);
        if (!words.every(word => itemWords.includes(word))) continue;
        const amount = Math.round((item.amount ?? 0) * 100);
        rows.push({ item, expense });
        total += amount;
        stores.set(expense.merchant, (stores.get(expense.merchant) ?? 0) + amount);
      }
    }
    return { rows, total: total / 100, stores: [...stores].map(([name, amount]) => ({ name, amount: amount / 100 }))
      .sort((left, right) => right.amount - left.amount) };
  }

  get peakDay(): number {
    return Math.max(1, ...(this.summary?.days ?? []).map(day => day.amount));
  }

  get peakWeekday(): number {
    return Math.max(1, ...(this.summary?.weekdays ?? []).map(day => day.average));
  }

  get highestWeekday(): string {
    const highest = [...(this.summary?.weekdays ?? [])].sort((a, b) => b.average - a.average)[0];
    return highest?.average ? this.weekdays[highest.index] : '—';
  }

  get budgetRemaining(): number | null {
    return this.summary?.budget == null ? null : this.summary.budget - this.summary.total;
  }

  get budgetProgress(): number {
    return this.summary?.budget ? Math.min(100, this.summary.total * 100 / this.summary.budget) : 0;
  }

  get changeLabel(): string {
    const previous = this.summary?.previous ?? 0;
    if (previous <= 0) return 'Az előző hónapból nincs kiadás';
    const percent = ((this.summary?.total ?? 0) / previous - 1) * 100;
    return (percent > 0 ? '+' : '') + this.format.format(Math.round(percent * 10) / 10) + '% az előző hónaphoz képest';
  }

  money(value: number | null | undefined): string {
    return value == null ? '—' : this.format.format(value) + ' Ft';
  }

  shortDate(value: string): string {
    return value.replaceAll('-', '.') + '.';
  }

  navigate(view: View): void {
    this.view = view;
    this.query = '';
    window.scrollTo(0, 0);
  }

  setAuth(mode: 'login' | 'register'): void {
    this.authMode = mode;
    this.error = '';
    this.credentials.password = '';
  }

  async authenticate(): Promise<void> {
    await this.action(async () => {
      await this.request('/' + this.authMode, 'POST', this.credentials);
      this.credentials = { email: '', password: '' };
      await this.session();
      await this.refresh();
    });
  }

  async logout(): Promise<void> {
    await this.action(async () => {
      await this.request('/logout', 'POST');
      this.summary = null;
      this.email = null;
      this.message = '';
      this.view = 'overview';
      this.productQuery = '';
      this.productMerchant = '';
      this.authMode = 'login';
      await this.session();
    });
  }

  setMonth(value: string): void {
    if (!/^(19\d{2}|20\d{2})-\d{2}$/.test(value) || this.loading) return;
    this.month = value;
    this.productMerchant = '';
    void this.refresh();
  }

  shiftMonth(offset: number): void {
    const date = new Date(this.month + '-01T00:00:00Z');
    date.setUTCMonth(date.getUTCMonth() + offset);
    this.setMonth(date.toISOString().slice(0, 7));
  }

  open(kind: Dialog, existing?: Expense): void {
    this.resetImport();
    this.dialog = kind;
    this.editing = !!existing;
    this.deleteTarget = null;
    this.error = '';
    this.message = '';
    this.rows = kind === 'expense' ? [existing ? structuredClone(existing) : this.newRow()] : [];
    this.budget = this.summary?.budget ?? null;
    this.editor.nativeElement.showModal();
  }

  close(): void {
    if (this.busy) return;
    this.editor.nativeElement.close();
  }

  resetEditor(): void {
    this.resetImport();
    this.rows = [];
    this.error = '';
  }

  async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.resetImport();
    this.rows = [];
    this.fileName = file.name;
    if (/\.(jpe?g|png|webp)$/i.test(file.name))
      this.photoUrl = URL.createObjectURL(file);
    const data = new FormData();
    data.set('file', file);
    await this.action(async () => {
      const preview = await this.request<{ rows: Omit<Expense, 'id'>[]; warnings: string[]; text: string | null }>('/import', 'POST', data);
      this.warnings = preview.warnings;
      this.extracted = preview.text ?? '';
      this.rows = preview.rows.map(row => ({
        ...row, id: crypto.randomUUID(), merchant: row.merchant ?? '', date: row.date ?? '',
      }));
    });
  }

  itemTotal(row: Expense): number {
    return Math.round(row.items.reduce((total, item) => total + (item.amount ?? 0), 0) * 100) / 100;
  }

  async save(): Promise<void> {
    await this.action(async () => {
      if (this.dialog === 'budget') {
        await this.request('/budget', 'PUT', { month: this.month, amount: this.budget });
        this.message = this.budget == null ? 'A havi keretet töröltük.' : 'A havi keret elmentve.';
      } else if (this.deleteTarget) {
        await this.request('/expenses/' + this.deleteTarget.id, 'DELETE');
        this.message = 'A kiadást töröltük.';
      } else {
        const first = this.rows[0];
        if (!first || this.rows.some(row => !row.merchant.trim() || !row.date || row.amount == null || row.amount <= 0)) {
          throw new Error('Minden kiadáshoz adj meg nevet, dátumot és pozitív összeget.');
        }
        if (this.editing) await this.request('/expenses/' + first.id, 'PUT', first);
        else await this.request('/expenses', 'POST', this.rows);
        this.message = this.rows.length === 1 ? 'A kiadás elmentve.' : this.rows.length + ' kiadás elmentve.';
        if (this.rows.length === 1) this.month = first.date.slice(0, 7);
        else if (!this.rows.some(row => row.date.startsWith(this.month + '-'))) this.month = first.date.slice(0, 7);
      }
      this.editor.nativeElement.close();
      await this.refresh();
    });
  }

  private newRow(): Expense {
    const today = this.dateFormat.format(new Date());
    return { id: crypto.randomUUID(), merchant: '', date: today.startsWith(this.month) ? today : this.month + '-01',
      amount: null, category: 'Egyéb', source: 'manual', items: [] };
  }

  private productWords(value: string): string[] {
    return value.toLocaleLowerCase('hu').normalize('NFD').replace(/\p{M}/gu, '').match(/[\p{L}\p{N}]+/gu) ?? [];
  }

  private resetImport(): void {
    if (this.photoUrl) URL.revokeObjectURL(this.photoUrl);
    this.photoUrl = '';
    this.extracted = '';
    this.fileName = '';
    this.warnings = [];
  }

  private async start(): Promise<void> {
    await this.action(async () => {
      await this.session();
      if (this.email) await this.refresh();
    });
    this.ready = true;
    this.change.markForCheck();
  }

  private async session(): Promise<void> {
    const session = await this.request<{ email: string | null; csrf: string; categories: string[] }>('/session');
    this.email = session.email;
    this.categories = session.categories;
    this.csrf = session.csrf;
  }

  private async refresh(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      this.summary = await this.request<Summary>('/dashboard?month=' + this.month);
    } catch (error) {
      this.summary = null;
      this.error = this.errorText(error);
    } finally {
      this.loading = false;
      this.change.markForCheck();
    }
  }

  private async action(work: () => Promise<void>): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.error = '';
    try {
      await work();
    } catch (error) {
      this.error = this.errorText(error);
    } finally {
      this.busy = false;
      this.change.markForCheck();
    }
  }

  private errorText(error: unknown): string {
    return error instanceof Error ? error.message : 'Váratlan hiba történt.';
  }

  private async request<T = void>(path: string, method = 'GET', body?: unknown): Promise<T> {
    const headers = new Headers();
    if (method !== 'GET') headers.set('X-Szamlazo-Csrf', this.csrf);
    if (body !== undefined && !(body instanceof FormData)) headers.set('Content-Type', 'application/json');
    let response: Response;
    try {
      response = await fetch('/api' + path, {
        method, headers, body: body instanceof FormData ? body : body === undefined ? undefined : JSON.stringify(body),
      });
    } catch {
      throw new Error('A kiszolgáló nem érhető el. Próbáld újra.');
    }
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}));
      if (response.status === 401 && this.email) {
        this.email = null;
        this.summary = null;
        this.editor.nativeElement.close();
        await this.session().catch(() => undefined);
      }
      const fallback = response.status === 429 ? 'Túl sok kérés érkezett. Próbáld újra később.'
        : response.status === 413 ? 'A fájl túl nagy. Legfeljebb 12 MB tölthető fel.'
          : response.status === 401 ? 'A munkamenet lejárt. Jelentkezz be újra.' : 'A művelet sikertelen. Próbáld újra.';
      throw new Error(problem.message ?? fallback);
    }
    return (response.status === 204 ? undefined : await response.json()) as T;
  }
}

bootstrapApplication(App).catch(error => console.error(error));
