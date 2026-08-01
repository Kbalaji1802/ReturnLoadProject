import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, inject, signal, viewChild, ElementRef } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

import { ApiService } from '../../core/api/api.service';

export interface DocPreviewData {
  documentId: string;
  title: string;
}

/**
 * Previews a document's file in-dialog with zoom / rotate / fullscreen (Part 1). Fetches the file as
 * a blob (auth header attached), renders images with CSS transforms and PDFs in an object frame, and
 * offers a Download. The blob URL is revoked on close so nothing leaks.
 */
@Component({
  selector: 'rl-doc-preview-dialog',
  imports: [DecimalPipe, MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule],
  template: `
    <div class="head">
      <h2>{{ data.title }}</h2>
      <span class="spacer"></span>
      @if (isImage()) {
        <button mat-icon-button matTooltip="Zoom out" (click)="zoomBy(-0.25)"><mat-icon>zoom_out</mat-icon></button>
        <span class="zoom">{{ (zoom() * 100) | number:'1.0-0' }}%</span>
        <button mat-icon-button matTooltip="Zoom in" (click)="zoomBy(0.25)"><mat-icon>zoom_in</mat-icon></button>
        <button mat-icon-button matTooltip="Rotate" (click)="rotate()"><mat-icon>rotate_right</mat-icon></button>
      }
      <button mat-icon-button matTooltip="Fullscreen" (click)="toggleFullscreen()"><mat-icon>fullscreen</mat-icon></button>
      <button mat-icon-button matTooltip="Download" (click)="download()"><mat-icon>download</mat-icon></button>
      <button mat-icon-button matTooltip="Close" (click)="ref.close()"><mat-icon>close</mat-icon></button>
    </div>

    @if (loading()) { <mat-progress-bar mode="indeterminate" /> }

    <div class="stage" #stage>
      @if (error()) {
        <div class="msg"><mat-icon>broken_image</mat-icon><p>{{ error() }}</p></div>
      } @else if (isImage() && rawUrl()) {
        <img [src]="rawUrl()" [style.transform]="transform()" alt="{{ data.title }}" />
      } @else if (frameUrl()) {
        <iframe [src]="frameUrl()" title="{{ data.title }}"></iframe>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; height: 100%; }
    .head { display: flex; align-items: center; gap: 4px; padding: 8px 12px; }
    .head h2 { font-size: 1rem; font-weight: 600; margin: 0; }
    .spacer { flex: 1; }
    .zoom { font-size: 0.8rem; min-width: 44px; text-align: center; color: var(--mat-sys-on-surface-variant); }
    .stage { flex: 1; overflow: auto; display: flex; align-items: center; justify-content: center;
             background: var(--mat-sys-surface-container-high); min-height: 60vh; }
    .stage:fullscreen { background: #111; }
    img { max-width: 100%; max-height: 78vh; transition: transform 0.15s ease; }
    iframe { width: 100%; height: 78vh; border: 0; background: #fff; }
    .msg { text-align: center; color: var(--mat-sys-on-surface-variant); }
    .msg mat-icon { font-size: 48px; height: 48px; width: 48px; }
  `],
})
export class DocPreviewDialog implements OnDestroy {
  protected readonly ref = inject(MatDialogRef<DocPreviewDialog>);
  protected readonly data = inject<DocPreviewData>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly stage = viewChild<ElementRef<HTMLElement>>('stage');

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  /** Raw blob: URL for the <img> (blob URLs are safe in the URL context). */
  protected readonly rawUrl = signal<string | null>(null);
  /** Sanitised resource URL for the PDF <iframe> (RESOURCE_URL context). */
  protected readonly frameUrl = signal<SafeResourceUrl | null>(null);
  protected readonly isImage = signal(true);
  protected readonly zoom = signal(1);
  protected readonly rotateDeg = signal(0);

  private objectUrl: string | null = null;

  protected readonly transform = () => `scale(${this.zoom()}) rotate(${this.rotateDeg()}deg)`;

  constructor() {
    this.api.getBlob(`documents/${this.data.documentId}/file`).subscribe({
      next: (blob) => {
        const image = blob.type.startsWith('image/');
        this.isImage.set(image);
        this.objectUrl = URL.createObjectURL(blob);
        this.rawUrl.set(this.objectUrl);
        if (!image) this.frameUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl));
        this.loading.set(false);
      },
      error: () => { this.error.set('Could not load the document file.'); this.loading.set(false); },
    });
  }

  protected zoomBy(delta: number): void {
    this.zoom.set(Math.min(4, Math.max(0.25, +(this.zoom() + delta).toFixed(2))));
  }

  protected rotate(): void {
    this.rotateDeg.set((this.rotateDeg() + 90) % 360);
  }

  protected toggleFullscreen(): void {
    const el = this.stage()?.nativeElement;
    if (!el) return;
    if (document.fullscreenElement) {
      void document.exitFullscreen();
    } else {
      void el.requestFullscreen?.();
    }
  }

  protected download(): void {
    if (!this.objectUrl) return;
    const a = document.createElement('a');
    a.href = this.objectUrl;
    a.download = this.data.title.replace(/\s+/g, '_');
    a.click();
  }

  ngOnDestroy(): void {
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
  }
}
