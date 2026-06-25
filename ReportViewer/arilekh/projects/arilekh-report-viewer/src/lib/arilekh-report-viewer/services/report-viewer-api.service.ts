import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, Subject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { IReportViewer } from '../interfaces/report-viewer-interface';

// ── DTOs matching the .NET API ─────────────────────────────────────────────

export interface RenderRequest {
  reportXml: string;
  data?: Record<string, Record<string, unknown>[]>;
  parameters?: Record<string, unknown>;
  ttlMinutes?: number;
}

export interface RenderResponse {
  sessionId: string;
  pageCount: number;
  pageWidthPt: number;
  pageHeightPt: number;
  renderedAt: string;
  renderMs: number;
}

export interface StyleDto {
  fontFamily?: string;
  fontSize?: number;
  bold: boolean;
  italic: boolean;
  underline: boolean;
  foreColor?: string;
  backColor?: string;
  paddingLeft: number;
  paddingRight: number;
}

export interface ElementDto {
  type: 'text' | 'rect' | 'line' | 'ellipse' | 'image';
  x: number; y: number;
  width: number; height: number;
  rotation: number;
  // text
  text?: string;
  alignment?: 'left' | 'center' | 'right';
  verticalAlign?: 'top' | 'middle' | 'bottom';
  hyperlinkUrl?: string;
  style?: StyleDto;
  // rect / ellipse
  fillColor?: string;
  strokeColor?: string;
  strokeWidth?: number;
  borderRadius?: number;
  // line
  x2?: number; y2?: number;
  // image
  src?: string;
  stretch?: string;
}

export interface PageResponse {
  pageNumber: number;
  totalPages: number;
  widthPt: number;
  heightPt: number;
  elements: ElementDto[];
}

export interface PageSummary {
  pageNumber: number;
  firstText: string;
  groupLabel: string;
}

export interface ThumbnailListResponse {
  sessionId: string;
  pageCount: number;
  pages: PageSummary[];
}

export interface SearchHit {
  pageNumber: number;
  snippet: string;
  x: number; y: number;
}

export interface SearchResponse {
  query: string;
  hits: number;
  results: SearchHit[];
}

