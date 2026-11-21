# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\04-offenders-update-delete.api.spec.ts >> API · update & delete offender >> TC-26 deleted offender returns 404 on GET
- Location: tests\api\04-offenders-update-delete.api.spec.ts:86:7

# Error details

```
Error: [BUG-08] GET deleted offender must be 404: GET https://svcdemoaz.puremonitor.supercom.com/AQApplication/Neil/api/offenders/509 -> 200 <empty body>

expect(received).toBe(expected) // Object.is equality

Expected: 404
Received: 200
```

# Test source

```ts
  1   | import { test, expect } from '@fixtures/test.fixtures';
  2   | import { SEED } from '@data/seed.data';
  3   | import { validOffender } from '@data/offender.factory';
  4   | import { outOfOrderTrail, validLocation } from '@data/location.factory';
  5   | import { INVALID_UPDATE_CASES } from '@data/validation.data';
  6   | import { bugMessage, knownBug, knownBugIf } from '@helpers/known-bug';
  7   | import { describe as describeResponse } from '@api/api-client';
  8   | 
  9   | /** An id far above anything the instance will reach during the assignment. */
  10  | const UNKNOWN_ID = 987_654;
  11  | 
  12  | /**
  13  |  * FR-04 / FR-05 / API-02 / API-03 — editing, deleting and "not found" handling.
  14  |  */
  15  | test.describe('API · update & delete offender', () => {
  16  |   /**
  17  |    * TC-23 · FR-04, API-03
  18  |    * WHAT : PUT with a complete, valid payload returns 200 and persists exactly those values.
  19  |    * URL  : PUT {BASE_URL}api/offenders/{id}   then   GET {BASE_URL}api/offenders/{id}
  20  |    */
  21  |   test('TC-23 PUT with a full valid payload persists every field', { tag: ['@TC-23', '@FR-04'] }, async ({ api, testData }) => {
  22  |     const offender = await testData.createOffender({ riskLevel: 'Low', status: 'Active' });
  23  |     const changes = validOffender({ riskLevel: 'High', status: 'Inactive', dateOfBirth: '1979-12-31' });
  24  | 
  25  |     const result = await api.updateOffender(offender.id, changes);
  26  | 
  27  |     expect(result.status, describeResponse(result)).toBe(200);
  28  |     expect(await api.getOffenderOrThrow(offender.id)).toMatchObject(changes);
  29  |   });
  30  | 
  31  |   for (const row of INVALID_UPDATE_CASES) {
  32  |     /**
  33  |      * TC-24 · FR-04, API-03 (negative)
  34  |      * WHAT : PUT enforces the same validation as POST — an invalid value is rejected with 400 and
  35  |      *        the stored offender is left unchanged.
  36  |      * URL  : PUT {BASE_URL}api/offenders/{id}   then   GET {BASE_URL}api/offenders/{id}
  37  |      */
  38  |     test(`${row.tc} PUT rejects ${row.name}`, { tag: [`@${row.tc}`, '@FR-04', '@API-03', '@negative'] }, async ({ api, testData }) => {
  39  |       knownBugIf(row.knownBug);
  40  |       const offender = await testData.createOffender();
  41  |       const { id, createdAt, ...original } = offender;
  42  | 
  43  |       const result = await api.updateOffender(id, { ...original, ...row.override });
  44  | 
  45  |       expect(result.status, `[${row.knownBug}] PUT with ${row.name} must be rejected: ${describeResponse(result)}`).toBe(400);
  46  |       expect(await api.getOffenderOrThrow(id), 'stored record must be unchanged').toMatchObject(original);
  47  |     });
  48  |   }
  49  | 
  50  |   /**
  51  |    * TC-24 · FR-03, FR-04, API-03 (negative)
  52  |    * WHAT : PUT cannot be used to take another offender's national ID (uniqueness on update).
  53  |    * URL  : PUT {BASE_URL}api/offenders/{id}  body.nationalId = seeded Cohen's ID
  54  |    */
  55  |   test('TC-24 PUT rejects a national ID that belongs to another offender', { tag: ['@TC-24', '@FR-04', '@API-03', '@negative'] }, async ({ api, testData }) => {
  56  |     knownBug('BUG-13');
  57  |     const offender = await testData.createOffender();
  58  |     const { id, createdAt, ...original } = offender;
  59  | 
  60  |     const result = await api.updateOffender(id, { ...original, nationalId: SEED.cohen.nationalId });
  61  | 
  62  |     expect(result.status, bugMessage('BUG-13', `duplicate national ID on update must be rejected: ${describeResponse(result)}`)).toBe(400);
  63  |   });
  64  | 
  65  |   /**
  66  |    * TC-28 · FR-05, API-02
  67  |    * WHAT : DELETE of an existing offender returns 204; deleting the same id again returns 404.
  68  |    * URL  : DELETE {BASE_URL}api/offenders/{id}  (twice)
  69  |    */
  70  |   test('TC-28 DELETE returns 204, repeated DELETE returns 404', { tag: ['@TC-28', '@FR-05', '@API-02'] }, async ({ api, testData }) => {
  71  |     const offender = await testData.createOffender();
  72  | 
  73  |     const first = await api.deleteOffender(offender.id);
  74  |     const second = await api.deleteOffender(offender.id);
  75  |     await testData.forget(offender.id);
  76  | 
  77  |     expect(first.status, describeResponse(first)).toBe(204);
  78  |     expect(second.status, describeResponse(second)).toBe(404);
  79  |   });
  80  | 
  81  |   /**
  82  |    * TC-26 · FR-05, API-02
  83  |    * WHAT : after deletion the offender can no longer be retrieved — GET by id returns 404.
  84  |    * URL  : DELETE {BASE_URL}api/offenders/{id}   then   GET {BASE_URL}api/offenders/{id}
  85  |    */
  86  |   test('TC-26 deleted offender returns 404 on GET', { tag: ['@TC-26', '@FR-05', '@API-02'] }, async ({ api, testData }) => {
  87  |     knownBug('BUG-08');
  88  |     const offender = await testData.createOffender();
  89  |     expect((await api.deleteOffender(offender.id)).status).toBe(204);
  90  |     await testData.forget(offender.id);
  91  | 
  92  |     const result = await api.getOffender(offender.id);
  93  | 
> 94  |     expect(result.status, bugMessage('BUG-08', `GET deleted offender must be 404: ${describeResponse(result)}`)).toBe(404);
      |                                                                                                                  ^ Error: [BUG-08] GET deleted offender must be 404: GET https://svcdemoaz.puremonitor.supercom.com/AQApplication/Neil/api/offenders/509 -> 200 <empty body>
  95  |   });
  96  | 
  97  |   /**
  98  |    * TC-27 · FR-05, API-05 (data)
  99  |    * WHAT : deleting an offender also deletes all of their trail points — the trail is no longer
  100 |    *        returned and the stored points are gone.
  101 |    * URL  : POST {BASE_URL}api/offenders/{id}/locations (x4) → DELETE {BASE_URL}api/offenders/{id}
  102 |    *        → GET {BASE_URL}api/offenders/{id}/trail
  103 |    */
  104 |   test('TC-27 deleting an offender deletes their trail data', { tag: ['@TC-27', '@FR-05', '@data'] }, async ({ api, testData }) => {
  105 |     knownBug('BUG-02');
  106 |     const offender = await testData.createOffenderWithTrail(outOfOrderTrail());
  107 |     expect(await api.getTrailOrThrow(offender.id)).toHaveLength(4);
  108 | 
  109 |     expect((await api.deleteOffender(offender.id)).status).toBe(204);
  110 |     await testData.forget(offender.id);
  111 | 
  112 |     const trail = await api.getTrail(offender.id);
  113 |     const remaining = trail.status === 200 ? (trail.body ?? []) : [];
  114 |     expect(remaining, bugMessage('BUG-02', 'no trail point may survive the deletion of its offender')).toEqual([]);
  115 |   });
  116 | 
  117 |   /**
  118 |    * TC-50 · API-02
  119 |    * WHAT : requesting an offender that does not exist returns HTTP 404.
  120 |    * URL  : GET {BASE_URL}api/offenders/987654
  121 |    */
  122 |   test('TC-50 GET unknown offender returns 404', { tag: ['@TC-50', '@API-02', '@negative'] }, async ({ api }) => {
  123 |     knownBug('BUG-08');
  124 |     const result = await api.getOffender(UNKNOWN_ID);
  125 | 
  126 |     expect(result.status, bugMessage('BUG-08', `unknown offender must be 404: ${describeResponse(result)}`)).toBe(404);
  127 |   });
  128 | 
  129 |   /**
  130 |    * TC-52 · API-02
  131 |    * WHAT : PUT, DELETE and POST-location on an unknown offender id all return 404.
  132 |    * URL  : PUT    {BASE_URL}api/offenders/987654
  133 |    *        DELETE {BASE_URL}api/offenders/987654
  134 |    *        POST   {BASE_URL}api/offenders/987654/locations
  135 |    */
  136 |   test('TC-52 mutating an unknown offender returns 404', { tag: ['@TC-52', '@API-02', '@negative'] }, async ({ api }) => {
  137 |     const put = await api.updateOffender(UNKNOWN_ID, validOffender());
  138 |     const del = await api.deleteOffender(UNKNOWN_ID);
  139 |     const loc = await api.addLocation(UNKNOWN_ID, validLocation());
  140 | 
  141 |     expect(put.status, describeResponse(put)).toBe(404);
  142 |     expect(del.status, describeResponse(del)).toBe(404);
  143 |     expect(loc.status, describeResponse(loc)).toBe(404);
  144 |   });
  145 | });
  146 | 
```