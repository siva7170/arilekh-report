import {
  Component, Input, OnChanges, SimpleChanges,
  ChangeDetectionStrategy, ElementRef, ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ElementDto, PageResponse, StyleDto, ChartSeriesDto } from '../services/report-viewer-api.service';
import { SafeHtmlPipe } from '../pipes/safe-html.pipe';


const PT_TO_PX = 1.333; // 1pt = 1.333px at 96dpi

@Component({
  selector: 'arv-page-renderer',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="arv-page"
         #pageEl
         [style.width.px]="widthPx"
         [style.height.px]="heightPx"
         [attr.data-page]="page?.pageNumber">

      @if (page) {
        @for (el of page.elements; track $index) {
          <!-- TEXT -->
          @if (el.type === 'text') {
            <div [style]="textStyle(el)"
                 [class.arv-hyperlink]="!!el.hyperlinkUrl"
                 (click)="el.hyperlinkUrl && openLink(el.hyperlinkUrl)">
              {{ el.text }}
            </div>
          }
          <!-- RECT -->
          @else if (el.type === 'rect') {
            <div [style]="rectStyle(el)"></div>
          }
          <!-- LINE -->
          @else if (el.type === 'line') {
            <svg [style]="lineSvgStyle(el)" xmlns="http://www.w3.org/2000/svg">
              <line
                [attr.x1]="lineX1(el)" [attr.y1]="lineY1(el)"
                [attr.x2]="lineX2(el)" [attr.y2]="lineY2(el)"
                [attr.stroke]="el.strokeColor || '#000'"
                [attr.stroke-width]="(el.strokeWidth || 1) * scale" />
            </svg>
          }
          <!-- ELLIPSE -->
          @else if (el.type === 'ellipse') {
            <svg [style]="ellipseSvgStyle(el)" xmlns="http://www.w3.org/2000/svg">
              <ellipse
                [attr.cx]="(el.width  * scale / 2)"
                [attr.cy]="(el.height * scale / 2)"
                [attr.rx]="Math.max(0, el.width  * scale / 2 - (el.strokeWidth||1)*scale/2)"
                [attr.ry]="Math.max(0, el.height * scale / 2 - (el.strokeWidth||1)*scale/2)"
                [attr.fill]="el.fillColor || 'none'"
                [attr.stroke]="el.strokeColor || '#000'"
                [attr.stroke-width]="(el.strokeWidth || 1) * scale" />
            </svg>
          }
          <!-- IMAGE -->
          @else if (el.type === 'image') {
            <img [style]="imageStyle(el)"
                 [src]="el.src"
                 alt="" />
          }
          <!-- CHART -->
          @else if (el.type === 'chart') {
            <div [style]="chartWrapStyle(el)">
              <svg [attr.width]="el.width * scale" [attr.height]="el.height * scale"
                   xmlns="http://www.w3.org/2000/svg"
                   [innerHTML]="buildChartSvgContent(el) | safeHtml">
              </svg>
            </div>
          }
        }
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .arv-page {
      position: relative;
      background: white;
      overflow: hidden;
      box-shadow: 0 2px 12px rgba(0,0,0,0.3);
    }
    .arv-hyperlink { cursor: pointer; }
    .arv-hyperlink:hover { opacity: 0.8; }
    svg { overflow: visible; }
  `]
})
export class PageRendererComponent implements OnChanges {
  @Input() page?: PageResponse;
  @Input() scale: number = PT_TO_PX;
  @ViewChild('pageEl') pageEl?: ElementRef<HTMLDivElement>;

  protected Math = Math;

  get widthPx():  number { return (this.page?.widthPt  ?? 595) * this.scale; }
  get heightPx(): number { return (this.page?.heightPt ?? 842) * this.scale; }

  ngOnChanges(c: SimpleChanges): void {}

  // ── Style builders ────────────────────────────────────────────────────────

  textStyle(el: ElementDto): Record<string, string> {
    const s = el.style;
    const rot = el.rotation ? `rotate(${el.rotation}deg)` : '';
    const vAlign = el.verticalAlign === 'middle' ? 'center'
                 : el.verticalAlign === 'bottom' ? 'flex-end'
                 : 'flex-start';
    return {
      position:         'absolute',
      left:             `${el.x * this.scale}px`,
      top:              `${el.y * this.scale}px`,
      width:            `${el.width  * this.scale}px`,
      height:           `${el.height * this.scale}px`,
      overflow:         'hidden',
      fontSize:         `${(s?.fontSize ?? 9) * this.scale}px`,
      fontFamily:       s?.fontFamily ?? 'inherit',
      fontWeight:       s?.bold    ? 'bold'   : 'normal',
      fontStyle:        s?.italic  ? 'italic' : 'normal',
      textDecoration:   s?.underline ? 'underline' : 'none',
      color:            s?.foreColor  ?? '#000',
      background:       s?.backColor  ?? 'transparent',
      textAlign:        el.alignment  ?? 'left',
      display:          'flex',
      flexDirection:    'column',
      justifyContent:   vAlign,
      paddingLeft:      `${(s?.paddingLeft  ?? 0) * this.scale}px`,
      paddingRight:     `${(s?.paddingRight ?? 0) * this.scale}px`,
      transform:        rot,
      transformOrigin:  rot ? 'center center' : '',
      boxSizing:        'border-box',
    };
  }

  rectStyle(el: ElementDto): Record<string, string> {
    const rot = el.rotation ? `rotate(${el.rotation}deg)` : '';
    return {
      position:        'absolute',
      left:            `${el.x * this.scale}px`,
      top:             `${el.y * this.scale}px`,
      width:           `${el.width  * this.scale}px`,
      height:          `${el.height * this.scale}px`,
      background:      el.fillColor    ?? 'transparent',
      border:          el.strokeColor
                       ? `${(el.strokeWidth ?? 1) * this.scale}px solid ${el.strokeColor}`
                       : 'none',
      borderRadius:    el.borderRadius ? `${el.borderRadius}%` : '0',
      transform:       rot,
      transformOrigin: rot ? 'center center' : '',
      boxSizing:       'border-box',
    };
  }

  lineSvgStyle(el: ElementDto): Record<string, string> {
    const x1 = el.x,  y1 = el.y;
    const x2 = el.x2 ?? el.x, y2 = el.y2 ?? el.y;
    const left   = Math.min(x1, x2) * this.scale;
    const top    = Math.min(y1, y2) * this.scale;
    const width  = Math.max(Math.abs(x2 - x1) * this.scale, (el.strokeWidth ?? 1) * this.scale + 2);
    const height = Math.max(Math.abs(y2 - y1) * this.scale, (el.strokeWidth ?? 1) * this.scale + 2);
    const rot    = el.rotation ? `rotate(${el.rotation}deg)` : '';
    return {
      position: 'absolute',
      left: `${left}px`, top: `${top}px`,
      width: `${width}px`, height: `${height}px`,
      transform: rot, transformOrigin: rot ? 'center center' : '',
    };
  }

  lineX1(el: ElementDto): number {
    return (el.x  * this.scale) - Math.min(el.x, el.x2 ?? el.x) * this.scale;
  }
  lineY1(el: ElementDto): number {
    return (el.y  * this.scale) - Math.min(el.y, el.y2 ?? el.y) * this.scale;
  }
  lineX2(el: ElementDto): number {
    return ((el.x2 ?? el.x) * this.scale) - Math.min(el.x, el.x2 ?? el.x) * this.scale;
  }
  lineY2(el: ElementDto): number {
    return ((el.y2 ?? el.y) * this.scale) - Math.min(el.y, el.y2 ?? el.y) * this.scale;
  }

  ellipseSvgStyle(el: ElementDto): Record<string, string> {
    const rot = el.rotation ? `rotate(${el.rotation}deg)` : '';
    return {
      position: 'absolute',
      left: `${el.x * this.scale}px`, top: `${el.y * this.scale}px`,
      width: `${el.width * this.scale}px`, height: `${el.height * this.scale}px`,
      transform: rot, transformOrigin: rot ? 'center center' : '',
    };
  }

  imageStyle(el: ElementDto): Record<string, string> {
    const rot = el.rotation ? `rotate(${el.rotation}deg)` : '';
    return {
      position: 'absolute',
      left:     `${el.x * this.scale}px`,
      top:      `${el.y * this.scale}px`,
      width:    `${el.width  * this.scale}px`,
      height:   `${el.height * this.scale}px`,
      objectFit: (el.stretch as any) ?? 'contain',
      transform: rot, transformOrigin: rot ? 'center center' : '',
    };
  }

  openLink(url: string): void {
    window.open(url, '_blank', 'noopener');
  }


  // ── Chart rendering ───────────────────────────────────────────────────────

  chartWrapStyle(el: ElementDto): Record<string, string> {
    const rot = el.rotation ? `rotate(${el.rotation}deg)` : '';
    return {
      position: 'absolute',
      left: `${el.x * this.scale}px`, top: `${el.y * this.scale}px`,
      width: `${el.width * this.scale}px`, height: `${el.height * this.scale}px`,
      transform: rot, transformOrigin: rot ? 'center center' : '',
      overflow: 'hidden',
    };
  }

  buildChartSvgContent(el: ElementDto): string {
    const w   = el.width  * this.scale;
    const h   = el.height * this.scale;
    const pad = 8;
    const series  = el.series  ?? [];
    const cats    = el.categories ?? [];
    const colors  = ['#4472C4','#ED7D31','#A9D18E','#FF0000','#FFC000','#5B9BD5','#70AD47','#7030A0'];
    let svg = '';

    // Border / background
    if (el.backgroundColor)
      svg += `<rect x="0" y="0" width="${w}" height="${h}" fill="${el.backgroundColor}"/>`;
    if (el.showBorder)
      svg += `<rect x="0.5" y="0.5" width="${w-1}" height="${h-1}" fill="none" stroke="${el.borderColor||'#ccc'}" stroke-width="${el.borderWidth||1}"/>`;

    // Title
    let topY = pad;
    if (el.chartTitle) {
      svg += `<text x="${w/2}" y="${topY+10}" text-anchor="middle" font-size="10" font-weight="bold" fill="#333">${el.chartTitle}</text>`;
      topY += 16;
    }

    // Legend height
    const legH = (el.showLegend && series.length > 0) ? 14 : 0;
    const chartH = h - topY - pad - legH;
    const chartW = w - pad * 2;

    const type = el.chartType ?? 'bar';

    if (type === 'pie') {
      svg += this.buildPie(series, cats, colors, w/2, topY + chartH/2, Math.min(chartW, chartH)/2 - 4);
    } else if (type === 'bar' || type === 'barhorizontal') {
      svg += this.buildBar(series, cats, colors, pad, topY, chartW, chartH, type === 'barhorizontal');
    } else if (type === 'line') {
      svg += this.buildLine(series, cats, colors, pad, topY, chartW, chartH);
    }

    // Legend
    if (el.showLegend && series.length > 0) {
      let lx = pad;
      const ly = h - legH + 2;
      series.forEach((s, i) => {
        const col = s.color || colors[i % colors.length];
        svg += `<rect x="${lx}" y="${ly}" width="8" height="8" fill="${col}"/>`;
        svg += `<text x="${lx+10}" y="${ly+8}" font-size="8" fill="#333">${s.label}</text>`;
        lx += Math.max(35, s.label.length * 5 + 14);
        if (lx > w - 30) return;
      });
    }
    return svg;
  }

  private buildPie(series: ChartSeriesDto[], cats: string[], colors: string[],
                   cx: number, cy: number, r: number): string {
    const serCount = Math.max(1, series.length || cats.length);
    let svg = '';
    let angle = -Math.PI / 2;
    const step = (2 * Math.PI) / serCount;
    for (let i = 0; i < serCount; i++) {
      const s   = series[i];
      const col = s?.color || colors[i % colors.length];
      const a1  = angle, a2 = angle + step;
      const x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1);
      const x2 = cx + r * Math.cos(a2), y2 = cy + r * Math.sin(a2);
      const large = step > Math.PI ? 1 : 0;
      svg += `<path d="M${cx},${cy} L${x1.toFixed(1)},${y1.toFixed(1)} A${r},${r} 0 ${large} 1 ${x2.toFixed(1)},${y2.toFixed(1)} Z" fill="${col}" stroke="white" stroke-width="1"/>`;
      angle = a2;
    }
    return svg;
  }

  private buildBar(series: ChartSeriesDto[], cats: string[], colors: string[],
                   ox: number, oy: number, cw: number, ch: number, horiz: boolean): string {
    const count  = Math.max(1, series.length);
    const allVals = series.flatMap(s => s.values ?? [0.4, 0.7, 0.5]).slice(0, Math.max(cats.length, 3));
    const maxVal  = Math.max(...allVals, 1);
    let svg = '';
    // Draw baseline
    if (horiz) svg += `<line x1="${ox}" y1="${oy}" x2="${ox}" y2="${oy+ch}" stroke="#ccc" stroke-width="0.5"/>`;
    else        svg += `<line x1="${ox}" y1="${oy+ch}" x2="${ox+cw}" y2="${oy+ch}" stroke="#ccc" stroke-width="0.5"/>`;

    const groupW = horiz ? (ch / count) : (cw / count);
    const barW   = Math.max(4, groupW * 0.7);
    series.forEach((s, si) => {
      const col  = s.color || colors[si % colors.length];
      const vals = s.values?.length ? s.values : [0.4 + si*0.15, 0.6 + si*0.1, 0.5];
      vals.slice(0, Math.max(cats.length || 3, 1)).forEach((v, vi) => {
        const frac = v / maxVal;
        if (horiz) {
          const by = oy + si * groupW + (groupW - barW)/2;
          const bw = cw * frac;
          svg += `<rect x="${ox}" y="${by.toFixed(1)}" width="${bw.toFixed(1)}" height="${barW.toFixed(1)}" fill="${col}"/>`;
        } else {
          const bx = ox + vi * (cw / Math.max(vals.length, 1));
          const bh = ch * frac;
          svg += `<rect x="${bx.toFixed(1)}" y="${(oy+ch-bh).toFixed(1)}" width="${(barW/series.length).toFixed(1)}" height="${bh.toFixed(1)}" fill="${col}"/>`;
        }
      });
    });
    return svg;
  }

  private buildLine(series: ChartSeriesDto[], cats: string[], colors: string[],
                    ox: number, oy: number, cw: number, ch: number): string {
    let svg = `<line x1="${ox}" y1="${oy+ch}" x2="${ox+cw}" y2="${oy+ch}" stroke="#ccc" stroke-width="0.5"/>`;
    const ptCount = Math.max(cats.length, 3);
    series.forEach((s, si) => {
      const col  = s.color || colors[si % colors.length];
      const vals = s.values?.length ? s.values : Array.from({length: ptCount}, (_, i) => 0.3 + ((i + si) % 5) * 0.15);
      const maxV = Math.max(...vals, 1);
      const pts  = vals.map((v, i) => {
        const x = ox + (i / Math.max(vals.length - 1, 1)) * cw;
        const y = oy + ch - (v / maxV) * ch;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      }).join(' ');
      svg += `<polyline points="${pts}" fill="none" stroke="${col}" stroke-width="2"/>`;
      vals.forEach((v, i) => {
        const x = ox + (i / Math.max(vals.length - 1, 1)) * cw;
        const y = oy + ch - (v / maxV) * ch;
        svg += `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="2.5" fill="${col}"/>`;
      });
    });
    return svg;
  }

  /** Expose the page div for screenshot (called by parent). */
  getPageElement(): HTMLDivElement | undefined {
    return this.pageEl?.nativeElement;
  }
}