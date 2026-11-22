import { Injectable, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { Camera, type MediaResult } from '@capacitor/camera';
import { App } from '@capacitor/app';
import { imageError } from './expense-validation';

@Injectable({ providedIn: 'root' })
export class PhotoService {
  readonly restoredFile = signal<File | null>(null);
  readonly restoredError = signal('');
  readonly native = Capacitor.isNativePlatform();

  constructor() {
    if (this.native) {
      void App.addListener('appRestoredResult', async result => {
        if (result.pluginId !== 'Camera' || !result.success) return;
        try {
          const photo = result.methodName === 'chooseFromGallery' ? result.data.results?.[0] : result.data;
          if (photo?.webPath) this.restoredFile.set(await this.toFile(photo));
        } catch {
          this.restoredError.set('A visszaállított fotó nem olvasható. Válaszd ki újra.');
        }
      });
    }
  }

  async takePhoto(): Promise<File | null> {
    const photo = await Camera.takePhoto({ quality: 90, saveToGallery: false, includeMetadata: true });
    return this.toFile(photo);
  }

  async choosePhoto(): Promise<File | null> {
    const { results } = await Camera.chooseFromGallery({ quality: 90, allowMultipleSelection: false, includeMetadata: true });
    return results[0] ? this.toFile(results[0]) : null;
  }

  private async toFile(photo: MediaResult): Promise<File> {
    if (!photo.webPath) throw new Error('A kamera nem adott vissza olvasható képet. Válaszd ki újra.');
    const response = await fetch(photo.webPath);
    if (!response.ok) throw new Error('A kiválasztott kép nem olvasható.');
    const blob = await response.blob();
    const format = (photo.metadata?.format ?? blob.type.split('/')[1] ?? 'jpeg').toLowerCase();
    const extension = format === 'jpeg' ? 'jpg' : format;
    const file = new File([blob], `blokk-${Date.now()}.${extension}`, { type: blob.type || `image/${format}` });
    const error = imageError(file);
    if (error) throw new Error(error);
    return file;
  }
}
