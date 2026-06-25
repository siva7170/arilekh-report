import {
  Component, Input, OnInit, OnDestroy, AfterViewInit,
  ViewChild, ViewChildren, QueryList, ElementRef,
  ChangeDetectionStrategy, ChangeDetectorRef,
  Output,
  EventEmitter
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, fromEvent } from 'rxjs';
import { takeUntil, debounceTime } from 'rxjs/operators';

import { ReportViewerApiService, PageResponse } from '../services/report-viewer-api.service';
import { PageRendererComponent } from './page-renderer.component';
import { LeftPanelComponent } from './left-panel.component';

const PT_TO_PX     = 1.333;
const WINDOW_RADIUS = 5;   // render ±5 pages around current

@Component({
  selector: 'arv-report-viewer',
  standalone: true,
  imports: [CommonModule, FormsModule, PageRendererComponent, LeftPanelComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="arv-shell" [attr.data-theme]="theme">

      <div class="arv-header-of-arview">
        <span class="arv-header__title">📄 {{ reportTitle }}</span>

        <div class="arv-header__btn-group">
           <button class="arv-btn arv-btn--sm" title="{{closeBtnText}}"
                (click)="closeBtnClick()">{{closeBtnText}}</button>
        </div>
      </div>
      <!-- ═══ TOOLBAR ════════════════════════════════════════════════ -->
      <div class="arv-toolbar arv-no-print">

        <!-- <span class="arv-toolbar__brand">📄 Report Viewer</span> -->

        <!-- Navigation -->
        <div class="arv-nav">
          <button class="arv-btn arv-btn--icon" title="First" [disabled]="currentPage <= 1"
                  (click)="navigateTo(1)">⏮</button>
          <button class="arv-btn arv-btn--icon" title="Previous" [disabled]="currentPage <= 1"
                  (click)="navigateTo(currentPage - 1)">◀</button>

          <input class="arv-page-input" type="number"
                 [min]="1" [max]="api.pageCount"
                 [ngModel]="currentPage"
                 (change)="onPageInputChange($event)" />
          <span class="arv-nav__of">/ {{ api.pageCount }}</span>

          <button class="arv-btn arv-btn--icon" title="Next"
                  [disabled]="currentPage >= api.pageCount"
                  (click)="navigateTo(currentPage + 1)">▶</button>
          <button class="arv-btn arv-btn--icon" title="Last"
                  [disabled]="currentPage >= api.pageCount"
                  (click)="navigateTo(api.pageCount)">⏭</button>
        </div>

        <!-- Zoom -->
        <div class="arv-zoom">
          <button class="arv-btn arv-btn--icon" title="Zoom out" (click)="zoomOut()">−</button>
          <span class="arv-zoom__val">{{ zoomPercent }}%</span>
          <button class="arv-btn arv-btn--icon" title="Zoom in"  (click)="zoomIn()">+</button>
          <button class="arv-btn arv-btn--sm" (click)="zoomReset()">1:1</button>
        </div>

        <!-- Refresh -->
        <button class="arv-btn arv-btn--sm" title="Refresh" (click)="refresh()">🔄</button>

        <!-- Print -->
        <div class="arv-btn-group">
          <button class="arv-btn arv-btn--sm" title="Print all pages" (click)="printAll()">🖨 Print All</button>
          <button class="arv-btn arv-btn--sm" title="Print current page" (click)="printCurrent()">🖨 Page</button>
        </div>

        <!-- Download page as image -->
        <button class="arv-btn arv-btn--sm" title="Download current page as image"
                (click)="downloadPageImage()">⬇ Image</button>

        <!-- Status -->
        <div class="arv-status">
          @if (api.loading$ | async) {
            <span class="arv-spinner-sm"></span> Rendering…
          } @else if (api.pageCount > 0) {
            <span class="arv-status__ok">✓ {{ api.pageCount }} pages</span>
          }
        </div>
      </div>

      <!-- ═══ BODY ════════════════════════════════════════════════════ -->
      <div class="arv-body">

        <!-- Left panel -->
        <arv-left-panel
          class="arv-no-print"
          [currentPage]="currentPage"
          (pageSelected)="navigateTo($event)">
        </arv-left-panel>

        <!-- Virtual page list -->
        <div class="arv-canvas-scroll"
             #scrollEl
             (scroll)="onScroll()">

          @if (api.loading$ | async) {
            <div class="arv-loading">
              <div class="arv-spinner"></div>
              <div>Rendering report…</div>
            </div>
          }

          @else if ((api.session$ | async) === null) {
            <div class="arv-empty">
              No report loaded.
            </div>
          }

          @else {
            <div class="arv-page-list"
                 [style.width.px]="pageListWidth">

              @for (p of pageNumbers; track p) {
                <div class="arv-page-wrap"
                     [id]="'arv-pw-' + p"
                     [style.height.px]="pageWrapHeight">

                  <div class="arv-page-num-label arv-no-print">{{ p }}</div>

                  @if (isInWindow(p)) {
                    <!-- Rendered page -->
                    <arv-page-renderer
                      #pageRenderers
                      [page]="getPage(p)"
                      [scale]="scale"
                      [attr.data-page]="p">
                    </arv-page-renderer>
                  } @else {
                    <!-- Placeholder (correct height to keep scrollbar accurate) -->
                    <div class="arv-placeholder"
                         [style.width.px]="pageWidthPx"
                         [style.height.px]="pageHeightPx">
                      Page {{ p }}
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      </div>
    </div>

    <!-- ═══ PRINT STYLES ═══════════════════════════════════════════════ -->
    <style id="arv-print-css">
      @media print {
        .arv-no-print { display: none !important; }
        .arv-shell { height: auto !important; }
        .arv-body  { overflow: visible !important; }
        .arv-canvas-scroll { overflow: visible !important; height: auto !important; }
        .arv-page-wrap { page-break-after: always; }
        .arv-page-wrap:last-child { page-break-after: avoid; }
      }
    </style>
  `,
  styles: [`
    :host { display: block; height: 100%; }

    .arv-header-of-arview{
      padding: 0.25rem;
      font-size: 12px;
      display: flex;
      .arv-header__title{
        flex-grow: 1;
        text-align: left;
      }
      .arv-header__btn-group{
        flex-grow: 0;
      }
    }

    

    /* ── Dark theme (default) ──────────────────────────────────── */
    .arv-shell,
    .arv-shell[data-theme="dark"] {
      --arv-bg:          #1e1e2e;
      --arv-surface:     #2a2a3e;
      --arv-surface2:    #313148;
      --arv-border:      #3d3d5c;
      --arv-accent:      #5b8ef0;
      --arv-text:        #abb2bf;
      --arv-text-strong: #e0e0f0;
      --arv-text-muted:  #6b7280;
    }

    /* ── Light theme ──────────────────────────────────────────── */
    .arv-shell[data-theme="light"] {
      --arv-bg:          #f0f0f5;
      --arv-surface:     #ffffff;
      --arv-surface2:    #f5f5fa;
      --arv-border:      #d0d0e0;
      --arv-accent:      #3b6fd4;
      --arv-text:        #333344;
      --arv-text-strong: #111122;
      --arv-text-muted:  #888899;
    }

    /* Shell */
    .arv-shell {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--arv-bg);
      color: var(--arv-text);
      font-family: system-ui, -apple-system, sans-serif;
      font-size: 12px;
    }

    /* Toolbar */
    .arv-toolbar {
      display: flex; align-items: center;
      gap: 8px; padding: 0 12px; height: 44px;
      flex-shrink: 0;
      background: var(--arv-surface);
      border-bottom: 1px solid var(--arv-border);
      flex-wrap: wrap;
    }
    .arv-toolbar__brand {
      font-weight: 700; font-size: 13px;
      color: var(--arv-text-strong); margin-right: 8px;
    }

    /* Navigation */
    .arv-nav { display: flex; align-items: center; gap: 4px; }
    .arv-nav__of { font-size: 11px; color: var(--arv-text-muted); }
    .arv-page-input {
      width: 52px; text-align: center; padding: 2px 4px;
      background: var(--arv-surface); border: 1px solid var(--arv-border);
      border-radius: 4px; color: var(--arv-text-strong); font-size: 12px;
    }

    /* Zoom */
    .arv-zoom { display: flex; align-items: center; gap: 4px; }
    .arv-zoom__val { min-width: 42px; text-align: center; font-size: 11px; color: var(--arv-text-muted); }

    /* Buttons */
    .arv-btn {
      display: inline-flex; align-items: center; gap: 3px;
      padding: 3px 8px;
      background: var(--arv-surface2);
      border: 1px solid var(--arv-border);
      border-radius: 4px;
      color: var(--arv-text);
      font-size: 11px; cursor: pointer; white-space: nowrap;
    }
    .arv-btn:hover { background: var(--arv-border); color: var(--arv-text-strong); }
    .arv-btn:disabled { opacity: 0.4; pointer-events: none; }
    .arv-btn--icon { padding: 3px 6px; font-size: 13px; }
    .arv-btn--sm   { padding: 2px 7px; }
    .arv-btn-group { display: flex; gap: 1px; }
    .arv-btn-group .arv-btn:first-child { border-radius: 4px 0 0 4px; }
    .arv-btn-group .arv-btn:last-child  { border-radius: 0 4px 4px 0; }

    /* Status */
    .arv-status { margin-left: auto; display: flex; align-items: center; gap: 6px; font-size: 11px; color: var(--arv-text-muted); }
    .arv-status__ok { color: #4caf50; }

    /* Body */
    .arv-body { display: flex; flex: 1; overflow: hidden; min-height: 0; }

    /* Canvas scroll */
    .arv-canvas-scroll {
      flex: 1; overflow: auto;
      background: var(--arv-bg);
      padding: 20px;
      display: flex; flex-direction: column; align-items: center;
    }

    /* Page list */
    .arv-page-list { display: flex; flex-direction: column; align-items: center; }
    .arv-page-wrap {
      position: relative;
      display: flex; flex-direction: column; align-items: center;
      padding-bottom: 16px;
    }
    .arv-page-num-label { font-size: 10px; color: var(--arv-text-muted); margin-bottom: 4px; align-self: flex-start; }

    /* Page rendered box */
    arv-page-renderer { display: block; box-shadow: 0 2px 12px rgba(0,0,0,0.3); }

    /* Placeholder for pages outside render window */
    .arv-placeholder {
      background: var(--arv-surface);
      border: 1px dashed var(--arv-border);
      display: flex; align-items: center; justify-content: center;
      color: var(--arv-text-muted); font-size: 11px;
    }

    /* Loading / empty */
    .arv-loading { display: flex; flex-direction: column; align-items: center; gap: 12px; padding: 60px; color: var(--arv-text-muted); justify-content: center;
  height: 100%; }
    .arv-empty   { padding: 60px; text-align: center; color: var(--arv-text-muted); }
    .arv-spinner {
      width: 32px; height: 32px;
      border: 3px solid var(--arv-border);
      border-top-color: var(--arv-accent);
      border-radius: 50%; animation: arv-spin 0.8s linear infinite;
    }
    .arv-spinner-sm {
      display: inline-block; width: 14px; height: 14px;
      border: 2px solid var(--arv-border);
      border-top-color: var(--arv-accent);
      border-radius: 50%; animation: arv-spin 0.7s linear infinite;
    }
    @keyframes arv-spin { to { transform: rotate(360deg); } }
  `]
})
export class ReportViewerComponent implements OnInit, OnDestroy, AfterViewInit {

  /** API base URL, e.g. 'http://localhost:5000' */
  @Input() set apiBaseUrl(url: string) {
    if (url) this.api.configure(url);
  }

  @Input() reportTitle: string = 'Report Viewer';

  @Input() closeBtnText: string = 'Close';

  /** Light or dark theme */
  @Input() theme: 'light' | 'dark' = 'dark';

  /** If provided, render this report on init */
  @Input() renderRequest?: { reportXml: string; data?: any; parameters?: any };

  @Output()
  scrolled = new EventEmitter<number>();

  @Output()
  closeBtn = new EventEmitter<void>();

  @ViewChild('scrollEl') scrollEl!: ElementRef<HTMLDivElement>;
  @ViewChildren('pageRenderers') pageRenderers!: QueryList<PageRendererComponent>;

  currentPage = 1;
  scale       = PT_TO_PX;
  pageCache   = new Map<number, PageResponse>();

  private destroy$  = new Subject<void>();
  private printPage?: number;  // undefined = all, number = single page

  constructor(
    public api: ReportViewerApiService,
    private cdr: ChangeDetectorRef
  ) {}

  // ── Lifecycle ──────────────────────────────────────────────────────────────

  ngOnInit(): void {
    // Subscribe to external navigation commands
    this.api.navigateTo$
      .pipe(takeUntil(this.destroy$))
      .subscribe(p => this.navigateTo(p));

    // Auto-render if input provided
    if (this.renderRequest) {
      this.api.render(this.renderRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => { this.currentPage = 1; this.cdr.markForCheck(); });
    }
  }

  closeBtnClick(){
    this.closeBtn.emit();
  }

  ngAfterViewInit(): void {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Page numbers ───────────────────────────────────────────────────────────

  get pageNumbers(): number[] {
    const count = this.api.pageCount;
    return count > 0 ? Array.from({ length: count }, (_, i) => i + 1) : [];
  }

  get pageWidthPx():  number { return this.api.pageWidthPt  * this.scale; }
  get pageHeightPx(): number { return this.api.pageHeightPt * this.scale; }
  get pageWrapHeight():  number { return this.pageHeightPx + 32; }
  get pageListWidth():   number { return this.pageWidthPx + 40; }
  get zoomPercent():     number { return Math.round(this.scale / PT_TO_PX * 100); }

  isInWindow(p: number): boolean {
    return Math.abs(p - this.currentPage) <= WINDOW_RADIUS;
  }

  getPage(p: number): PageResponse | undefined {
    if (!this.pageCache.has(p)) {
      // Trigger async load
      this.loadPage(p);
      return undefined;
    }
    return this.pageCache.get(p);
  }

  private loadPage(p: number): void {
    if (this.pageCache.has(p)) return;
    this.api.getPage(p)
      .pipe(takeUntil(this.destroy$))
      .subscribe(page => {
        this.pageCache.set(p, page);
        this.cdr.markForCheck();
      });
  }

  // Preload window pages whenever currentPage changes
  private preloadWindow(): void {
    const from = Math.max(1, this.currentPage - WINDOW_RADIUS);
    const to   = Math.min(this.api.pageCount, this.currentPage + WINDOW_RADIUS);
    for (let p = from; p <= to; p++) this.loadPage(p);
  }

  // ── Navigation ─────────────────────────────────────────────────────────────

  navigateTo(page: number): void {
    this.currentPage = Math.max(1, Math.min(page, this.api.pageCount));
    this.preloadWindow();
    this.cdr.markForCheck();
    setTimeout(() => this.scrollToPage(this.currentPage), 50);
  }

  onPageInputChange(e: Event): void {
    const v = parseInt((e.target as HTMLInputElement).value, 10);
    if (!isNaN(v)) this.navigateTo(v);
  }

  private scrollToPage(p: number): void {
    const el = document.getElementById(`arv-pw-${p}`);
    const scroll = this.scrollEl?.nativeElement;
    if (el && scroll) {
      scroll.scrollTo({ top: el.offsetTop - 8, behavior: 'smooth' });
    }
  }

  onScroll(): void {
    const scroll = this.scrollEl?.nativeElement;
    if (!scroll) return;
    const list = scroll.querySelector('.arv-page-list') as HTMLElement;
    if (!list) return;
    const mid = scroll.scrollTop + scroll.clientHeight / 2;
    let best = 1, bestDist = Infinity;
    for (const child of Array.from(list.children) as HTMLElement[]) {
      if (!child.id?.startsWith('arv-pw-')) continue;
      const p    = parseInt(child.id.replace('arv-pw-', ''), 10);
      const dist = Math.abs(child.offsetTop + child.offsetHeight / 2 - mid);
      if (dist < bestDist) { bestDist = dist; best = p; }
    }
    if (best !== this.currentPage) {
      this.currentPage = best;
      this.preloadWindow();
      this.cdr.markForCheck();
    }
  }

  // ── Zoom ───────────────────────────────────────────────────────────────────

  zoomIn():    void { this.scale = Math.min(this.scale * 1.2, PT_TO_PX * 4); this.cdr.markForCheck(); }
  zoomOut():   void { this.scale = Math.max(this.scale / 1.2, PT_TO_PX * 0.25); this.cdr.markForCheck(); }
  zoomReset(): void { this.scale = PT_TO_PX; this.cdr.markForCheck(); }

  // ── Refresh ────────────────────────────────────────────────────────────────

  refresh(): void {
    this.pageCache.clear();
    this.api.clearCache();
    this.preloadWindow();
    this.cdr.markForCheck();
  }

  // ── Print ──────────────────────────────────────────────────────────────────

  printAll(): void {
    this.ensureAllPagesLoaded(() => window.print());
  }

  printCurrent(): void {
    // Add temporary class to hide all other pages
    const list = document.querySelector('.arv-page-list');
    if (!list) { window.print(); return; }
    Array.from(list.querySelectorAll('.arv-page-wrap')).forEach((el: Element) => {
      const pageEl = el as HTMLElement;
      const id = pageEl.id?.replace('arv-pw-', '');
      pageEl.style.display = id === String(this.currentPage) ? '' : 'none';
    });
    window.print();
    // Restore
    Array.from(list.querySelectorAll('.arv-page-wrap'))
      .forEach((el: Element) => ((el as HTMLElement).style.display = ''));
  }

  private ensureAllPagesLoaded(callback: () => void): void {
    // Pre-load remaining pages then print
    const missing: number[] = [];
    for (let p = 1; p <= this.api.pageCount; p++) {
      if (!this.pageCache.has(p)) missing.push(p);
    }
    if (missing.length === 0) { callback(); return; }

    let loaded = 0;
    for (const p of missing) {
      this.api.getPage(p).pipe(takeUntil(this.destroy$)).subscribe(page => {
        this.pageCache.set(p, page);
        loaded++;
        this.cdr.markForCheck();
        if (loaded === missing.length) callback();
      });
    }
  }

  // ── Download page as image ─────────────────────────────────────────────────

  async downloadPageImage(): Promise<void> {
    // Find the current page's renderer
    const renderer = this.pageRenderers.find(r => {
      const el = r.getPageElement();
      return el?.getAttribute('data-page') === String(this.currentPage);
    });
    const pageEl = renderer?.getPageElement();
    if (!pageEl) { alert('Page not rendered yet. Please wait.'); return; }

    try {
      // Use html2canvas if available, otherwise canvas screenshot
      const h2c = (window as any).html2canvas;
      if (h2c) {
        const canvas = await h2c(pageEl, { scale: 2, useCORS: true });
        this.downloadCanvas(canvas, `page-${this.currentPage}.png`);
      } else {
        // Fallback: DOM-to-canvas via browser print screenshot prompt
        alert('html2canvas not loaded. Add <script src="https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js"></script> to your index.html for image download.');
      }
    } catch (e) {
      console.error('Screenshot failed', e);
    }
  }

  private downloadCanvas(canvas: HTMLCanvasElement, filename: string): void {
    const a    = document.createElement('a');
    a.href     = canvas.toDataURL('image/png');
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }
}
