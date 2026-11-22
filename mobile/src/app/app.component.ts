import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonApp, IonContent, IonHeader, IonToolbar, IonTitle, IonButton, IonIcon, IonSpinner, IonInput } from '@ionic/angular';
import { ApiService } from './core/api.service';
import { PhotoService } from './core/photo.service';
import { matchesProduct } from './core/expense-validation';
import type { Dashboard } from './core/models';
import { AuthComponent } from './auth/auth.component';
import { ScanComponent } from './scan/scan.component';

type Tab = 'overview' | 'scan' | 'expenses';

@Component({
  selector: 'app-root',
  imports: [FormsModule, IonApp, IonContent, IonHeader, IonToolbar, IonTitle,
    IonButton, IonIcon, IonSpinner, IonInput, AuthComponent, ScanComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  readonly api = inject(ApiService);
  private readonly photos = inject(PhotoService);
  readonly tab = signal<Tab>('overview');
  readonly ready = signal(false);
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  private readonly summaryData = signal<Dashboard | null>(null);
  private readonly summaryAccount = signal<string | null>(null);
  readonly summary = computed(() => this.summaryAccount() === this.api.email() ? this.summaryData() : null);
  readonly month = signal(new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Budapest' }).format(new Date()).slice(0, 7));
  readonly query = signal('');
  readonly searchMode = signal<'merchant' | 'product'>('merchant');
  readonly expanded = signal<string | null>(null);
  readonly money = (value: number | null | undefined) => new Intl.NumberFormat('hu-HU', { maximumFractionDigits: 2 }).format(value ?? 0) + ' Ft';
  readonly monthLabel = computed(() => new Intl.DateTimeFormat('hu-HU', { year: 'numeric', month: 'long', timeZone: 'UTC' })
    .format(new Date(this.month() + '-01T00:00:00Z')));
  readonly peak = computed(() => Math.max(1, ...(this.summary()?.days ?? []).map(day => day.amount)));
  readonly shownExpenses = computed(() => {
    const query = this.query().trim().toLocaleLowerCase('hu');
    const expenses = this.summary()?.expenses ?? [];
    if (!query || this.searchMode() === 'product') return expenses;
    return expenses.filter(expense => expense.merchant.toLocaleLowerCase('hu').includes(query));
  });
  readonly productLines = computed(() => (this.summary()?.expenses ?? []).flatMap(expense => expense.items
    .filter(item => matchesProduct(item.name, this.query()))
    .map(item => ({ expense, item }))));
  readonly productTotal = computed(() => this.productLines().reduce((sum, row) => sum + Math.round((row.item.amount ?? 0) * 100), 0) / 100);
  readonly budgetProgress = computed(() => Math.max(0, Math.min(100, (this.summary()?.total ?? 0) * 100 / (this.summary()?.budget || 1))));
  private request = 0;

  constructor() {
    effect(() => { if (this.photos.restoredFile()) this.tab.set('scan'); });
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.error.set('');
    try {
      await this.api.restoreSession();
      if (this.api.email()) await this.refresh();
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.ready.set(true);
    }
  }

  async refresh(): Promise<void> {
    const request = ++this.request;
    const account = this.api.email();
    this.loading.set(true);
    this.error.set('');
    try {
      const summary = await this.api.dashboard(this.month());
      if (request === this.request && this.api.email() === account) {
        this.summaryAccount.set(account);
        this.summaryData.set(summary);
      }
    } catch (error) {
      if (request === this.request) this.error.set(this.errorMessage(error));
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }

  changeMonth(value: string): void {
    if (!/^\d{4}-(0[1-9]|1[0-2])$/.test(value)) return;
    this.month.set(value);
    this.summaryData.set(null);
    void this.refresh();
  }

  async logout(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await this.api.logout();
      ++this.request;
      this.summaryData.set(null);
      this.query.set('');
      this.message.set('');
      this.tab.set('overview');
      this.photos.restoredFile.set(null);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  onSaved(date: string): void {
    this.month.set(date.slice(0, 7));
    this.query.set('');
    this.searchMode.set('merchant');
    this.tab.set('expenses');
    this.message.set('A kiadást elmentettük. A webes felületen is megtalálod.');
    void this.refresh();
  }

  chooseTab(tab: Tab): void {
    if (!this.busy()) this.tab.set(tab);
  }

  private errorMessage(error: unknown): string {
    return error instanceof Error ? error.message : 'Váratlan hiba történt. Próbáld újra.';
  }
}
