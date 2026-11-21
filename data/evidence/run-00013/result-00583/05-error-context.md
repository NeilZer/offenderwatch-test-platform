# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\02-trail-map.ui.spec.ts >> UI · robustness of the trail view (own page — this test crashes the console) >> TC-37 an out-of-range signal value does not crash the console
- Location: tests\ui\02-trail-map.ui.spec.ts:160:7

# Error details

```
Error: [BUG-15] console must not go blank

expect(received).toBe(expected) // Object.is equality

Expected: false
Received: true
```

# Test source

```ts
  68  |    * TC-33 · FR-07
  69  |    * WHAT : the trail table lists the points oldest → newest.
  70  |    * URL  : UI {BASE_URL}  select "Cohen, David" → trail table   (data: GET {BASE_URL}api/offenders/1/trail)
  71  |    */
  72  |   test('TC-33 trail table is ordered oldest to newest', { tag: ['@TC-33', '@FR-07', '@ui'] }, async () => {
  73  |     knownBug('BUG-06');
  74  |     const shownIds = (await screen.trail.readTrailRows()).map((r) => r.id);
  75  | 
  76  |     expect(shownIds, bugMessage('BUG-06', 'table must list points chronologically')).toEqual(chronological(trail).map((p) => p.id));
  77  |   });
  78  | 
  79  |   /**
  80  |    * TC-34 · FR-07
  81  |    * WHAT : the trail line follows the real movement: it starts at the OLDEST point's marker and
  82  |    *        ends at the NEWEST point's marker (read from the rendered SVG geometry).
  83  |    * URL  : UI {BASE_URL}  select "Cohen, David" → Leaflet polyline + markers
  84  |    */
  85  |   test('TC-34 map line starts at the oldest point and ends at the newest', { tag: ['@TC-34', '@FR-07', '@ui'] }, async () => {
  86  |     knownBug('BUG-06');
  87  |     const centres = await screen.map.markerCentres(); // DOM order = API order
  88  |     const line = await screen.map.lineVertices();
  89  |     const markerOf = (point: TrailPoint) => centres[trail.findIndex((p) => p.id === point.id)];
  90  | 
  91  |     const lineStartsAt = trail[nearestIndex(line[0], centres)];
  92  |     const lineEndsAt = trail[nearestIndex(line[line.length - 1], centres)];
  93  | 
  94  |     expect(distance(line[0], markerOf(lineStartsAt)), 'line start must sit on a marker').toBeLessThan(3);
  95  |     expect({ start: lineStartsAt.id, end: lineEndsAt.id }, bugMessage('BUG-06', 'line must run from the oldest to the newest point')).toEqual({
  96  |       start: oldestPoint(trail).id,
  97  |       end: newestPoint(trail).id,
  98  |     });
  99  |   });
  100 | 
  101 |   /**
  102 |    * TC-36 · FR-08
  103 |    * WHAT : clicking a map point shows that point's own timestamp, speed (km/h), battery (%) and
  104 |    *        signal (1–5) — compared field by field with the API for the first, a middle and the
  105 |    *        last marker.
  106 |    * URL  : UI {BASE_URL}  select "Cohen, David" → click marker → popup   vs   GET {BASE_URL}api/offenders/1/trail
  107 |    */
  108 |   test('TC-36 clicking a map point shows its recorded data', { tag: ['@TC-36', '@FR-08', '@ui'] }, async () => {
  109 |     for (const index of [0, Math.floor(trail.length / 2), trail.length - 1]) {
  110 |       const popup = await screen.map.openPopup(index); // closes the popup again → screen unchanged
  111 |       const point = trail.find((p) => p.id === popup.pointId);
  112 |       expect(point, `popup point #${popup.pointId} must exist in the API trail`).toBeDefined();
  113 |       expect(popup).toEqual({
  114 |         pointId: point!.id,
  115 |         time: instantToDdMmYyyyHhMm(point!.timestamp, environment.timezone),
  116 |         speedKmh: point!.speedKmh,
  117 |         batteryPct: point!.batteryPct,
  118 |         signal: point!.signal,
  119 |       });
  120 |     }
  121 |   });
  122 | 
  123 |   /**
  124 |    * TC-38 · FR-09
  125 |    * WHAT : the Latest reading card shows the speed, battery and signal of the MOST RECENT point.
  126 |    * URL  : UI {BASE_URL}  select "Cohen, David" → "Latest reading"   vs   newest point of GET {BASE_URL}api/offenders/1/trail
  127 |    */
  128 |   test('TC-38 latest reading shows the most recent point', { tag: ['@TC-38', '@FR-09', '@ui'] }, async () => {
  129 |     knownBug('BUG-05');
  130 |     const newest = newestPoint(trail);
  131 | 
  132 |     const latest = await screen.trail.latestReading();
  133 | 
  134 |     expect(
  135 |       { speedKmh: latest.speedKmh, batteryPct: latest.batteryPct, signal: latest.signal },
  136 |       bugMessage('BUG-05', `readings must come from the newest point #${newest.id} (${newest.timestamp})`),
  137 |     ).toEqual({ speedKmh: newest.speedKmh, batteryPct: newest.batteryPct, signal: newest.signal });
  138 |   });
  139 | 
  140 |   /**
  141 |    * TC-39 · FR-09, FR-06
  142 |    * WHAT : "Last seen" shows the timestamp of the most recent point (DD/MM/YYYY, HH:mm, operator
  143 |    *        timezone).
  144 |    * URL  : UI {BASE_URL}  select "Cohen, David" → "Last seen"   vs   newest point of GET {BASE_URL}api/offenders/1/trail
  145 |    */
  146 |   test('TC-39 last seen shows the most recent timestamp', { tag: ['@TC-39', '@FR-09', '@ui'] }, async () => {
  147 |     const latest = await screen.trail.latestReading();
  148 | 
  149 |     expect(latest.lastSeen).toBe(instantToDdMmYyyyHhMm(newestPoint(trail).timestamp, 'UTC')); // TEMPORARY: simulated regression for the Part 5 history demo
  150 |   });
  151 | });
  152 | 
  153 | test.describe('UI · robustness of the trail view (own page — this test crashes the console)', () => {
  154 |   /**
  155 |    * TC-37 · FR-08 (negative)
  156 |    * WHAT : a stored point with an out-of-range signal (6) must not break the console: selecting
  157 |    *        the offender still renders the trail and raises no uncaught page error.
  158 |    * URL  : POST {BASE_URL}api/offenders/{id}/locations {signal: 6}  →  UI {BASE_URL} select offender
  159 |    */
  160 |   test('TC-37 an out-of-range signal value does not crash the console', { tag: ['@TC-37', '@FR-08', '@ui', '@negative'] }, async ({ consolePage, testData }) => {
  161 |     knownBug('BUG-15');
  162 |     const offender = await testData.createOffenderWithTrail([validLocation({ signal: 6 })]);
  163 |     await consolePage.open();
  164 | 
  165 |     await consolePage.openOffender(offender);
  166 |     await consolePage.waitForTrailOrCrash();
  167 | 
> 168 |     expect(await consolePage.isBlank(), bugMessage('BUG-15', 'console must not go blank')).toBe(false);
      |                                                                                            ^ Error: [BUG-15] console must not go blank
  169 |     expect(consolePage.pageErrors, bugMessage('BUG-15', 'no uncaught errors')).toEqual([]);
  170 |   });
  171 | });
  172 | 
```