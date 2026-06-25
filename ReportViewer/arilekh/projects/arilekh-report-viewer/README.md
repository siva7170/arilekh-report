# Arilekh Report Viewer

Angular report viewer component for the Arilekh Reporting Platform.

## Installation

```bash
npm install arilekh-report-viewer
```

## Usage

```typescript
import { ReportViewerComponent } from 'arilekh-report-viewer';

...

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ReportViewerComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements IReportViewer {

  baseUrl: string = 'http://localhost:2943';

  constructor(private reportApiService: ReportViewerApiService, private http: HttpClient) { }
    
  render(req: RenderRequest): Observable<RenderResponse> {
    return this.http.post<RenderResponse>(`${this.baseUrl}/api/reports/render`, req)
  }
  getPage(sessionId: string, pageNumber: number): Observable<PageResponse> {
    return this.http.get<PageResponse>(`${this.baseUrl}/api/reports/${sessionId}/pages/${pageNumber}`)
  }
  getThumbnails(sessionId: string): Observable<ThumbnailListResponse> {
    return this.http.get<ThumbnailListResponse>(`${this.baseUrl}/api/reports/${sessionId}/thumbnails`);
  }
  search(sessionId:string, params: HttpParams): Observable<SearchResponse> {
    return this.http.get<SearchResponse>(`${this.baseUrl}/api/reports/${sessionId}/search`, { params });
  }
  loadSession(sessionId: string): Observable<RenderResponse> {
    console.log('>>>',this.http);
    return this.http.get<RenderResponse>(`${this.baseUrl}/api/reports/${sessionId}/info`);
  }
  renderServer(req: { reportXml: string; dataProviderKey: string; parameters?: Record<string, unknown>; ttlMinutes?: number; }): Observable<RenderResponse> {
    return this.http.post<RenderResponse>(`${this.baseUrl}/api/reports/render-server`, req);
  }

  ngOnInit(): void {
    this.reportApiService.setProvider(this);
    this.reportApiService.setLoading(true);

    this.http.post(`${this.baseUrl}/api/reports/generate-sales`,{ Req: 'Test' }).subscribe({        
      next: (v:any) => {
        this.reportApiService.loadSession(v.sessionId).subscribe({
            next: res => {
                console.log('session loaded', res);
            },
            error: err => {
                console.error(err);
            }
        });;
        console.log('resolve', v, v.sessionId);
      },
      error: (e) => {
        console.log('error', e);
      }
    });
  }
}
```

## Features

- Report viewing
- Zoom
- Pagination
- Export support

## API Endpoints

In arilekh-report repository, please find `ArilekhReport.WebApi` project for your reference. You can modify the API endpoints as you wish. But, Arilekh Report Viewer expects/passes some parameters from/to API endpoints. Please modify accordingly.

## Updates

Document will be upated soon