// ── Service ────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ReportViewerApiService {
  private baseUrl = '';

  // ── Observable state ───────────────────────────────────────────────────────

  private _session  = new BehaviorSubject<RenderResponse | null>(null);
  private _loading  = new BehaviorSubject<boolean>(false);
  private _error    = new BehaviorSubject<string | null>(null);
  private _pageCache = new Map<number, PageResponse>();
  private _navigateTo$ = new Subject<number>();

  readonly session$    = this._session.asObservable();
  readonly loading$    = this._loading.asObservable();
  readonly error$      = this._error.asObservable();
  readonly navigateTo$ = this._navigateTo$.asObservable();

  private provider!: IReportViewer;

  setProvider(provider: IReportViewer) {
    this.provider = provider;
  }

  // ── Configuration ──────────────────────────────────────────────────────────

  configure(apiBaseUrl: string): void {
    this.baseUrl = apiBaseUrl.replace(/\/$/, '');
  }

  get sessionId(): string | null {
    return this._session.value?.sessionId ?? null;
  }

  get pageCount(): number {
    return this._session.value?.pageCount ?? 0;
  }

  get pageWidthPt(): number {
    return this._session.value?.pageWidthPt ?? 595;
  }

  get pageHeightPt(): number {
    return this._session.value?.pageHeightPt ?? 842;
  }

  setLoading(loading: boolean): void {
    this._loading.next(loading);
  }

  // ── Load a pre-rendered session (server generated the GUID) ─────────────────

  /**
   * Tell the viewer to use a session that was already rendered server-side.
   * Your Angular code calls this with the GUID returned by your API controller
   * (which called ReportSessionService.StoreDocument or render-server endpoint).
   *
   * Example:
   *   // In your form submit handler:
   *   myApi.generateReport(formData).subscribe(({ sessionId }) => {
   *     reportViewer.loadSession(sessionId);
   *   });
   */
  loadSession(sessionId: string): Observable<RenderResponse> {
    this._loading.next(true);
    this._error.next(null);
    this._pageCache.clear();
    this._session.next(null);

    // return this.http
    //   .get<RenderResponse>(`${this.baseUrl}/api/reports/${sessionId}/info`)
    //   .pipe(
    //     tap({
    //       next:  r => { this._session.next(r); this._loading.next(false); },
    //       error: e => {
    //         this._error.next(e?.error?.error ?? 'Session not found or expired');
    //         this._loading.next(false);
    //       }
    //     })
    //   );
    return this.provider.loadSession(sessionId)
      .pipe(
        tap({
          next:  r => { this._session.next(r); this._loading.next(false); },
          error: e => {
            this._error.next(e?.error?.error ?? 'Session not found or expired');
            this._loading.next(false);
          }
        })
      );
  }

  /**
   * Render server-side using a registered IServerDataProvider key.
   * The API resolves data from your database — Angular never sees the raw data.
   *
   * Example:
   *   reportViewer.renderServer({
   *     reportXml: this.rdxXml,
   *     dataProviderKey: 'SalesReport',
   *     parameters: { DateFrom: '2026-01-01', DateTo: '2026-12-31' }
   *   }).subscribe();
   */
  renderServer(req: {
    reportXml: string;
    dataProviderKey: string;
    parameters?: Record<string, unknown>;
    ttlMinutes?: number;
  }): Observable<RenderResponse> {
    this._loading.next(true);
    this._error.next(null);
    this._pageCache.clear();
    this._session.next(null);

    // return this.http
    //   .post<RenderResponse>(`${this.baseUrl}/api/reports/render-server`, req)
    //   .pipe(
    //     tap({
    //       next:  r => { this._session.next(r); this._loading.next(false); },
    //       error: e => {
    //         this._error.next(e?.error?.error ?? 'Server render failed');
    //         this._loading.next(false);
    //       }
    //     })
    //   );
    return this.provider.renderServer(req)
      .pipe(
        tap({
          next:  r => { this._session.next(r); this._loading.next(false); },
          error: e => {
            this._error.next(e?.error?.error ?? 'Server render failed');
            this._loading.next(false);
          }
        })
      );
  }

  // ── Render ────────────────────────────────────────────────────────────────

  constructor(private http: HttpClient) {}

  render(req: RenderRequest): Observable<RenderResponse> {
    this._loading.next(true);
    this._error.next(null);
    this._pageCache.clear();
    this._session.next(null);

    // return this.http
    //   .post<RenderResponse>(`${this.baseUrl}/api/reports/render`, req)
    //   .pipe(
    //     tap({
    //       next:  r => { this._session.next(r); this._loading.next(false); },
    //       error: e => {
    //         this._error.next(e?.error?.error ?? 'Render failed');
    //         this._loading.next(false);
    //       }
    //     })
    //   );
    return this.provider.render(req)
      .pipe(
        tap({
          next:  r => { this._session.next(r); this._loading.next(false); },
          error: e => {
            this._error.next(e?.error?.error ?? 'Render failed');
            this._loading.next(false);
          }
        })
      );
  }

  // ── Page retrieval (cached) ────────────────────────────────────────────────

  getPage(pageNumber: number): Observable<PageResponse> {
    const id = this.sessionId;
    if (!id) throw new Error('No active session.');

    // Check cache first
    const cached = this._pageCache.get(pageNumber);
    if (cached) {
      return new Observable(o => { o.next(cached); o.complete(); });
    }

    // return this.http
    //   .get<PageResponse>(`${this.baseUrl}/api/reports/${id}/pages/${pageNumber}`)
    //   .pipe(tap(p => this._pageCache.set(pageNumber, p)));

    return this.provider.getPage(id, pageNumber)
            .pipe(tap(p => this._pageCache.set(pageNumber, p)));
  }

  getPageRange(from: number, to: number): Observable<PageResponse[]> {
    const id = this.sessionId;
    if (!id) throw new Error('No active session.');
    const params = new HttpParams()
      .set('from', from.toString())
      .set('to',   to.toString());
    return this.http.get<PageResponse[]>(`${this.baseUrl}/api/reports/${id}/pages`, { params });
  }

  // ── Thumbnails ────────────────────────────────────────────────────────────

  getThumbnails(): Observable<ThumbnailListResponse> {
    const id = this.sessionId!;
    //return this.http.get<ThumbnailListResponse>(`${this.baseUrl}/api/reports/${id}/thumbnails`);
    return this.provider.getThumbnails(id);
  }

  // ── Search ────────────────────────────────────────────────────────────────

  search(query: string): Observable<SearchResponse> {
    const id     = this.sessionId!;
    const params = new HttpParams().set('q', query);
    // return this.http.get<SearchResponse>(
    //   `${this.baseUrl}/api/reports/${id}/search`, { params });
    return this.provider.search(id, params);
  }

  // ── Navigation helper ─────────────────────────────────────────────────────

  navigateTo(page: number): void {
    this._navigateTo$.next(Math.max(1, Math.min(page, this.pageCount)));
  }

  // ── Cache management ──────────────────────────────────────────────────────

  evictPage(pageNumber: number): void {
    this._pageCache.delete(pageNumber);
  }

  clearCache(): void {
    this._pageCache.clear();
  }

  isPageCached(pageNumber: number): boolean {
    return this._pageCache.has(pageNumber);
  }
}
