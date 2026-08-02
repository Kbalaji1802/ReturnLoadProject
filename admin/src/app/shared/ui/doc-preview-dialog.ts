import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, inject, signal, viewChild, ElementRef } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

import { ApiService } from '../../core/api/api.service';

/** One reviewable document, as the dialog needs it. */
export interface DocPreviewItem {
  documentId: string;
  title: string;
}

export interface DocPreviewData extends DocPreviewItem {
  /**
   * The queue being reviewed, so the dialog can step through it. Reviewing documents is a
   * sitting-down job — closing and reopening for each one is the difference between a usable
   * queue and a tedious one. Omit for a single-document preview.
   */
  queue?: DocPreviewItem[];
  /** Index of the opened document within {@link queue}. */
  index?: number;
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
      <h2>{{ current().title }}</h2>
      @if (queue().length > 1) {
        <span class="pos">{{ index() + 1 }} / {{ queue().length }}</span>
      }
      <span class="spacer"></span>
      @if (queue().length > 1) {
        <button mat-icon-button matTooltip="Previous document" [disabled]="index() === 0"
          (click)="step(-1)"><mat-icon>chevron_left</mat-icon></button>
        <button mat-icon-button matTooltip="Next document" [disabled]="index() >= queue().length - 1"
          (click)="step(1)"><mat-icon>chevron_right</mat-icon></button>
      }
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
        <img [src]="rawUrl()" [style.transform]="transform()" alt="{{ current().title }}" />
      } @else if (isPdf() && frameUrl()) {
        <iframe [src]="frameUrl()" title="{{ current().title }}"></iframe>
      } @else if (!loading()) {
        <!-- Neither an image nor a PDF. Saying so beats an empty frame the viewer cannot
             distinguish from a broken preview. -->
        <div class="msg">
          <mat-icon>description</mat-icon>
          <p>This file type can't be previewed in the browser.</p>
          <button mat-stroked-button (click)="download()"><mat-icon>download</mat-icon> Download to view</button>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; height: 100%; }
    .head { display: flex; align-items: center; gap: 4px; padding: 8px 12px; }
    .head h2 { font-size: 1rem; font-weight: 600; margin: 0; }
    .spacer { flex: 1; }
    .pos { font-size: 0.8rem; color: var(--mat-sys-on-surface-variant); margin-left: 10px; }
    .msg button { margin-top: 12px; }
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
  protected readonly isImage = signal(false);
  protected readonly isPdf = signal(false);
  protected readonly zoom = signal(1);
  protected readonly rotateDeg = signal(0);

  /** The queue being stepped through; a lone document when the caller passed none. */
  protected readonly queue = signal<DocPreviewItem[]>(
    this.data.queue?.length ? this.data.queue : [this.data]);
  protected readonly index = signal(this.data.index ?? 0);
  protected readonly current = () => this.queue()[this.index()] ?? this.data;

  private objectUrl: string | null = null;

  protected readonly transform = () => `scale(${this.zoom()}) rotate(${this.rotateDeg()}deg)`;

  constructor() {
    this.load();
  }

  /** Moves through the queue and reloads. Clamped, so the ends are inert rather than wrapping. */
  protected step(delta: number): void {
    const next = this.index() + delta;
    if (next < 0 || next >= this.queue().length) return;
    this.index.set(next);
    this.load();
  }

  private load(): void {
    this.revoke();
    this.loading.set(true);
    this.error.set(null);
    this.isImage.set(false);
    this.isPdf.set(false);
    this.rawUrl.set(null);
    this.frameUrl.set(null);
    this.zoom.set(1);
    this.rotateDeg.set(0);

    this.api.getBlob(`documents/${this.current().documentId}/file`).subscribe({
      next: (blob) => {
        // The server resolves the real MIME type from the stored file's extension, so the blob
        // type is authoritative. Anything else falls through to the explicit "download to view"
        // state rather than an empty frame.
        const type = blob.type || '';
        const image = type.startsWith('image/');
        const pdf = type === 'application/pdf';

        this.isImage.set(image);
        this.isPdf.set(pdf);
        this.objectUrl = URL.createObjectURL(blob);
        this.rawUrl.set(this.objectUrl);
        if (pdf) this.frameUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl));
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
    a.download = this.current().title.replace(/\s+/g, '_');
    a.click();
  }

  /** Frees the previous blob before loading another — stepping a long queue would otherwise
   *  hold every file open until the dialog closed. */
  private revoke(): void {
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = null;
  }

  ngOnDestroy(): void {
    this.revoke();
  }
}
