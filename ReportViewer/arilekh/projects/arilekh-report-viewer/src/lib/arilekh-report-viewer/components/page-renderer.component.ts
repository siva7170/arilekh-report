import {
  Component, Input, OnChanges, SimpleChanges,
  ChangeDetectionStrategy, ElementRef, ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ElementDto, PageResponse, StyleDto } from '../services/report-viewer-api.service';

const PT_TO_PX = 1.333; // 1pt = 1.333px at 96dpi

@Component({
  selector: 'arv-page-renderer',
  standalone: true,
  imports: [CommonModule],
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

  /** Expose the page div for screenshot (called by parent). */
  getPageElement(): HTMLDivElement | undefined {
    return this.pageEl?.nativeElement;
  }
}
