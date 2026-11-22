import { bootstrapApplication } from '@angular/platform-browser';
import { provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cameraOutline, imagesOutline, homeOutline, receiptOutline, logOutOutline,
  arrowBackOutline, addOutline, trashOutline, checkmarkCircleOutline, walletOutline,
  arrowForwardOutline, refreshOutline, scanOutline, closeOutline } from 'ionicons/icons';
import { AppComponent } from './app/app.component';

addIcons({ cameraOutline, imagesOutline, homeOutline, receiptOutline, logOutOutline,
  arrowBackOutline, addOutline, trashOutline, checkmarkCircleOutline, walletOutline,
  arrowForwardOutline, refreshOutline, scanOutline, closeOutline });

bootstrapApplication(AppComponent, {
  providers: [provideZoneChangeDetection(), provideRouter([]), provideIonicAngular({ mode: 'md' })],
}).catch(console.error);
