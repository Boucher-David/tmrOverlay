import { afterEach, describe, expect, it } from 'vitest';
import { freshLiveSnapshot, renderBrowserOverlay } from './browserOverlayTestHost.js';

let currentOverlay;

afterEach(() => {
  currentOverlay?.close();
  currentOverlay = null;
});

describe('relative browser rendering', () => {
  it('renders nearest configured cars around the reference row with stable gap signs', async () => {
    currentOverlay = await renderBrowserOverlay('relative', {
      live: freshLiveSnapshot({}),
      model: relativeDisplayModel()
    });

    const rows = [...currentOverlay.document.querySelectorAll('tbody tr')];
    const rowText = rows.map(rowCells);

    expect(rowText).toEqual([
      '3 #34 Near Ahead -2.350',
      '5 #55 Focus Driver 0.000',
      '6 #61 Near Behind +1.200 IN'
    ]);
    expect(rows[1].classList.contains('focus')).toBe(true);
    expect(currentOverlay.document.getElementById('status').textContent).toBe('5 - 2/4 cars');
    expect(currentOverlay.document.body.textContent).not.toContain('Far Ahead');
    expect(currentOverlay.document.body.textContent).not.toContain('Far Behind');
  });
});

function relativeDisplayModel() {
  return {
    overlayId: 'relative',
    title: 'Relative',
    status: '5 - 2/4 cars',
    source: 'source: live proximity telemetry',
    bodyKind: 'table',
    columns: [
      { id: 'relative.position', label: 'Pos', dataKey: 'relative-position', width: 38, alignment: 'right' },
      { id: 'relative.driver', label: 'Driver', dataKey: 'driver', width: 180, alignment: 'left' },
      { id: 'relative.gap', label: 'Gap', dataKey: 'gap', width: 70, alignment: 'right' },
      { id: 'relative.pit', label: 'Pit', dataKey: 'pit', width: 30, alignment: 'right' }
    ],
    rows: [
      row(['3', '#34 Near Ahead', '-2.350']),
      row(['5', '#55 Focus Driver', '0.000'], { isReference: true }),
      row(['6', '#61 Near Behind', '+1.200', 'IN'], { isPit: true })
    ],
    metrics: []
  };
}

function rowCells(row) {
  return [...row.querySelectorAll('td')].map((cell) => cell.textContent.trim()).join(' ').trim();
}

function row(cells, extra = {}) {
  return {
    cells,
    isReference: false,
    isClassHeader: false,
    isPit: false,
    isPartial: false,
    carClassColorHex: null,
    headerTitle: null,
    headerDetail: null,
    ...extra
  };
}
