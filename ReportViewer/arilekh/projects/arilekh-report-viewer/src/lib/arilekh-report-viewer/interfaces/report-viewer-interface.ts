import { Observable } from "rxjs";
import { PageResponse, RenderRequest, RenderResponse, SearchResponse, ThumbnailListResponse } from "../services/report-viewer-api.service";
import { HttpParams } from "@angular/common/http";

export interface IReportViewer {

    render(req: RenderRequest): Observable<RenderResponse>;

    getPage(sessionId: string, pageNumber: number): Observable<PageResponse>;

    getThumbnails(sessionId: string): Observable<ThumbnailListResponse>;

    search(sessionId: string, params: HttpParams): Observable<SearchResponse>;

    loadSession(sessionId: string): Observable<RenderResponse>;

    renderServer(req: {
                    reportXml: string;
                    dataProviderKey: string;
                    parameters?: Record<string, unknown>;
                    ttlMinutes?: number;
    }): Observable<RenderResponse>;
}