import type { Expense, ExpenseItem } from './models.ts';

export const MAX_FILE_BYTES = 12 * 1024 * 1024;
const supportedImages = /\.(jpe?g|jfif|png|webp|tiff?)$/i;

export function imageError(file: { name: string; size: number }): string | null {
  if (!supportedImages.test(file.name)) return 'JPG, PNG, WEBP vagy TIFF képet válassz.';
  if (file.size <= 0 || file.size > MAX_FILE_BYTES) return 'A kép legfeljebb 12 MB méretű lehet, és nem lehet üres.';
  return null;
}

function validMoney(value: number | null, positive: boolean): boolean {
  return value !== null && Number.isFinite(value) && Math.abs(value) <= 999999999 &&
    (positive ? value > 0 : true) && Math.abs(value * 100 - Math.round(value * 100)) < 0.00001;
}

export function itemError(item: ExpenseItem): string | null {
  if (!item.name?.trim() || item.name.trim().length > 160) return 'Minden tételhez adj meg egy legfeljebb 160 karakteres nevet.';
  if (!validMoney(item.amount, false) ||
      (item.kind === 'product' ? item.amount! < 0 : item.kind === 'discount' ? item.amount! >= 0 : true)) {
    return 'A termék ára legalább 0 Ft, a kedvezmény összege negatív legyen. Legfeljebb két tizedes adható meg.';
  }
  if (item.quantity != null && (!Number.isFinite(item.quantity) || item.quantity <= 0 || item.quantity > 100000)) {
    return 'A mennyiségnek pozitívnak és legfeljebb 100 000-nek kell lennie.';
  }
  if (item.unitPrice != null && !validMoney(item.unitPrice, true)) return 'Az egységár pozitív összeg legyen.';
  if (item.unit != null && item.unit.length > 12) return 'A mértékegység legfeljebb 12 karakter lehet.';
  return null;
}

export function expenseError(expense: Expense): string | null {
  if (!expense.merchant.trim() || expense.merchant.trim().length > 160) return 'Add meg a bolt nevét, legfeljebb 160 karakterrel.';
  const date = /^\d{4}-\d{2}-\d{2}$/.test(expense.date) ? new Date(expense.date + 'T00:00:00Z') : null;
  if (!date || !Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== expense.date ||
      expense.date < '1900-01-01' || expense.date > '2099-12-31') return 'Add meg a vásárlás érvényes dátumát.';
  if (!validMoney(expense.amount, true)) return 'A végösszeg pozitív szám legyen, legfeljebb két tizedessel.';
  if (!expense.category) return 'Válassz kategóriát.';
  if (expense.items.length > 100) return 'Legfeljebb 100 tétel menthető egy blokkhoz.';
  for (const item of expense.items) {
    const error = itemError(item);
    if (error) return error;
  }
  return null;
}

export function itemTotal(items: ExpenseItem[]): number {
  return items.reduce((sum, item) => sum + Math.round((item.amount ?? 0) * 100), 0) / 100;
}

export function matchesProduct(name: string, query: string): boolean {
  const words = (text: string): string[] => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().match(/[\p{L}\p{N}]+/gu) ?? [];
  const tokens = words(name);
  const requested = words(query);
  return requested.length > 0 && requested.every(word => tokens.includes(word));
}
