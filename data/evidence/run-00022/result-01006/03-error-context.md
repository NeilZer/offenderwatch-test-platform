# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\02-trail-map.ui.spec.ts >> UI · trail, map & latest reading (one page, Cohen selected once) >> TC-33 trail table is ordered oldest to newest
- Location: tests\ui\02-trail-map.ui.spec.ts:66:7

# Error details

```
Error: [BUG-06] table must list points chronologically

expect(received).toEqual(expected) // deep equality

- Expected  - 11
+ Received  + 11

  Array [
-   7,
-   11,
-   4,
+   1,
    2,
-   15,
-   9,
+   3,
+   4,
+   5,
    6,
-   12,
-   16,
+   7,
+   8,
+   9,
    10,
+   11,
+   12,
    13,
-   5,
-   3,
    14,
-   8,
-   1,
+   15,
+   16,
    179,
    180,
  ]
```

# Page snapshot

```yaml
- generic [ref=e3]:
  - banner [ref=e4]:
    - generic [ref=e5]:
      - generic [ref=e6]: 📍
      - heading "OffenderWatch" [level=1] [ref=e7]
      - generic [ref=e8]: Monitoring Console
    - generic [ref=e9]:
      - generic [ref=e10]:
        - generic [ref=e11]: "38"
        - text: Offenders
      - generic [ref=e12]:
        - generic [ref=e13]: "18"
        - text: Active
      - generic [ref=e14]:
        - generic [ref=e15]: "1145"
        - text: Trail points
  - generic [ref=e16]:
    - generic [ref=e17]:
      - generic [ref=e18]:
        - textbox "Search name or national ID…" [ref=e19]: "305412876"
        - button "+ Add Offender" [ref=e20] [cursor=pointer]
      - table [ref=e21]:
        - rowgroup [ref=e22]:
          - row "Name National ID Date of birth Risk Status" [ref=e23]:
            - columnheader "Name" [ref=e24]
            - columnheader "National ID" [ref=e25]
            - columnheader "Date of birth" [ref=e26]
            - columnheader "Risk" [ref=e27]
            - columnheader "Status" [ref=e28]
            - columnheader [ref=e29]
        - rowgroup [ref=e30]:
          - row "Cohen, David 305412876 01/09/1987 Low Active Edit Delete" [ref=e31] [cursor=pointer]:
            - cell "Cohen, David" [ref=e32]
            - cell "305412876" [ref=e33]
            - cell "01/09/1987" [ref=e34]
            - cell "Low" [ref=e35]:
              - generic [ref=e36]: Low
            - cell "Active" [ref=e37]:
              - generic [ref=e38]: Active
            - cell "Edit Delete" [ref=e39]:
              - button "Edit" [ref=e40]
              - button "Delete" [ref=e41]
          - row "Colman, Dup 305412876 05/04/1990 Low Active Edit Delete" [ref=e42] [cursor=pointer]:
            - cell "Colman, Dup" [ref=e43]
            - cell "305412876" [ref=e44]
            - cell "05/04/1990" [ref=e45]
            - cell "Low" [ref=e46]:
              - generic [ref=e47]: Low
            - cell "Active" [ref=e48]:
              - generic [ref=e49]: Active
            - cell "Edit Delete" [ref=e50]:
              - button "Edit" [ref=e51]
              - button "Delete" [ref=e52]
          - row "Duplicate, ApiTest 305412876 15/05/1995 Low Active Edit Delete" [ref=e53] [cursor=pointer]:
            - cell "Duplicate, ApiTest" [ref=e54]
            - cell "305412876" [ref=e55]
            - cell "15/05/1995" [ref=e56]
            - cell "Low" [ref=e57]:
              - generic [ref=e58]: Low
            - cell "Active" [ref=e59]:
              - generic [ref=e60]: Active
            - cell "Edit Delete" [ref=e61]:
              - button "Edit" [ref=e62]
              - button "Delete" [ref=e63]
          - row "sss, sss 305412876 01/00/1990 Low Active Edit Delete" [ref=e64] [cursor=pointer]:
            - cell "sss, sss" [ref=e65]
            - cell "305412876" [ref=e66]
            - cell "01/00/1990" [ref=e67]
            - cell "Low" [ref=e68]:
              - generic [ref=e69]: Low
            - cell "Active" [ref=e70]:
              - generic [ref=e71]: Active
            - cell "Edit Delete" [ref=e72]:
              - button "Edit" [ref=e73]
              - button "Delete" [ref=e74]
      - generic [ref=e75]:
        - button "‹ Prev" [disabled] [ref=e76]
        - generic [ref=e77]: Page 1 of 1 (4 results)
        - button "Next ›" [disabled] [ref=e78]
    - generic [ref=e80]:
      - generic [ref=e81]:
        - generic [ref=e82]:
          - heading "David Cohen" [level=2] [ref=e83]
          - generic [ref=e84]:
            - text: "National ID: 305412876 · DOB: 01/09/1987 ·"
            - generic [ref=e85]: Low
            - generic [ref=e86]: Active
        - generic [ref=e87]:
          - generic [ref=e88]: Latest reading
          - generic [ref=e89]:
            - generic [ref=e90]: 38.4 km/h
            - generic [ref=e91]:
              - generic [ref=e92]: 87%
              - text: battery
            - generic [ref=e93]:
              - generic [ref=e94]: 4/5
              - text: signal
          - generic [ref=e95]: "Last seen: 15/08/2026, 21:23"
      - generic [ref=e96]:
        - generic:
          - generic:
            - img
        - generic:
          - generic [ref=e116]:
            - button "Zoom in" [ref=e117] [cursor=pointer]: +
            - button "Zoom out" [ref=e118] [cursor=pointer]: −
          - generic [ref=e119]:
            - link "Leaflet" [ref=e120] [cursor=pointer]:
              - /url: https://leafletjs.com
              - img [ref=e121]
              - text: Leaflet
            - text: "| © OpenStreetMap contributors"
      - generic [ref=e125]:
        - heading "Trail points (18)" [level=3] [ref=e126]
        - button "+ Add location" [ref=e127] [cursor=pointer]
      - table [ref=e128]:
        - rowgroup [ref=e129]:
          - row "# Timestamp Latitude Longitude Speed Battery Signal" [ref=e130]:
            - columnheader "#" [ref=e131]
            - columnheader "Timestamp" [ref=e132]
            - columnheader "Latitude" [ref=e133]
            - columnheader "Longitude" [ref=e134]
            - columnheader "Speed" [ref=e135]
            - columnheader "Battery" [ref=e136]
            - columnheader "Signal" [ref=e137]
        - rowgroup [ref=e138]:
          - row "1 09/08/2026, 14:29 32.08412 34.77879 23 km/h 68% 1/5" [ref=e139]:
            - cell "1" [ref=e140]
            - cell "09/08/2026, 14:29" [ref=e141]
            - cell "32.08412" [ref=e142]
            - cell "34.77879" [ref=e143]
            - cell "23 km/h" [ref=e144]
            - cell "68%" [ref=e145]
            - cell "1/5" [ref=e146]
          - row "2 09/08/2026, 10:10 32.08640 34.78046 15.5 km/h 83% 1/5" [ref=e147]:
            - cell "2" [ref=e148]
            - cell "09/08/2026, 10:10" [ref=e149]
            - cell "32.08640" [ref=e150]
            - cell "34.78046" [ref=e151]
            - cell "15.5 km/h" [ref=e152]
            - cell "83%" [ref=e153]
            - cell "1/5" [ref=e154]
          - row "3 09/08/2026, 13:21 32.08448 34.77776 19.6 km/h 71% 5/5" [ref=e155]:
            - cell "3" [ref=e156]
            - cell "09/08/2026, 13:21" [ref=e157]
            - cell "32.08448" [ref=e158]
            - cell "34.77776" [ref=e159]
            - cell "19.6 km/h" [ref=e160]
            - cell "71%" [ref=e161]
            - cell "5/5" [ref=e162]
          - row "4 09/08/2026, 09:44 32.08436 34.78075 14.1 km/h 86% 5/5" [ref=e163]:
            - cell "4" [ref=e164]
            - cell "09/08/2026, 09:44" [ref=e165]
            - cell "32.08436" [ref=e166]
            - cell "34.78075" [ref=e167]
            - cell "14.1 km/h" [ref=e168]
            - cell "86%" [ref=e169]
            - cell "5/5" [ref=e170]
          - row "5 09/08/2026, 13:10 32.08732 34.77988 44.8 km/h 73% 3/5" [ref=e171]:
            - cell "5" [ref=e172]
            - cell "09/08/2026, 13:10" [ref=e173]
            - cell "32.08732" [ref=e174]
            - cell "34.77988" [ref=e175]
            - cell "44.8 km/h" [ref=e176]
            - cell "73%" [ref=e177]
            - cell "3/5" [ref=e178]
          - row "6 09/08/2026, 10:56 32.08825 34.78265 35.8 km/h 79% 5/5" [ref=e179]:
            - cell "6" [ref=e180]
            - cell "09/08/2026, 10:56" [ref=e181]
            - cell "32.08825" [ref=e182]
            - cell "34.78265" [ref=e183]
            - cell "35.8 km/h" [ref=e184]
            - cell "79%" [ref=e185]
            - cell "5/5" [ref=e186]
          - row "7 09/08/2026, 09:00 32.08542 34.78429 38.4 km/h 87% 4/5" [ref=e187]:
            - cell "7" [ref=e188]
            - cell "09/08/2026, 09:00" [ref=e189]
            - cell "32.08542" [ref=e190]
            - cell "34.78429" [ref=e191]
            - cell "38.4 km/h" [ref=e192]
            - cell "87%" [ref=e193]
            - cell "4/5" [ref=e194]
          - row "8 09/08/2026, 14:04 32.08335 34.77887 9.4 km/h 68% 3/5" [ref=e195]:
            - cell "8" [ref=e196]
            - cell "09/08/2026, 14:04" [ref=e197]
            - cell "32.08335" [ref=e198]
            - cell "34.77887" [ref=e199]
            - cell "9.4 km/h" [ref=e200]
            - cell "68%" [ref=e201]
            - cell "3/5" [ref=e202]
          - row "9 09/08/2026, 10:46 32.08779 34.78232 42.3 km/h 81% 3/5" [ref=e203]:
            - cell "9" [ref=e204]
            - cell "09/08/2026, 10:46" [ref=e205]
            - cell "32.08779" [ref=e206]
            - cell "34.78232" [ref=e207]
            - cell "42.3 km/h" [ref=e208]
            - cell "81%" [ref=e209]
            - cell "3/5" [ref=e210]
          - row "10 09/08/2026, 12:16 32.08382 34.78073 33.8 km/h 74% 3/5" [ref=e211]:
            - cell "10" [ref=e212]
            - cell "09/08/2026, 12:16" [ref=e213]
            - cell "32.08382" [ref=e214]
            - cell "34.78073" [ref=e215]
            - cell "33.8 km/h" [ref=e216]
            - cell "74%" [ref=e217]
            - cell "3/5" [ref=e218]
          - row "11 09/08/2026, 09:20 32.08333 34.78158 12.4 km/h 86% 5/5" [ref=e219]:
            - cell "11" [ref=e220]
            - cell "09/08/2026, 09:20" [ref=e221]
            - cell "32.08333" [ref=e222]
            - cell "34.78158" [ref=e223]
            - cell "12.4 km/h" [ref=e224]
            - cell "86%" [ref=e225]
            - cell "5/5" [ref=e226]
          - row "12 09/08/2026, 11:22 32.08663 34.78467 23.5 km/h 76% 1/5" [ref=e227]:
            - cell "12" [ref=e228]
            - cell "09/08/2026, 11:22" [ref=e229]
            - cell "32.08663" [ref=e230]
            - cell "34.78467" [ref=e231]
            - cell "23.5 km/h" [ref=e232]
            - cell "76%" [ref=e233]
            - cell "1/5" [ref=e234]
          - row "13 09/08/2026, 12:42 32.08447 34.78145 4.9 km/h 73% 1/5" [ref=e235]:
            - cell "13" [ref=e236]
            - cell "09/08/2026, 12:42" [ref=e237]
            - cell "32.08447" [ref=e238]
            - cell "34.78145" [ref=e239]
            - cell "4.9 km/h" [ref=e240]
            - cell "73%" [ref=e241]
            - cell "1/5" [ref=e242]
          - row "14 09/08/2026, 13:39 32.08376 34.77689 34.6 km/h 70% 1/5" [ref=e243]:
            - cell "14" [ref=e244]
            - cell "09/08/2026, 13:39" [ref=e245]
            - cell "32.08376" [ref=e246]
            - cell "34.77689" [ref=e247]
            - cell "34.6 km/h" [ref=e248]
            - cell "70%" [ref=e249]
            - cell "1/5" [ref=e250]
          - row "15 09/08/2026, 10:35 32.08918 34.78229 6.3 km/h 82% 4/5" [ref=e251]:
            - cell "15" [ref=e252]
            - cell "09/08/2026, 10:35" [ref=e253]
            - cell "32.08918" [ref=e254]
            - cell "34.78229" [ref=e255]
            - cell "6.3 km/h" [ref=e256]
            - cell "82%" [ref=e257]
            - cell "4/5" [ref=e258]
          - row "16 09/08/2026, 11:48 32.08537 34.78177 26.9 km/h 76% 1/5" [ref=e259]:
            - cell "16" [ref=e260]
            - cell "09/08/2026, 11:48" [ref=e261]
            - cell "32.08537" [ref=e262]
            - cell "34.78177" [ref=e263]
            - cell "26.9 km/h" [ref=e264]
            - cell "76%" [ref=e265]
            - cell "1/5" [ref=e266]
          - row "179 15/08/2026, 21:19 32.09043 34.80390 100000 km/h 150% 5/5" [ref=e267]:
            - cell "179" [ref=e268]
            - cell "15/08/2026, 21:19" [ref=e269]
            - cell "32.09043" [ref=e270]
            - cell "34.80390" [ref=e271]
            - cell "100000 km/h" [ref=e272]
            - cell "150%" [ref=e273]
            - cell "5/5" [ref=e274]
          - row "180 15/08/2026, 21:23 32.09048 34.80297 -99 km/h -87% 3/5" [ref=e275]:
            - cell "180" [ref=e276]
            - cell "15/08/2026, 21:23" [ref=e277]
            - cell "32.09048" [ref=e278]
            - cell "34.80297" [ref=e279]
            - cell "-99 km/h" [ref=e280]
            - cell "-87%" [ref=e281]
            - cell "3/5" [ref=e282]
```

