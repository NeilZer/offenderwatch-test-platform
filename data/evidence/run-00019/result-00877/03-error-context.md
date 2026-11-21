# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\01-offenders-paging.api.spec.ts >> API · offender list & paging >> TC-03 totalPages equals ceil(total / pageSize) for every page size
- Location: tests\api\01-offenders-paging.api.spec.ts:39:7

# Error details

```
Error: [BUG-01] totalPages must be ceil(total / pageSize)

expect(received).toEqual(expected) // deep equality

- Expected  - 1
+ Received  + 5

- Array []
+ Array [
+   "pageSize=5: total=36 totalPages=7 expected=8",
+   "pageSize=10: total=36 totalPages=3 expected=4",
+   "pageSize=11: total=36 totalPages=3 expected=4",
+ ]
```

# Test source

```ts
  1   | import { test, expect } from '@fixtures/test.fixtures';
  2   | import { environment } from '@config/environment';
  3   | import { PAGE_SIZES_TO_CHECK } from '@data/validation.data';
  4   | import { compareNames, expectedTotalPages, findDuplicates } from '@helpers/collection.helper';
  5   | import { bugMessage, knownBug } from '@helpers/known-bug';
  6   | import { describe as describeResponse } from '@api/api-client';
  7   | 
  8   | /**
  9   |  * FR-01 / API-01 — listing and paging of offenders through the REST API.
  10  |  */
  11  | test.describe('API · offender list & paging', () => {
  12  |   /**
  13  |    * TC-05 · API-01, FR-01
  14  |    * WHAT : the list response has the documented contract (items, total, page, pageSize,
  15  |    *        totalPages with correct types) and items are sorted alphabetically by last name.
  16  |    * URL  : GET {BASE_URL}api/offenders?page=1&pageSize=1000
  17  |    */
  18  |   test('TC-05 list response contract and last-name sort order', { tag: ['@TC-05', '@API-01', '@FR-01'] }, async ({ api }) => {
  19  |     const result = await api.listOffenders({ page: 1, pageSize: 1000 });
  20  | 
  21  |     expect(result.status, describeResponse(result)).toBe(200);
  22  |     const body = result.body!;
  23  |     expect(Array.isArray(body.items)).toBe(true);
  24  |     for (const key of ['total', 'page', 'pageSize', 'totalPages'] as const) {
  25  |       expect(Number.isInteger(body[key]), `${key} must be an integer`).toBe(true);
  26  |     }
  27  |     expect(body.total).toBe(body.items.length);
  28  | 
  29  |     const lastNames = body.items.map((o) => o.lastName);
  30  |     expect(lastNames, 'items must be sorted alphabetically by last name').toEqual([...lastNames].sort(compareNames));
  31  |   });
  32  | 
  33  |   /**
  34  |    * TC-03 · API-01, FR-01
  35  |    * WHAT : paging metadata is consistent with the data: totalPages = ceil(total / pageSize),
  36  |    *        checked for many page sizes, including sizes that leave a partial last page.
  37  |    * URL  : GET {BASE_URL}api/offenders?page=1&pageSize={1,2,3,4,5,6,10,11,12,50}
  38  |    */
  39  |   test('TC-03 totalPages equals ceil(total / pageSize) for every page size', { tag: ['@TC-03', '@API-01', '@FR-01'] }, async ({ api, testData }) => {
  40  |     knownBug('BUG-01');
  41  |     await ensureTotalLeavesPartialPage(api, testData);
  42  | 
  43  |     const mismatches: string[] = [];
  44  |     for (const pageSize of PAGE_SIZES_TO_CHECK) {
  45  |       const { body } = await api.listOffenders({ page: 1, pageSize });
  46  |       const expected = expectedTotalPages(body!.total, pageSize);
  47  |       if (body!.totalPages !== expected) {
  48  |         mismatches.push(`pageSize=${pageSize}: total=${body!.total} totalPages=${body!.totalPages} expected=${expected}`);
  49  |       }
  50  |     }
> 51  |     expect(mismatches, bugMessage('BUG-01', 'totalPages must be ceil(total / pageSize)')).toEqual([]);
      |                                                                                           ^ Error: [BUG-01] totalPages must be ceil(total / pageSize)
  52  |   });
  53  | 
  54  |   /**
  55  |    * TC-04 · API-01, FR-01
  56  |    * WHAT : walking pages 1..totalPages (as a client following the metadata would) returns every
  57  |    *        offender exactly once — nothing unreachable, nothing duplicated.
  58  |    * URL  : GET {BASE_URL}api/offenders?page={1..totalPages}&pageSize=5
  59  |    */
  60  |   test('TC-04 following totalPages reaches every offender exactly once', { tag: ['@TC-04', '@API-01', '@FR-01'] }, async ({ api, testData }) => {
  61  |     knownBug('BUG-01');
  62  |     await ensureTotalLeavesPartialPage(api, testData);
  63  |     const pageSize = environment.uiPageSize;
  64  | 
  65  |     const first = (await api.listOffenders({ page: 1, pageSize })).body!;
  66  |     const ids = [...first.items.map((o) => o.id)];
  67  |     for (let page = 2; page <= first.totalPages; page += 1) {
  68  |       const { body } = await api.listOffenders({ page, pageSize });
  69  |       ids.push(...body!.items.map((o) => o.id));
  70  |     }
  71  | 
  72  |     expect(findDuplicates(ids), 'no offender may appear on two pages').toEqual([]);
  73  |     expect(ids.length, bugMessage('BUG-01', `pages 1..${first.totalPages} must contain all ${first.total} offenders`)).toBe(first.total);
  74  |   });
  75  | 
  76  |   /**
  77  |    * TC-06 · API-01 (negative)
  78  |    * WHAT : out-of-range and invalid paging parameters are handled gracefully: a page past the end
  79  |    *        returns no items with intact metadata; page=0 / negative values never cause a 5xx.
  80  |    * URL  : GET {BASE_URL}api/offenders?page=999&pageSize=5
  81  |    *        GET {BASE_URL}api/offenders?page=0&pageSize=5 | ?page=-1&pageSize=-5 | ?page=1&pageSize=0
  82  |    */
  83  |   test('TC-06 out-of-range and invalid paging parameters are handled gracefully', { tag: ['@TC-06', '@API-01'] }, async ({ api }) => {
  84  |     const beyond = await api.listOffenders({ page: 999, pageSize: 5 });
  85  |     expect(beyond.status).toBe(200);
  86  |     expect(beyond.body!.items).toEqual([]);
  87  |     expect(beyond.body!.total).toBeGreaterThan(0);
  88  | 
  89  |     for (const query of [{ page: 0, pageSize: 5 }, { page: -1, pageSize: -5 }, { page: 1, pageSize: 0 }]) {
  90  |       const result = await api.listOffenders(query);
  91  |       expect(result.status, `${JSON.stringify(query)} -> ${describeResponse(result)}`).toBeLessThan(500);
  92  |       if (result.status === 200) {
  93  |         expect(result.body!.page, 'clamped page must be >= 1').toBeGreaterThanOrEqual(1);
  94  |         expect(result.body!.pageSize, 'clamped pageSize must be >= 1').toBeGreaterThanOrEqual(1);
  95  |       }
  96  |     }
  97  |   });
  98  | });
  99  | 
  100 | /**
  101 |  * Makes the paging tests deterministic: the defect only shows when total is NOT a multiple of the
  102 |  * page size. If leftover data made it a multiple of 5, add one temporary offender.
  103 |  */
  104 | async function ensureTotalLeavesPartialPage(
  105 |   api: import('@api/offender-watch.api').OffenderWatchApi,
  106 |   testData: import('@helpers/test-data-tracker').TestDataTracker,
  107 | ): Promise<void> {
  108 |   const { total } = await api.listAllOffendersOrThrow();
  109 |   if (total % environment.uiPageSize === 0) await testData.createOffender();
  110 | }
  111 | 
```