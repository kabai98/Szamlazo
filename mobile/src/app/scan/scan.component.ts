import { Component, DestroyRef, effect, inject, output, signal } from '@angular/core';
import { IonButton, IonIcon, IonSpinner } from '@ionic/angular';
import { ApiService } from '../core/api.service';
import { PhotoService } from '../core/photo.service';
import { expenseError, imageError } from '../core/expense-validation';
import type { Expense } from '../core/models';
import { ReviewComponent } from './review.component';

@Component({
  selector: 'app-scan',
  imports: [IonButton, IonIcon, IonSpinner, ReviewComponent],
  templateUrl: './scan.component.html',
})
export class ScanComponent {
  readonly api = inject(ApiService);
  readonly photos = inject(PhotoService);
  readonly saved = output<string>();
  readonly busyChange = output<boolean>();
  readonly busy = signal(false);
  readonly phase = signal('');
  readonly error = signal('');
  readonly file = signal<File | null>(null);
  readonly photoUrl = signal('');
  readonly rows = signal<Expense[]>([]);
  readonly warnings = signal<string[]>([]);
  readonly confirmed = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.releasePreview());
    effect(() => {
      const restored = this.photos.restoredFile();
      if (restored && !this.busy()) {
        this.select(restored);
        this.photos.restoredFile.set(null);
      }
    });
  }

  async capture(source: 'camera' | 'gallery'): Promise<void> {
    if (this.busy()) return;
    this.setBusy(true, 'Kép kiválasztása…');
    this.error.set('');
    try {
      const file = source === 'camera' ? await this.photos.takePhoto() : await this.photos.choosePhoto();
      if (file) this.select(file);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!/cancel|canceled|cancelled|mégse/i.test(message)) this.error.set(message || 'A kamera vagy a galéria nem érhető el.');
    } finally {
      this.setBusy(false);
    }
  }

  selectFromBrowser(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.select(file);
    input.value = '';
  }

  select(file: File): void {
    const error = imageError(file);
    if (error) { this.error.set(error); return; }
    this.reset();
    this.file.set(file);
    this.photoUrl.set(URL.createObjectURL(file));
  }

  async upload(): Promise<void> {
    const file = this.file();
    if (!file || this.busy()) return;
    this.setBusy(true, 'A blokk feldolgozása folyamatban…');
    this.error.set('');
    try {
      const preview = await this.api.preview(file);
      if (!preview.rows.length) throw new Error('Nem találtunk feldolgozható adatot. Próbálj másik képet.');
      this.rows.set(preview.rows.map(row => ({
        ...row, id: crypto.randomUUID(), merchant: row.merchant ?? '', date: row.date ?? '',
        items: row.items.map(item => ({ ...item, kind: item.kind ?? (item.amount != null && item.amount < 0 ? 'discount' : 'product') })),
      })));
      this.warnings.set(preview.warnings);
      this.confirmed.set(false);
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'A kép feldolgozása nem sikerült.');
    } finally {
      this.setBusy(false);
    }
  }

  async save(): Promise<void> {
    if (this.busy() || !this.confirmed() || !this.rows().length) return;
    for (const row of this.rows()) {
      const error = expenseError(row);
      if (error) { this.error.set(error); return; }
    }
    this.setBusy(true, 'Kiadás mentése…');
    this.error.set('');
    try {
      // IDs stay unchanged on retry, so a lost response cannot create duplicate expenses.
      await this.api.save(this.rows());
      const date = this.rows()[0].date;
      this.reset();
      this.saved.emit(date);
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'A mentés nem sikerült. Próbáld újra.');
    } finally {
      this.setBusy(false);
    }
  }

  reset(): void {
    this.releasePreview();
    this.file.set(null);
    this.rows.set([]);
    this.warnings.set([]);
    this.confirmed.set(false);
    this.error.set('');
  }

  private releasePreview(): void {
    if (this.photoUrl()) URL.revokeObjectURL(this.photoUrl());
    this.photoUrl.set('');
  }

  private setBusy(value: boolean, phase = ''): void {
    this.busy.set(value);
    this.phase.set(phase);
    this.busyChange.emit(value);
  }
}
