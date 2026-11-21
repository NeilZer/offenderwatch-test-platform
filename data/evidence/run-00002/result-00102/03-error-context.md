# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\06-stats.api.spec.ts >> API · statistics >> TC-48 trail-point total drops when an offender with a trail is deleted
- Location: tests\api\06-stats.api.spec.ts:49:7

# Error details

```
Error: [BUG-02] deleting 4 points must lower the total by 4

expect(received).toBe(expected) // Object.is equality

Expected: 4
Received: 0
```

# Test source

```ts
  1  | import { test, expect } from '@fixtures/test.fixtures';
  2  | import { outOfOrderTrail } from '@data/location.factory';
  3  | import { bugMessage, knownBug } from '@helpers/known-bug';
  4  | 
  5  | /**
  6  |  * FR-11 / API-05 — totals returned by /api/stats must always match the stored data.
  7  |  * (Run with workers=1: these tests assert exact deltas on a shared instance.)
  8  |  */
  9  | test.describe('API · statistics', () => {
  10 |   /**
  11 |    * TC-49 · API-05, FR-11
  12 |    * WHAT : totalOffenders and activeOffenders equal what the offender list actually contains.
  13 |    * URL  : GET {BASE_URL}api/stats   vs   GET {BASE_URL}api/offenders?page=1&pageSize=1000
  14 |    */
  15 |   test('TC-49 offender totals match the stored offenders', { tag: ['@TC-49', '@API-05', '@FR-11'] }, async ({ api }) => {
  16 |     const stats = await api.getStatsOrThrow();
  17 |     const all = await api.listAllOffendersOrThrow();
  18 | 
  19 |     expect(stats.totalOffenders).toBe(all.total);
  20 |     expect(stats.activeOffenders).toBe(all.items.filter((o) => o.status === 'Active').length);
  21 |   });
  22 | 
  23 |   /**
  24 |    * TC-47 · API-05, FR-11
  25 |    * WHAT : counters update after creation — +1 offender, +1 active (Active status only),
  26 |    *        +N trail points after N locations are added; an Inactive offender is not counted
  27 |    *        as active.
  28 |    * URL  : GET {BASE_URL}api/stats → POST {BASE_URL}api/offenders → POST .../locations → GET {BASE_URL}api/stats
  29 |    */
  30 |   test('TC-47 counters update after creating offenders and points', { tag: ['@TC-47', '@API-05', '@FR-11'] }, async ({ api, testData }) => {
  31 |     const before = await api.getStatsOrThrow();
  32 | 
  33 |     const points = outOfOrderTrail();
  34 |     await testData.createOffenderWithTrail(points, { status: 'Active' });
  35 |     await testData.createOffender({ status: 'Inactive' });
  36 |     const after = await api.getStatsOrThrow();
  37 | 
  38 |     expect(after.totalOffenders - before.totalOffenders, 'two offenders created').toBe(2);
  39 |     expect(after.activeOffenders - before.activeOffenders, 'only the Active one counts as active').toBe(1);
  40 |     expect(after.totalLocationPoints - before.totalLocationPoints, 'points added').toBe(points.length);
  41 |   });
  42 | 
  43 |   /**
  44 |    * TC-48 · API-05, FR-11, FR-05 (data)
  45 |    * WHAT : deleting an offender with N trail points lowers totalLocationPoints by N
  46 |    *        (trail data is deleted together with its offender).
  47 |    * URL  : GET {BASE_URL}api/stats → DELETE {BASE_URL}api/offenders/{id} → GET {BASE_URL}api/stats
  48 |    */
  49 |   test('TC-48 trail-point total drops when an offender with a trail is deleted', { tag: ['@TC-48', '@API-05', '@FR-11', '@data'] }, async ({ api, testData }) => {
  50 |     knownBug('BUG-02');
  51 |     const points = outOfOrderTrail();
  52 |     const offender = await testData.createOffenderWithTrail(points);
  53 |     const before = await api.getStatsOrThrow();
  54 | 
  55 |     expect((await api.deleteOffender(offender.id)).status).toBe(204);
  56 |     await testData.forget(offender.id);
  57 |     const after = await api.getStatsOrThrow();
  58 | 
  59 |     expect(before.totalOffenders - after.totalOffenders).toBe(1);
> 60 |     expect(before.totalLocationPoints - after.totalLocationPoints, bugMessage('BUG-02', `deleting ${points.length} points must lower the total by ${points.length}`)).toBe(points.length);
     |                                                                                                                                                                       ^ Error: [BUG-02] deleting 4 points must lower the total by 4
  61 |   });
  62 | });
  63 | 
```