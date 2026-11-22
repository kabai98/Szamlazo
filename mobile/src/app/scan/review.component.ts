import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonInput, IonSelect, IonSelectOption, IonButton, IonIcon, IonCheckbox } from '@ionic/angular';
import type { Expense, ExpenseItem } from '../core/models';
import { itemTotal } from '../core/expense-validation';

@Component({
  selector: 'app-review',
  imports: [FormsModule, IonInput, IonSelect, IonSelectOption, IonButton, IonIcon, IonCheckbox],
  template: `
    <section class="section-heading"><div><p class="eyebrow">02 · ELLENŐRZÉS</p><h2>A blokk adatai</h2></div></section>
    @for (row of rows(); track row.id) {
      <article class="surface review-card">
        <ion-input label="Bolt neve" labelPlacement="stacked" fill="outline" required maxlength="160" [(ngModel)]="row.merchant" />
        <div class="form-grid">
          <ion-input label="Vásárlás dátuma" labelPlacement="stacked" fill="outline" type="date" required [(ngModel)]="row.date" />
          <ion-input label="Végösszeg (Ft)" labelPlacement="stacked" fill="outline" type="number" min="0.01" max="999999999" step="0.01" required [(ngModel)]="row.amount" />
        </div>
        <ion-select label="Kategória" labelPlacement="stacked" fill="outline" [(ngModel)]="row.category" interface="action-sheet" cancelText="Mégse">
          @for (category of categories(); track category) { <ion-select-option [value]="category">{{ category }}</ion-select-option> }
        </ion-select>
        @if (row.paymentMethod) { <p class="muted">Fizetés: {{ row.paymentMethod }} {{ row.transactionTime ?? '' }}</p> }
        <div class="section-heading compact"><h3>Termékek és kedvezmények</h3><span>{{ row.items.length }} sor</span></div>
        @for (item of row.items; track $index; let index = $index) {
          <div class="item-editor">
            <div class="item-title"><span class="item-number">{{ index + 1 }}</span>
              <ion-input aria-label="Termék neve" placeholder="Termék neve" maxlength="160" [(ngModel)]="item.name" />
              <ion-button fill="clear" color="danger" size="small" aria-label="Tétel törlése" (click)="row.items.splice(index, 1)"><ion-icon name="trash-outline" /></ion-button>
            </div>
            <div class="form-grid">
              <ion-select label="Tétel típusa" labelPlacement="stacked" fill="outline" [ngModel]="item.kind" (ngModelChange)="changeKind(item, $event)" interface="popover">
                <ion-select-option value="product">Termék</ion-select-option>
                <ion-select-option value="discount">Kedvezmény</ion-select-option>
              </ion-select>
              <ion-input label="Összeg (Ft)" labelPlacement="stacked" fill="outline" type="number" step="0.01"
                [min]="item.kind === 'discount' ? -999999999 : 0" [max]="item.kind === 'discount' ? -0.01 : 999999999"
                (keydown)="preventNegative($event, item)" [(ngModel)]="item.amount" />
            </div>
            @if (item.quantity != null) { <p class="quantity">{{ item.quantity }} {{ item.unit }} · {{ item.unitPrice ?? '–' }} Ft / {{ item.unit }}</p> }
          </div>
        }
        <ion-button fill="outline" expand="block" [disabled]="row.items.length >= 100" (click)="row.items.push({name: '', amount: null, kind: 'product'})">
          <ion-icon name="add-outline" slot="start" /> Új tétel
        </ion-button>
        <div class="reconcile" [class.mismatch]="row.items.length > 0 && total(row) !== row.amount">
          <span>Tételek összege</span><strong>{{ total(row).toLocaleString('hu-HU') }} Ft</strong>
        </div>
        @if (row.items.length && total(row) !== row.amount) { <p class="notice warning">A tételek és a végösszeg eltérnek. Ellenőrizd az árakat és a kedvezményeket.</p> }
      </article>
    }
    <ion-checkbox [ngModel]="confirmed()" (ngModelChange)="confirmedChange.emit($event)" labelPlacement="end" class="review-confirmation">
      Ellenőriztem a felismert adatokat, javítottam az eltéréseket.
    </ion-checkbox>
  `,
})
export class ReviewComponent {
  readonly rows = input.required<Expense[]>();
  readonly categories = input.required<string[]>();
  readonly confirmed = input(false);
  readonly confirmedChange = output<boolean>();
  total(row: Expense): number { return itemTotal(row.items); }
  changeKind(item: ExpenseItem, kind: 'product' | 'discount'): void {
    item.kind = kind;
    item.amount = null;
    this.confirmedChange.emit(false);
  }
  preventNegative(event: KeyboardEvent, item: ExpenseItem): void {
    if (item.kind === 'product' && (event.key === '-' || event.key === '−')) event.preventDefault();
  }
}
