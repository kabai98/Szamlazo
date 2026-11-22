import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonButton, IonInput, IonSpinner, IonIcon } from '@ionic/angular';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-auth',
  imports: [FormsModule, IonButton, IonInput, IonSpinner, IonIcon],
  template: `
    <section class="auth-card">
      <div class="brand-mark"><ion-icon name="wallet-outline" aria-hidden="true" /></div>
      <p class="eyebrow">SZÁMLÁZÓ · A ZSEBEDBEN</p>
      <h1>A blokkod egy fotó.<br>A költéseid egy helyen.</h1>
      <p class="muted">Fotózd le, ellenőrizd és kövesd, mire megy el a pénzed.</p>
      <form (ngSubmit)="submit()">
        <h2>{{ register() ? 'Hozd létre a fiókod' : 'Jó újra látni!' }}</h2>
        <ion-input label="E-mail-cím" labelPlacement="stacked" fill="outline" type="email"
          name="email" autocomplete="username" required maxlength="254" [(ngModel)]="email" [disabled]="busy()" />
        <ion-input label="Jelszó" labelPlacement="stacked" fill="outline" type="password"
          name="password" [autocomplete]="register() ? 'new-password' : 'current-password'"
          required minlength="10" maxlength="128" [(ngModel)]="password" [disabled]="busy()" />
        @if (error()) { <p class="notice error" role="alert">{{ error() }}</p> }
        <ion-button expand="block" type="submit" [disabled]="busy()">
          @if (busy()) { <ion-spinner name="crescent" /> } @else { {{ register() ? 'Regisztráció' : 'Belépés' }} }
        </ion-button>
      </form>
      <ion-button expand="block" fill="clear" [disabled]="busy()" (click)="register.set(!register()); error.set('')">
        {{ register() ? 'Már van fiókom, belépek' : 'Még nincs fiókom' }}
      </ion-button>
      <p class="privacy-note">A webes fiókoddal itt is beléphetsz. A felismerés után mindig ellenőrizd az adatokat.</p>
    </section>
  `,
})
export class AuthComponent {
  private readonly api = inject(ApiService);
  readonly authenticated = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  readonly register = signal(false);
  email = '';
  password = '';

  async submit(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.authenticate(this.email, this.password, this.register());
      this.password = '';
      this.authenticated.emit();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'A belépés nem sikerült.');
    } finally {
      this.busy.set(false);
    }
  }
}
