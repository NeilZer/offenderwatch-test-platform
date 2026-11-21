# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\02-offenders-search.api.spec.ts >> API · offender search >> TC-09 case-insensitive search "COHEN" finds Cohen
- Location: tests\api\02-offenders-search.api.spec.ts:40:9

# Error details

```
Error: [BUG-07] "COHEN" must match David Cohen

expect(received).toContain(expected) // indexOf

Expected value: 1
Received array: []
```

# Test source

```ts
  1  | import { test, expect } from '@fixtures/test.fixtures';
  2  | import { SEED } from '@data/seed.data';
  3  | import { SPECIAL_CHAR_SEARCHES } from '@data/validation.data';
  4  | import { bugMessage, knownBug } from '@helpers/known-bug';
  5  | import { describe as describeResponse } from '@api/api-client';
  6  | 
  7  | /**
  8  |  * FR-02 — search by first name, last name or national ID; partial and case-insensitive.
  9  |  */
  10 | test.describe('API · offender search', () => {
  11 |   const cohen = SEED.cohen;
  12 | 
  13 |   const substringCases = [
  14 |     { field: 'last name (exact)', term: cohen.lastName },
  15 |     { field: 'last name (middle substring)', term: cohen.lastName.slice(1, 4) }, // "ohe"
  16 |     { field: 'first name (prefix)', term: cohen.firstName.slice(0, 3) }, // "Dav"
  17 |     { field: 'national ID (suffix)', term: cohen.nationalId.slice(-6) }, // "412876"
  18 |   ];
  19 | 
  20 |   for (const { field, term } of substringCases) {
  21 |     /**
  22 |      * TC-08 · FR-02, API-01
  23 |      * WHAT : search matches a substring of first name, last name or national ID.
  24 |      * URL  : GET {BASE_URL}api/offenders?search={term}&page=1&pageSize=1000
  25 |      */
  26 |     test(`TC-08 search by ${field} "${term}" finds ${cohen.lastName}`, { tag: ['@TC-08', '@FR-02'] }, async ({ api }) => {
  27 |       const result = await api.listOffenders({ search: term, page: 1, pageSize: 1000 });
  28 | 
  29 |       expect(result.status, describeResponse(result)).toBe(200);
  30 |       expect(result.body!.items.map((o) => o.id)).toContain(cohen.id);
  31 |     });
  32 |   }
  33 | 
  34 |   for (const term of [cohen.lastName.toLowerCase(), cohen.lastName.toUpperCase(), 'dAVID']) {
  35 |     /**
  36 |      * TC-09 · FR-02
  37 |      * WHAT : search is case-insensitive — a different letter case finds the same offender.
  38 |      * URL  : GET {BASE_URL}api/offenders?search={cohen|COHEN|dAVID}&page=1&pageSize=1000
  39 |      */
  40 |     test(`TC-09 case-insensitive search "${term}" finds ${cohen.lastName}`, { tag: ['@TC-09', '@FR-02'] }, async ({ api }) => {
  41 |       knownBug('BUG-07');
  42 |       const result = await api.listOffenders({ search: term, page: 1, pageSize: 1000 });
  43 | 
  44 |       expect(result.status).toBe(200);
> 45 |       expect(result.body!.items.map((o) => o.id), bugMessage('BUG-07', `"${term}" must match ${cohen.firstName} ${cohen.lastName}`)).toContain(cohen.id);
     |                                                                                                                                      ^ Error: [BUG-07] "COHEN" must match David Cohen
  46 |     });
  47 |   }
  48 | 
  49 |   /**
  50 |    * TC-11 · FR-02 (negative)
  51 |    * WHAT : terms with SQL wildcard / quote characters, or that match nothing, return an empty
  52 |    *        200 result with consistent metadata — never an error and never "match everything".
  53 |    * URL  : GET {BASE_URL}api/offenders?search={% | _ | ' | " | zzzz-no-such-offender}
  54 |    */
  55 |   test('TC-11 special characters and no-match terms return an empty result', { tag: ['@TC-11', '@FR-02'] }, async ({ api }) => {
  56 |     for (const term of SPECIAL_CHAR_SEARCHES) {
  57 |       const result = await api.listOffenders({ search: term, page: 1, pageSize: 5 });
  58 |       expect(result.status, `search "${term}": ${describeResponse(result)}`).toBe(200);
  59 |       expect(result.body!.total, `search "${term}" must be treated literally`).toBe(0);
  60 |       expect(result.body!.items).toEqual([]);
  61 |     }
  62 |   });
  63 | });
  64 | 
```