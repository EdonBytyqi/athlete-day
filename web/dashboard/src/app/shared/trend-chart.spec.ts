import { LoadTrendPoint } from '../api/models';
import { PADDING, chartGeometry, niceMax } from './trend-chart';

const point = (date: string, dailyLoad: number, acute7: number, chronic28: number): LoadTrendPoint => ({
  date,
  dailyLoad,
  acute7,
  chronic28,
});

describe('trend chart geometry', () => {
  it('[AC-16] rounds the axis up to multiples of 250 AU', () => {
    expect(niceMax(0)).toBe(250);
    expect(niceMax(251)).toBe(500);
    expect(niceMax(1000)).toBe(1000);
  });

  it('[AC-16] draws one bar per day, scaled to the axis maximum', () => {
    const g = chartGeometry([point('d1', 500, 250, 250), point('d2', 0, 250, 250)], 244, 134);
    // inner area: 244-44-12 = 188 wide, 134-12-22 = 100 high; max = 500
    expect(g.maxY).toBe(500);
    expect(g.bars).toHaveLength(2);
    expect(g.bars[0].height).toBe(100);
    expect(g.bars[0].y).toBe(PADDING.top);
    expect(g.bars[1].height).toBe(0);
  });

  it('[AC-16] puts line points at the bar centres', () => {
    const g = chartGeometry([point('d1', 0, 500, 250), point('d2', 0, 500, 250)], 244, 134);
    // step = 94; centres at 44+47 = 91 and 44+141 = 185; acute 500 → top, chronic 250 → middle
    expect(g.acutePath).toBe('M91,12 L185,12');
    expect(g.chronicPath).toBe('M91,62 L185,62');
  });

  it('[AC-16] copes with an empty series', () => {
    const g = chartGeometry([], 720, 240);
    expect(g.bars).toEqual([]);
    expect(g.acutePath).toBe('');
    expect(g.maxY).toBe(250);
  });
});