# Test source

```ts
  1   | import { Page } from '@playwright/test';
  2   | import { test, expect } from '@fixtures/test.fixtures';
  3   | import { environment } from '@config/environment';
  4   | import { SEED } from '@data/seed.data';
  5   | import { validLocation } from '@data/location.factory';
  6   | import { chronological, newestPoint, oldestPoint } from '@helpers/collection.helper';
  7   | import { instantToDdMmYyyyHhMm } from '@helpers/date.helper';
  8   | import { bugMessage, knownBug } from '@helpers/known-bug';
  9   | import { distance, nearestIndex } from '@helpers/map-geometry.helper';
  10  | import { ApiClient } from '@api/api-client';
  11  | import { OffenderWatchApi } from '@api/offender-watch.api';
  12  | import { TrailPoint } from '@api/types';
  13  | import { ConsolePage } from '@pages/console.page';
  14  | 
  15  | /**
  16  |  * FR-07 / FR-08 / FR-09 — trail table, map and the Latest reading card.
  17  |  * Uses seeded Cohen, David: 16 points whose stored order is NOT chronological.
  18  |  *
  19  |  * ONE SHARED PAGE: these tests only READ the same screen (Cohen selected), so the console is opened
  20  |  * and Cohen selected once, then every test inspects it — like an operator looking at one trail.
  21  |  * Serial mode: the tests run in this order on the same page; if one breaks unexpectedly the rest
  22  |  * are skipped instead of reporting on a screen in an unknown state.
  23  |  * Tests that CHANGE state (create / edit / delete / crash) keep their own fresh page.
  24  |  */
  25  | test.describe('UI · trail, map & latest reading (one page, Cohen selected once)', () => {
  26  |   test.describe.configure({ mode: 'serial' });
  27  | 
  28  |   const cohen = SEED.cohen;
  29  |   let page: Page;
  30  |   let screen: ConsolePage;
  31  |   let trail: TrailPoint[];
  32  | 
  33  |   test.beforeAll(async ({ browser, playwright }) => {
  34  |     const request = await playwright.request.newContext({ baseURL: environment.baseUrl });
  35  |     trail = await new OffenderWatchApi(new ApiClient(request)).getTrailOrThrow(cohen.id);
  36  |     await request.dispose();
  37  | 
  38  |     page = await browser.newPage(); // inherits baseURL, timezone and locale from the config
  39  |     screen = new ConsolePage(page);
  40  |     await screen.open();
  41  |     await screen.openOffender(cohen);
  42  |     await screen.map.waitUntilSettled(trail.length);
  43  |   });
  44  | 
  45  |   test.afterAll(async () => {
  46  |     expect(screen.pageErrors, 'the shared screen must not raise browser errors').toEqual([]);
  47  |     await page.close();
  48  |   });
  49  | 
  50  |   /**
  51  |    * TC-35 · FR-07
  52  |    * WHAT : the map draws one marker per trail point plus one trail line.
  53  |    * URL  : UI {BASE_URL}  select "Cohen, David" → Leaflet map
  54  |    */
  55  |   test('TC-35 map shows one marker per trail point and a trail line', { tag: ['@TC-35', '@FR-07', '@ui'] }, async () => {
  56  |     await expect(screen.map.markers).toHaveCount(trail.length);
  57  |     await expect(screen.map.polyline).toHaveCount(1);
  58  |     await expect(screen.trail.tableTitle()).toHaveText(`Trail points (${trail.length})`);
  59  |   });
  60  | 
  61  |   /**
  62  |    * TC-33 · FR-07
  63  |    * WHAT : the trail table lists the points oldest → newest.
  64  |    * URL  : UI {BASE_URL}  select "Cohen, David" → trail table   (data: GET {BASE_URL}api/offenders/1/trail)
  65  |    */
  66  |   test('TC-33 trail table is ordered oldest to newest', { tag: ['@TC-33', '@FR-07', '@ui'] }, async () => {
  67  |     knownBug('BUG-06');
  68  |     const shownIds = (await screen.trail.readTrailRows()).map((r) => r.id);
  69  | 
> 70  |     expect(shownIds, bugMessage('BUG-06', 'table must list points chronologically')).toEqual(chronological(trail).map((p) => p.id));
      |                                                                                      ^ Error: [BUG-06] table must list points chronologically
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
  162 |     expect(await consolePage.isBlank(), bugMessage('BUG-15', 'console must not go blank')).toBe(false);
  163 |     expect(consolePage.pageErrors, bugMessage('BUG-15', 'no uncaught errors')).toEqual([]);
  164 |   });
  165 | });
  166 | 
```