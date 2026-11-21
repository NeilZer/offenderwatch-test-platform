# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\02-trail-map.ui.spec.ts >> UI · robustness of the trail view (own page — this test crashes the console) >> TC-37 an out-of-range signal value does not crash the console
- Location: tests\ui\02-trail-map.ui.spec.ts:155:7

# Error details

```
Error: [BUG-15] console must not go blank

expect(received).toBe(expected) // Object.is equality

Expected: false
Received: true
```

# Test source

```ts
  63  |    * TC-33 · FR-07
  64  |    * WHAT : the trail table lists the points oldest → newest.
  65  |    * URL  : UI {BASE_URL}  select "Cohen, David" → trail table   (data: GET {BASE_URL}api/offenders/1/trail)
  66  |    */
  67  |   test('TC-33 trail table is ordered oldest to newest', { tag: ['@TC-33', '@FR-07', '@ui'] }, async () => {
  68  |     knownBug('BUG-06');
  69  |     const shownIds = (await screen.trail.readTrailRows()).map((r) => r.id);
  70  | 
  71  |     expect(shownIds, bugMessage('BUG-06', 'table must list points chronologically')).toEqual(chronological(trail).map((p) => p.id));
  72  |   });
  73  | 
  74  |   /**
  75  |    * TC-34 · FR-07
  76  |    * WHAT : the trail line follows the real movement: it starts at the OLDEST point's marker and
  77  |    *        ends at the NEWEST point's marker (read from the rendered SVG geometry).
  78  |    * URL  : UI {BASE_URL}  select "Cohen, David" → Leaflet polyline + markers
  79  |    */
  80  |   test('TC-34 map line starts at the oldest point and ends at the newest', { tag: ['@TC-34', '@FR-07', '@ui'] }, async () => {
  81  |     knownBug('BUG-06');
  82  |     const centres = await screen.map.markerCentres(); // DOM order = API order
  83  |     const line = await screen.map.lineVertices();
  84  |     const markerOf = (point: TrailPoint) => centres[trail.findIndex((p) => p.id === point.id)];
  85  | 
  86  |     const lineStartsAt = trail[nearestIndex(line[0], centres)];
  87  |     const lineEndsAt = trail[nearestIndex(line[line.length - 1], centres)];
  88  | 
  89  |     expect(distance(line[0], markerOf(lineStartsAt)), 'line start must sit on a marker').toBeLessThan(3);
  90  |     expect({ start: lineStartsAt.id, end: lineEndsAt.id }, bugMessage('BUG-06', 'line must run from the oldest to the newest point')).toEqual({
  91  |       start: oldestPoint(trail).id,
  92  |       end: newestPoint(trail).id,
  93  |     });
  94  |   });
  95  | 
  96  |   /**
  97  |    * TC-36 · FR-08
  98  |    * WHAT : clicking a map point shows that point's own timestamp, speed (km/h), battery (%) and
  99  |    *        signal (1–5) — compared field by field with the API for the first, a middle and the
  100 |    *        last marker.
  101 |    * URL  : UI {BASE_URL}  select "Cohen, David" → click marker → popup   vs   GET {BASE_URL}api/offenders/1/trail
  102 |    */
  103 |   test('TC-36 clicking a map point shows its recorded data', { tag: ['@TC-36', '@FR-08', '@ui'] }, async () => {
  104 |     for (const index of [0, Math.floor(trail.length / 2), trail.length - 1]) {
  105 |       const popup = await screen.map.openPopup(index); // closes the popup again → screen unchanged
  106 |       const point = trail.find((p) => p.id === popup.pointId);
  107 |       expect(point, `popup point #${popup.pointId} must exist in the API trail`).toBeDefined();
  108 |       expect(popup).toEqual({
  109 |         pointId: point!.id,
  110 |         time: instantToDdMmYyyyHhMm(point!.timestamp, environment.timezone),
  111 |         speedKmh: point!.speedKmh,
  112 |         batteryPct: point!.batteryPct,
  113 |         signal: point!.signal,
  114 |       });
  115 |     }
  116 |   });
  117 | 
  118 |   /**
  119 |    * TC-38 · FR-09
  120 |    * WHAT : the Latest reading card shows the speed, battery and signal of the MOST RECENT point.
  121 |    * URL  : UI {BASE_URL}  select "Cohen, David" → "Latest reading"   vs   newest point of GET {BASE_URL}api/offenders/1/trail
  122 |    */
  123 |   test('TC-38 latest reading shows the most recent point', { tag: ['@TC-38', '@FR-09', '@ui'] }, async () => {
  124 |     knownBug('BUG-05');
  125 |     const newest = newestPoint(trail);
  126 | 
  127 |     const latest = await screen.trail.latestReading();
  128 | 
  129 |     expect(
  130 |       { speedKmh: latest.speedKmh, batteryPct: latest.batteryPct, signal: latest.signal },
  131 |       bugMessage('BUG-05', `readings must come from the newest point #${newest.id} (${newest.timestamp})`),
  132 |     ).toEqual({ speedKmh: newest.speedKmh, batteryPct: newest.batteryPct, signal: newest.signal });
  133 |   });
  134 | 
  135 |   /**
  136 |    * TC-39 · FR-09, FR-06
  137 |    * WHAT : "Last seen" shows the timestamp of the most recent point (DD/MM/YYYY, HH:mm, operator
  138 |    *        timezone).
  139 |    * URL  : UI {BASE_URL}  select "Cohen, David" → "Last seen"   vs   newest point of GET {BASE_URL}api/offenders/1/trail
  140 |    */
  141 |   test('TC-39 last seen shows the most recent timestamp', { tag: ['@TC-39', '@FR-09', '@ui'] }, async () => {
  142 |     const latest = await screen.trail.latestReading();
  143 | 
  144 |     expect(latest.lastSeen).toBe(instantToDdMmYyyyHhMm(newestPoint(trail).timestamp, environment.timezone));
  145 |   });
  146 | });
  147 | 
  148 | test.describe('UI · robustness of the trail view (own page — this test crashes the console)', () => {
  149 |   /**
  150 |    * TC-37 · FR-08 (negative)
  151 |    * WHAT : a stored point with an out-of-range signal (6) must not break the console: selecting
  152 |    *        the offender still renders the trail and raises no uncaught page error.
  153 |    * URL  : POST {BASE_URL}api/offenders/{id}/locations {signal: 6}  →  UI {BASE_URL} select offender
  154 |    */
  155 |   test('TC-37 an out-of-range signal value does not crash the console', { tag: ['@TC-37', '@FR-08', '@ui', '@negative'] }, async ({ consolePage, testData }) => {
  156 |     knownBug('BUG-15');
  157 |     const offender = await testData.createOffenderWithTrail([validLocation({ signal: 6 })]);
  158 |     await consolePage.open();
  159 | 
  160 |     await consolePage.openOffender(offender);
  161 |     await consolePage.waitForTrailOrCrash();
  162 | 
> 163 |     expect(await consolePage.isBlank(), bugMessage('BUG-15', 'console must not go blank')).toBe(false);
      |                                                                                            ^ Error: [BUG-15] console must not go blank
  164 |     expect(consolePage.pageErrors, bugMessage('BUG-15', 'no uncaught errors')).toEqual([]);
  165 |   });
  166 | });
  167 | 
```