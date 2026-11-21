# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\05-trail.api.spec.ts >> API · trail & locations >> TC-41 location rejects longitude above 180
- Location: tests\api\05-trail.api.spec.ts:54:9

# Error details

```
Error: [BUG-14] longitude above 180 must be rejected: POST https://svcdemoaz.puremonitor.supercom.com/AQApplication/Neil/api/offenders/731/locations -> 201 {"ok":true}

expect(received).toBe(expected) // Object.is equality

Expected: 400
Received: 201
```

# Test source

```ts
  1  | import { test, expect } from '@fixtures/test.fixtures';
  2  | import { SEEDED_OFFENDERS } from '@data/seed.data';
  3  | import { outOfOrderTrail, validLocation } from '@data/location.factory';
  4  | import { INVALID_LOCATION_CASES, VALID_LOCATION_BOUNDARIES } from '@data/validation.data';
  5  | import { chronological } from '@helpers/collection.helper';
  6  | import { bugMessage, knownBug, knownBugIf } from '@helpers/known-bug';
  7  | import { describe as describeResponse } from '@api/api-client';
  8  | import { ApiErrorBody } from '@api/types';
  9  | 
  10 | /**
  11 |  * FR-07 / FR-10 / API-02 / API-03 / API-04 — trail retrieval and location-point validation.
  12 |  */
  13 | test.describe('API · trail & locations', () => {
  14 |   /**
  15 |    * TC-32 · API-04, FR-07
  16 |    * WHAT : every seeded offender's trail is returned in chronological order (oldest -> newest).
  17 |    * URL  : GET {BASE_URL}api/offenders/{1..11}/trail
  18 |    */
  19 |   test('TC-32 seeded trails are returned in chronological order', { tag: ['@TC-32', '@API-04', '@FR-07'] }, async ({ api }) => {
  20 |     knownBug('BUG-06');
  21 |     const unordered: string[] = [];
  22 |     for (const offender of SEEDED_OFFENDERS) {
  23 |       const trail = await api.getTrailOrThrow(offender.id);
  24 |       const actual = trail.map((p) => p.id);
  25 |       const expected = chronological(trail).map((p) => p.id);
  26 |       if (JSON.stringify(actual) !== JSON.stringify(expected)) unordered.push(`${offender.lastName} (id ${offender.id})`);
  27 |     }
  28 |     expect(unordered, bugMessage('BUG-06', 'trails must be ordered by timestamp')).toEqual([]);
  29 |   });
  30 | 
  31 |   /**
  32 |    * TC-32 · API-04, FR-07
  33 |    * WHAT : points inserted out of order (10:30, 10:00, 10:45, 10:15) come back sorted by time.
  34 |    * URL  : POST {BASE_URL}api/offenders/{id}/locations (x4)   then   GET {BASE_URL}api/offenders/{id}/trail
  35 |    */
  36 |   test('TC-32 points inserted out of order are returned chronologically', { tag: ['@TC-32', '@API-04', '@FR-07'] }, async ({ api, testData }) => {
  37 |     knownBug('BUG-06');
  38 |     const offender = await testData.createOffenderWithTrail(outOfOrderTrail());
  39 | 
  40 |     const trail = await api.getTrailOrThrow(offender.id);
  41 | 
  42 |     expect(trail.map((p) => p.timestamp), bugMessage('BUG-06', 'trail must be oldest -> newest')).toEqual(
  43 |       chronological(trail).map((p) => p.timestamp),
  44 |     );
  45 |   });
  46 | 
  47 |   for (const row of INVALID_LOCATION_CASES) {
  48 |     /**
  49 |      * TC-41 · FR-10, API-03 (negative)
  50 |      * WHAT : an out-of-range location value is rejected with 400 + error description and no
  51 |      *        point is added to the trail.
  52 |      * URL  : POST {BASE_URL}api/offenders/{id}/locations   then   GET {BASE_URL}api/offenders/{id}/trail
  53 |      */
  54 |     test(`${row.tc} location rejects ${row.name}`, { tag: [`@${row.tc}`, '@FR-10', '@API-03', '@negative'] }, async ({ api, testData }) => {
  55 |       knownBugIf(row.knownBug);
  56 |       const offender = await testData.createOffender();
  57 | 
  58 |       const result = await api.addLocation(offender.id, validLocation(row.override));
  59 | 
> 60 |       expect(result.status, `[${row.knownBug}] ${row.name} must be rejected: ${describeResponse(result)}`).toBe(400);
     |                                                                                                            ^ Error: [BUG-14] longitude above 180 must be rejected: POST https://svcdemoaz.puremonitor.supercom.com/AQApplication/Neil/api/offenders/731/locations -> 201 {"ok":true}
  61 |       expect((result.body as ApiErrorBody | null)?.error, `[${row.knownBug}] 400 must carry an error description`).toBeTruthy();
  62 |       expect(await api.getTrailOrThrow(offender.id), 'no point may be stored').toEqual([]);
  63 |     });
  64 |   }
  65 | 
  66 |   for (const { name, payload } of VALID_LOCATION_BOUNDARIES) {
  67 |     /**
  68 |      * TC-42 · FR-10
  69 |      * WHAT : boundary values that ARE valid (lat ±90, lon ±180, speed 0, battery 0/100,
  70 |      *        signal 1/5) are accepted with 201 and stored exactly.
  71 |      * URL  : POST {BASE_URL}api/offenders/{id}/locations   then   GET {BASE_URL}api/offenders/{id}/trail
  72 |      */
  73 |     test(`TC-42 location accepts boundary values: ${name}`, { tag: ['@TC-42', '@FR-10'] }, async ({ api, testData }) => {
  74 |       const offender = await testData.createOffender();
  75 | 
  76 |       const result = await api.addLocation(offender.id, payload);
  77 | 
  78 |       expect(result.status, describeResponse(result)).toBe(201);
  79 |       const [stored] = await api.getTrailOrThrow(offender.id);
  80 |       expect(stored).toMatchObject({ lat: payload.lat, lon: payload.lon, speedKmh: payload.speedKmh, batteryPct: payload.batteryPct, signal: payload.signal });
  81 |     });
  82 |   }
  83 | 
  84 |   /**
  85 |    * TC-51 · API-02
  86 |    * WHAT : the trail of an offender that does not exist returns HTTP 404 (not an empty list).
  87 |    * URL  : GET {BASE_URL}api/offenders/987654/trail
  88 |    */
  89 |   test('TC-51 trail of an unknown offender returns 404', { tag: ['@TC-51', '@API-02', '@negative'] }, async ({ api }) => {
  90 |     knownBug('BUG-08');
  91 |     const result = await api.getTrail(987_654);
  92 | 
  93 |     expect(result.status, bugMessage('BUG-08', `unknown offender trail must be 404: ${describeResponse(result)}`)).toBe(404);
  94 |   });
  95 | });
  96 | 
```