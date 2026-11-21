# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\04-locations-and-stats.ui.spec.ts >> UI · add location & live counters >> TC-43 invalid location values are rejected with a visible error
- Location: tests\ui\04-locations-and-stats.ui.spec.ts:47:7

# Error details

```
Error: [BUG-14] server must reject battery 150 / speed -5

expect(received).toBe(expected) // Object.is equality

Expected: 400
Received: 201
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
        - generic [ref=e11]: "12"
        - text: Offenders
      - generic [ref=e12]:
        - generic [ref=e13]: "11"
        - text: Active
      - generic [ref=e14]:
        - generic [ref=e15]: "1020"
        - text: Trail points
  - generic [ref=e16]:
    - generic [ref=e17]:
      - generic [ref=e18]:
        - textbox "Search name or national ID…" [ref=e19]: "997214204"
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
          - row "Segal, Roni 997214204 17/03/1985 Medium Active Edit Delete" [ref=e31] [cursor=pointer]:
            - cell "Segal, Roni" [ref=e32]
            - cell "997214204" [ref=e33]
            - cell "17/03/1985" [ref=e34]
            - cell "Medium" [ref=e35]:
              - generic [ref=e36]: Medium
            - cell "Active" [ref=e37]:
              - generic [ref=e38]: Active
            - cell "Edit Delete" [ref=e39]:
              - button "Edit" [ref=e40]
              - button "Delete" [ref=e41]
      - generic [ref=e42]:
        - button "‹ Prev" [disabled] [ref=e43]
        - generic [ref=e44]: Page 1 of 1 (1 results)
        - button "Next ›" [disabled] [ref=e45]
    - generic [ref=e47]:
      - generic [ref=e48]:
        - generic [ref=e49]:
          - heading "Roni Segal" [level=2] [ref=e50]
          - generic [ref=e51]:
            - text: "National ID: 997214204 · DOB: 17/03/1985 ·"
            - generic [ref=e52]: Medium
            - generic [ref=e53]: Active
        - generic [ref=e54]:
          - generic [ref=e55]: Latest reading
          - generic [ref=e56]:
            - generic [ref=e57]: "-5 km/h"
            - generic [ref=e58]:
              - generic [ref=e59]: 150%
              - text: battery
            - generic [ref=e60]:
              - generic [ref=e61]: 4/5
              - text: signal
          - generic [ref=e62]: "Last seen: 21/11/2026, 18:19"
      - generic [ref=e63]:
        - generic:
          - generic:
            - img
        - generic:
          - generic [ref=e65]:
            - button "Zoom in" [disabled] [ref=e66]: +
            - button "Zoom out" [ref=e67] [cursor=pointer]: −
          - generic [ref=e68]:
            - link "Leaflet" [ref=e69] [cursor=pointer]:
              - /url: https://leafletjs.com
              - img [ref=e70]
              - text: Leaflet
            - text: "| © OpenStreetMap contributors"
      - generic [ref=e74]:
        - heading "Trail points (1)" [level=3] [ref=e75]
        - button "+ Add location" [ref=e76] [cursor=pointer]
      - table [ref=e77]:
        - rowgroup [ref=e78]:
          - row "# Timestamp Latitude Longitude Speed Battery Signal" [ref=e79]:
            - columnheader "#" [ref=e80]
            - columnheader "Timestamp" [ref=e81]
            - columnheader "Latitude" [ref=e82]
            - columnheader "Longitude" [ref=e83]
            - columnheader "Speed" [ref=e84]
            - columnheader "Battery" [ref=e85]
            - columnheader "Signal" [ref=e86]
        - rowgroup [ref=e87]:
          - row "1020 21/11/2026, 18:19 32.10000 34.80000 -5 km/h 150% 4/5" [ref=e88]:
            - cell "1020" [ref=e89]
            - cell "21/11/2026, 18:19" [ref=e90]
            - cell "32.10000" [ref=e91]
            - cell "34.80000" [ref=e92]
            - cell "-5 km/h" [ref=e93]
            - cell "150%" [ref=e94]
            - cell "4/5" [ref=e95]
```

# Test source

