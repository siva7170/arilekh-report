import {
  Component, Input, Output, EventEmitter,
  OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ReportViewerApiService, ThumbnailListResponse, PageSummary, SearchHit
} from '../services/report-viewer-api.service';
import { Subject } from 'rxjs';
import { takeUntil, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';

@Component({
  selector: 'arv-left-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="arv-left">

      <!-- Header -->
      <div class="arv-left__header">
        <span class="arv-left__title">Pages</span>
        <span class="arv-left__badge">{{ api.pageCount }}</span>
      </div>

      <!-- Search -->
      <div class="arv-left__search">
        <input class="arv-input"
               placeholder="Search in report…"
               [(ngModel)]="searchQuery"
               (ngModelChange)="onSearchChange($event)" />
        @if (searching) {
          <span class="arv-spinner-sm"></span>
        }
      </div>

      <!-- Search results -->
      @if (searchResults.length > 0) {
        <div class="arv-left__section-title">
          {{ searchResults.length }} result(s) for "{{ lastQuery }}"
        </div>
        <div class="arv-left__list">
          @for (hit of searchResults; track hit.pageNumber) {
            <div class="arv-bm arv-bm--search"
                 [class.arv-bm--active]="currentPage === hit.pageNumber"
                 (click)="goTo(hit.pageNumber)">
              <span class="arv-bm__icon">🔍</span>
              <div class="arv-bm__col">
                <span class="arv-bm__label">Page {{ hit.pageNumber }}</span>
                <span class="arv-bm__snippet">{{ hit.snippet }}</span>
              </div>
            </div>
          }
        </div>
      }

      <!-- Page thumbnails -->
      @if (searchResults.length === 0) {
        <div class="arv-left__section-title">All Pages</div>
        <div class="arv-left__list">
          @if (thumbnails) {
            @for (pg of thumbnails.pages; track pg.pageNumber) {
              <div class="arv-bm arv-thumb"
                   [class.arv-bm--active]="currentPage === pg.pageNumber"
                   (click)="goTo(pg.pageNumber)">
                <!-- Mini thumbnail placeholder -->
                <div class="arv-thumb__box">
                  <span class="arv-thumb__num">{{ pg.pageNumber }}</span>
                </div>
                <div class="arv-bm__col">
                  <span class="arv-bm__label">Page {{ pg.pageNumber }}</span>
                  @if (pg.firstText) {
                    <span class="arv-bm__snippet">{{ pg.firstText | slice:0:40 }}</span>
                  }
                </div>
              </div>
            }
          } @else {
            @if (api.loading$ | async) {
              <div class="arv-left__loading">Loading…</div>
            }
            @else if ((api.session$ | async) === null){
              <div class="arv-left__loading">No report loaded...</div>
            }
            @else {
              <div class="arv-left__loading">Error loading report...</div>
            }
          }
        </div>
      }

      <!-- Footer info -->
      <div class="arv-left__footer">
        <span>{{ api.pageCount }} pages</span>
        @if (api.pageCount > 0) {
          <span>· Page {{ currentPage }}</span>
        }
      </div>
    </div>
  `,
  styles: [`
    /* Left panel inherits --arv-* from parent .arv-shell */
    .arv-left {
      width: 200px; flex-shrink: 0;
      display: flex; flex-direction: column;
      background: var(--arv-surface, #2a2a3e);
      border-right: 1px solid var(--arv-border, #3d3d5c);
      overflow: hidden; height: 100%;
    }
    .arv-left__header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 8px 10px 4px;
      font-weight: 700; font-size: 11px;
      color: var(--arv-text-muted, #6b7280);
      text-transform: uppercase; letter-spacing: 0.5px; flex-shrink: 0;
    }
    .arv-left__badge {
      font-size: 9px; background: var(--arv-border, #3d3d5c);
      border-radius: 8px; padding: 1px 6px; color: var(--arv-text, #ccc);
    }
    .arv-left__search {
      display: flex; align-items: center; gap: 4px;
      padding: 4px 8px; flex-shrink: 0;
    }
    .arv-input {
      flex: 1; padding: 4px 8px;
      background: var(--arv-surface2, #313148);
      border: 1px solid var(--arv-border, #3d3d5c);
      border-radius: 4px; color: var(--arv-text, #ccc);
      font-size: 11px; outline: none;
      width: 100%; box-sizing: border-box;
    }
    .arv-left__section-title {
      font-size: 9px; color: var(--arv-text-muted, #888);
      text-transform: uppercase; padding: 4px 10px 2px; flex-shrink: 0;
    }
    .arv-left__list { flex: 1; overflow-y: auto; min-height: 0; }
    .arv-left__loading { padding: 12px; font-size: 11px; color: var(--arv-text-muted, #888); }
    .arv-left__footer {
      padding: 6px 10px; font-size: 10px;
      color: var(--arv-text-muted, #888);
      border-top: 1px solid var(--arv-border, #3d3d5c);
      flex-shrink: 0; display: flex; gap: 4px;
    }
    .arv-bm {
      display: flex; align-items: flex-start; gap: 6px;
      padding: 5px 10px; cursor: pointer;
      font-size: 11px; border-radius: 2px; margin: 1px 4px;
    }
    .arv-bm:hover { background: var(--arv-surface2, #313148); }
    .arv-bm--active {
      background: rgba(91,142,240,0.15);
      color: var(--arv-accent, #5b8ef0);
    }
    .arv-bm__col { display: flex; flex-direction: column; gap: 2px; overflow: hidden; }
    .arv-bm__label { font-size: 11px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .arv-bm__snippet { font-size: 9px; color: var(--arv-text-muted, #888); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .arv-bm__icon { flex-shrink: 0; font-size: 12px; }
    .arv-thumb__box {
      width: 30px; height: 40px; background: white;
      border: 1px solid var(--arv-border, #3d3d5c);
      border-radius: 2px;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }
    .arv-thumb__num { font-size: 8px; color: #999; }
    .arv-spinner-sm {
      width: 14px; height: 14px;
      border: 2px solid var(--arv-border, #3d3d5c);
      border-top-color: var(--arv-accent, #5b8ef0);
      border-radius: 50%; animation: arv-spin 0.7s linear infinite; flex-shrink: 0;
    }
    @keyframes arv-spin { to { transform: rotate(360deg); } }
  `]
})
export class LeftPanelComponent implements OnInit, OnDestroy {
  @Input() currentPage = 1;
  @Output() pageSelected = new EventEmitter<number>();

  thumbnails?: ThumbnailListResponse;
  searchQuery  = '';
  lastQuery    = '';
  searching    = false;
  searchResults: SearchHit[] = [];

  private destroy$ = new Subject<void>();
  private search$  = new Subject<string>();

  constructor(
    public api: ReportViewerApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Load thumbnails when session is ready
    this.api.session$
      .pipe(takeUntil(this.destroy$))
      .subscribe(s => {
        if (s) this.loadThumbnails();
      });

    // Debounced search
    this.search$.pipe(
      takeUntil(this.destroy$),
      debounceTime(400),
      distinctUntilChanged(),
      switchMap(q => {
        if (!q.trim()) {
          this.searchResults = [];
          this.searching = false;
          this.cdr.markForCheck();
          return [];
        }
        this.searching = true;
        this.cdr.markForCheck();
        return this.api.search(q);
      })
    ).subscribe({
      next: r => {
        this.searchResults = r.results;
        this.lastQuery     = r.query;
        this.searching     = false;
        this.cdr.markForCheck();
      },
      error: () => { this.searching = false; this.cdr.markForCheck(); }
    });

    this.api.empty$
        .pipe(takeUntil(this.destroy$))
        .subscribe((emp)=>{
          if(emp) this.empty();
        });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadThumbnails(): void {
    this.api.getThumbnails()
      .pipe(takeUntil(this.destroy$))
      .subscribe(t => {
        this.thumbnails = t;
        this.cdr.markForCheck();
      });
  }

  onSearchChange(q: string): void {
    this.search$.next(q);
  }

  empty(){
    this.thumbnails = undefined;
    this.searchQuery  = '';
    this.lastQuery    = '';
    this.searching    = false;
    this.searchResults = [];
    this.cdr.markForCheck();
  }

  goTo(page: number): void {
    this.pageSelected.emit(page);
  }
}
