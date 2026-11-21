# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\02-trail-map.ui.spec.ts >> UI · robustness of the trail view (own page — this test crashes the console) >> TC-37 an out-of-range signal value does not crash the console
- Location: tests\ui\02-trail-map.ui.spec.ts:154:7

# Error details

```
Error: [BUG-15] console must not go blank

expect(received).toBe(expected) // Object.is equality

Expected: false
Received: true
```

# Test source

```ts
  62  |    * TC-33 · FR-07
  63  |    * WHAT : the trail table lists the points oldest → newest.
  64  |    * URL  : UI {BASE_URL}  select "Cohen, David" → trail table   (data: GET {BASE_URL}api/offenders/1/trail)
  65  |    */
  66  |   test('TC-33 trail table is ordered oldest to newest', { tag: ['@TC-33', '@FR-07', '@ui'] }, async () => {
  67  |     knownBug('BUG-06');
  68  |     const shownIds = (await screen.trail.readTrailRows()).map((r) => r.id);
  69  | 
  70  |     expect(shownIds, bugMessage('BUG-06', 'table must list points chronologically')).toEqual(chronological(trail).map((p) => p.id));
  71  |   });
  72  | 
  73  |   /**
  74  |    * TC-34 · FR-07
  75  |    * WHAT : the trail line follows the real movement: it starts at the OLDEST point's marker and
  76  |    *        ends at the NEWEST point's marker (read from the rendered SVG geometry).
  77  |    * URL  : UI {BASE_URL}  select "Cohen, David" → Leaflet polyline + markers
  78  |    */
  79  |   test('TC-34 map line starts at the oldest point and ends at the newest', { tag: ['@TC-34', '@FR-07', '@ui'] }, async () => {
  80  |     knownBug('BUG-06');
  81  |     const centres = await screen.map.markerCentres(); // DOM order = API order
  82  |     const line = await screen.map.lineVertices();
  83  |     const markerOf = (point: TrailPoint) => centres[trail.findIndex((p) => p.id === point.id)];
  84  | 
  85  |     const lineStartsAt = trail[nearestIndex(line[0], centres)];
  86  |     const lineEndsAt = trail[nearestIndex(line[line.length - 1], centres)];
  87  | 
  88  |     expect(distance(line[0], markerOf(lineStartsAt)), 'line start must sit on a marker').toBeLessThan(3);
  89  |     expect({ start: lineStartsAt.id, end: lineEndsAt.id }, bugMessage('BUG-06', 'line must run from the oldest to the newest point')).toEqual({
  90  |       start: oldestPoint(trail).id,
  91  |       end: newestPoint(trail).id,
  92  |     });
  93  |   });
  94  | 
  95  |   /**
  96  |    * TC-36 · FR-08
  97  |    * WHAT : clicking a map point shows that point's own timestamp, speed (km/h), battery (%) and
  98  |    *        signal (1–5) — compared field by field with the API for the first, a middle and the
  99  |    *        last marker.
  100 |    * URL  : UI {BASE_URL}  select "Cohen, David" → click marker → popup   vs   GET {BASE_URL}api/offenders/1/trail
  101 |    */
  102 |   test('TC-36 clicking a map point shows its recorded data', { tag: ['@TC-36', '@FR-08', '@ui'] }, async () => {
  103 |     for (const index of [0, Math.floor(trail.length / 2), trail.length - 1]) {
  104 |       const popup = await screen.map.openPopup(index); // closes the popup again → screen unchanged
  105 |       const point = trail.find((p) => p.id === popup.pointId);
  106 |       expect(point, `popup point #${popup.pointId} must exist in the API trail`).toBeDefined();
  107 |       expect(popup).toEqual({
  108 |         pointId: point!.id,
  109 |         time: instantToDdMmYyyyHhMm(point!.timestamp, environment.timezone),
  110 |         speedKmh: point!.speedKmh,
  111 |         batteryPct: point!.batteryPct,
  112 |         signal: point!.signal,
  113 |       });
  114 |     }
  115 |   });
  116 | 
  117 |   /**
  118 |    * TC-38 · FR-09
  119 |    * WHAT : the Latest reading card shows the speed, battery and signal of the MOST RECENT point.
  120 |    * URL  : UI {BASE_URL}  select "Cohen, David" → "Latest reading"   vs   newest point of GET {BASE_URL}api/offenders/1/trail
  121 |    */
  122 |   test('TC-38 latest reading shows the most recent point', { tag: ['@TC-38', '@FR-09', '@ui'] }, async () => {
  123 |     knownBug('BUG-05');
  124 |     const newest = newestPoint(trail);
  125 | 
  126 |     const latest = await screen.trail.latestReading();
  127 | 
  128 |     expect(
  129 |       { speedKmh: latest.speedKmh, batteryPct: latest.batteryPct, signal: latest.signal },
  130 |       bugMessage('BUG-05', `readings must come from the newest point #${newest.id} (${newest.timestamp})`),
  131 |     ).toEqual({ speedKmh: newest.speedKmh, batteryPct: newest.batteryPct, signal: newest.signal });
  132 |   });
  133 | 
  134 |   /**
  135 |    * TC-39 · FR-09, FR-06
  136 |    * WHAT : "Last seen" shows the timestamp of the most recent point (DD/MM/YYYY, HH:mm, operator
  137 |    *        timezone).
  138 |    * URL  : UI {BASE_URL}  select "Cohen, David" → "Last seen"   vs   newest point of GET {BASE_URL}api/offenders/1/trail
  139 |    */
  140 |   test('TC-39 last seen shows the most recent timestamp', { tag: ['@TC-39', '@FR-09', '@ui'] }, async () => {
  141 |     const latest = await screen.trail.latestReading();
  142 | 
  143 |     expect(latest.lastSeen).toBe(instantToDdMmYyyyHhMm(newestPoint(trail).timestamp, environment.timezone));
  144 |   });
  145 | });
  146 | 
  147 | test.describe('UI · robustness of the trail view (own page — this test crashes the console)', () => {
  148 |   /**
  149 |    * TC-37 · FR-08 (negative)
  150 |    * WHAT : a stored point with an out-of-range signal (6) must not break the console: selecting
  151 |    *        the offender still renders the trail and raises no uncaught page error.
  152 |    * URL  : POST {BASE_URL}api/offenders/{id}/locations {signal: 6}  →  UI {BASE_URL} select offender
  153 |    */
  154 |   test('TC-37 an out-of-range signal value does not crash the console', { tag: ['@TC-37', '@FR-08', '@ui', '@negative'] }, async ({ consolePage, testData }) => {
  155 |     knownBug('BUG-15');
  156 |     const offender = await testData.createOffenderWithTrail([validLocation({ signal: 6 })]);
  157 |     await consolePage.open();
  158 | 
  159 |     await consolePage.openOffender(offender);
  160 |     await consolePage.waitForTrailOrCrash();
  161 | 
> 162 |     expect(await consolePage.isBlank(), bugMessage('BUG-15', 'console must not go blank')).toBe(false);
      |                                                                                            ^ Error: [BUG-15] console must not go blank
  163 |     expect(consolePage.pageErrors, bugMessage('BUG-15', 'no uncaught errors')).toEqual([]);
  164 |   });
  165 | });
  166 | 
```