```ts
  1  | import { test, expect } from '@fixtures/test.fixtures';
  2  | import { bugMessage, knownBug } from '@helpers/known-bug';
  3  | 
  4  | /**
  5  |  * FR-10 / FR-11 — adding location points through the console, and the top-bar counters.
  6  |  */
  7  | test.describe('UI · add location & live counters', () => {
  8  |   /**
  9  |    * TC-46 · FR-11, API-05
  10 |    * WHAT : the three top-bar counters (Offenders, Active, Trail points) show exactly what the
  11 |    *        stats API returns.
  12 |    * URL  : UI {BASE_URL}  top bar   vs   GET {BASE_URL}api/stats
  13 |    */
  14 |   test('TC-46 top-bar counters equal the stats API', { tag: ['@TC-46', '@FR-11', '@ui'] }, async ({ consolePage, api }) => {
  15 |     await consolePage.open();
  16 | 
  17 |     expect(await consolePage.topBar.readStats()).toEqual(await api.getStatsOrThrow());
  18 |   });
  19 | 
  20 |   /**
  21 |    * TC-40 · FR-10, FR-11
  22 |    * WHAT : adding a valid location through "+ Add location" stores it, shows it in the trail table
  23 |    *        with the entered values, and raises the "Trail points" counter by one.
  24 |    * URL  : UI {BASE_URL}  select offender → "+ Add location" → Add  →  POST {BASE_URL}api/offenders/{id}/locations
  25 |    */
  26 |   test('TC-40 operator adds a valid location point', { tag: ['@TC-40', '@FR-10', '@FR-11', '@ui'] }, async ({ consolePage, api, testData }) => {
  27 |     const offender = await testData.createOffender();
  28 |     await consolePage.open();
  29 |     await consolePage.openOffender(offender);
  30 |     const before = await consolePage.topBar.readStats();
  31 | 
  32 |     const response = await consolePage.trail.addLocation({ lat: '32.0901', lon: '34.7802', speedKmh: '18.5', batteryPct: '77', signal: '4' });
  33 | 
  34 |     expect(response.status()).toBe(201);
  35 |     await expect(consolePage.trail.rows).toHaveCount(1);
  36 |     expect((await consolePage.trail.readTrailRows())[0]).toMatchObject({ lat: 32.0901, lon: 34.7802, speedKmh: 18.5, batteryPct: 77, signal: 4 });
  37 |     await expect.poll(() => consolePage.topBar.readStats().then((s) => s.totalLocationPoints)).toBe(before.totalLocationPoints + 1);
  38 |     expect(await api.getTrailOrThrow(offender.id)).toHaveLength(1);
  39 |   });
  40 | 
  41 |   /**
  42 |    * TC-43 · FR-10 (negative)
  43 |    * WHAT : entering invalid values in the add-location form (battery 150 %, speed −5) is rejected:
  44 |    *        the server answers 400, the operator sees an error message, and no point is added.
  45 |    * URL  : UI {BASE_URL}  select offender → "+ Add location" → Add  →  POST {BASE_URL}api/offenders/{id}/locations
  46 |    */
  47 |   test('TC-43 invalid location values are rejected with a visible error', { tag: ['@TC-43', '@FR-10', '@ui', '@negative'] }, async ({ consolePage, api, testData }) => {
  48 |     knownBug('BUG-14', 'BUG-16');
  49 |     const offender = await testData.createOffender();
  50 |     await consolePage.open();
  51 |     await consolePage.openOffender(offender);
  52 | 
  53 |     const response = await consolePage.trail.addLocation({ lat: '32.1', lon: '34.8', speedKmh: '-5', batteryPct: '150', signal: '4' });
  54 | 
> 55 |     expect(response.status(), bugMessage('BUG-14', 'server must reject battery 150 / speed -5')).toBe(400);
     |                                                                                                  ^ Error: [BUG-14] server must reject battery 150 / speed -5
  56 |     await expect(consolePage.errorBanner(), bugMessage('BUG-16', 'operator must see why the point was rejected')).toBeVisible();
  57 |     expect(await api.getTrailOrThrow(offender.id), 'no point may be stored').toEqual([]);
  58 |   });
  59 | });
  60 | 
```