# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\03-offenders-create.api.spec.ts >> API · create offender >> TC-17 rejects date of birth in the future
- Location: tests\api\03-offenders-create.api.spec.ts:40:9

# Error details

```
Error: [BUG-11] date of birth in the future must be rejected: POST https://svcdemoaz.puremonitor.supercom.com/AQApplication/Neil/api/offenders -> 201 {"id":779,"firstName":"Omer","lastName":"Sasson","nationalId":"990575990","dateOfBirth":"2027-11-21","riskLevel":"Medium","status":"Active","createdAt":"2026-11-21T11:51:01.9471881Z"}

expect(received).toBe(expected) // Object.is equality

Expected: 400
Received: 201
```

# Test source

```ts
  1  | import { test, expect } from '@fixtures/test.fixtures';
  2  | import { SEED } from '@data/seed.data';
  3  | import { validOffender } from '@data/offender.factory';
  4  | import { INVALID_OFFENDER_CASES } from '@data/validation.data';
  5  | import { bugMessage, knownBug, knownBugIf } from '@helpers/known-bug';
  6  | import { describe as describeResponse } from '@api/api-client';
  7  | import { ApiErrorBody } from '@api/types';
  8  | 
  9  | /**
  10 |  * FR-03 / API-02 / API-03 — creating offenders and validating input.
  11 |  */
  12 | test.describe('API · create offender', () => {
  13 |   /**
  14 |    * TC-13 · FR-03, API-02
  15 |    * WHAT : a valid offender is created with HTTP 201, gets an id, and every field is stored
  16 |    *        exactly as sent (verified by reading it back).
  17 |    * URL  : POST {BASE_URL}api/offenders   then   GET {BASE_URL}api/offenders/{id}
  18 |    */
  19 |   test('TC-13 valid offender is created with 201 and stored as sent', { tag: ['@TC-13', '@FR-03', '@API-02'] }, async ({ api, testData }) => {
  20 |     const input = validOffender({ riskLevel: 'High', status: 'Inactive' });
  21 | 
  22 |     const created = await api.createOffender(input);
  23 |     await testData.register(created.body);
  24 | 
  25 |     expect(created.status, describeResponse(created)).toBe(201);
  26 |     expect(created.body!.id).toBeGreaterThan(0);
  27 |     expect(created.body).toMatchObject(input);
  28 | 
  29 |     const readBack = await api.getOffenderOrThrow(created.body!.id);
  30 |     expect(readBack).toMatchObject(input);
  31 |   });
  32 | 
  33 |   for (const row of INVALID_OFFENDER_CASES) {
  34 |     /**
  35 |      * TC-14 / TC-15 / TC-17 / TC-18 · FR-03, API-03 (negative)
  36 |      * WHAT : an invalid payload is rejected with HTTP 400 + an error description, and nothing
  37 |      *        is saved (total number of offenders unchanged).
  38 |      * URL  : POST {BASE_URL}api/offenders   (+ GET {BASE_URL}api/stats before/after)
  39 |      */
  40 |     test(`${row.tc} rejects ${row.name}`, { tag: [`@${row.tc}`, '@FR-03', '@API-03', '@negative'] }, async ({ api, testData }) => {
  41 |       knownBugIf(row.knownBug);
  42 |       const before = await api.getStatsOrThrow();
  43 | 
  44 |       const result = await api.createOffender(validOffender(row.override));
  45 |       await testData.register(result.body); // clean up if wrongly created
  46 | 
  47 |       const tag = row.knownBug ?? 'FR-03';
> 48 |       expect(result.status, `[${tag}] ${row.name} must be rejected: ${describeResponse(result)}`).toBe(400);
     |                                                                                                   ^ Error: [BUG-11] date of birth in the future must be rejected: POST https://svcdemoaz.puremonitor.supercom.com/AQApplication/Neil/api/offenders -> 201 {"id":779,"firstName":"Omer","lastName":"Sasson","nationalId":"990575990","dateOfBirth":"2027-11-21","riskLevel":"Medium","status":"Active","createdAt":"2026-11-21T11:51:01.9471881Z"}
  49 |       expect((result.body as ApiErrorBody | null)?.error, 'error description must be present').toBeTruthy();
  50 |       const after = await api.getStatsOrThrow();
  51 |       expect(after.totalOffenders, 'nothing may be saved').toBe(before.totalOffenders);
  52 |     });
  53 |   }
  54 | 
  55 |   /**
  56 |    * TC-16 · FR-03, API-03 (negative)
  57 |    * WHAT : national ID must be unique across the system — re-using a seeded offender's ID is
  58 |    *        rejected with 400 and no second record is created.
  59 |    * URL  : POST {BASE_URL}api/offenders   then   GET {BASE_URL}api/offenders?search={nationalId}
  60 |    */
  61 |   test('TC-16 rejects a duplicate national ID', { tag: ['@TC-16', '@FR-03', '@API-03', '@negative'] }, async ({ api, testData }) => {
  62 |     knownBug('BUG-09');
  63 |     const duplicateId = SEED.cohen.nationalId;
  64 | 
  65 |     const result = await api.createOffender(validOffender({ nationalId: duplicateId }));
  66 |     await testData.register(result.body);
  67 | 
  68 |     expect(result.status, bugMessage('BUG-09', `duplicate national ID must be rejected: ${describeResponse(result)}`)).toBe(400);
  69 |     const holders = (await api.listAllOffendersOrThrow(duplicateId)).items.filter((o) => o.nationalId === duplicateId);
  70 |     expect(holders, 'exactly one offender may own the national ID').toHaveLength(1);
  71 |   });
  72 | 
  73 |   /**
  74 |    * TC-20 · API-03 (negative)
  75 |    * WHAT : a malformed (non-JSON) body is rejected with 400 AND an error description, like every
  76 |    *        other validation error.
  77 |    * URL  : POST {BASE_URL}api/offenders   body: "this is not json"
  78 |    */
  79 |   test('TC-20 malformed JSON body returns 400 with an error description', { tag: ['@TC-20', '@API-03', '@negative'] }, async ({ api }) => {
  80 |     knownBug('BUG-18');
  81 |     const result = await api.createOffenderRaw('this is not json');
  82 | 
  83 |     expect(result.status).toBe(400);
  84 |     expect(result.rawBody.trim(), bugMessage('BUG-18', '400 response must describe the error')).not.toBe('');
  85 |   });
  86 | });
  87 | 
```