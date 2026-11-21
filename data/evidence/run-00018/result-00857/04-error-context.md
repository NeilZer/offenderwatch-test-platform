# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\01-roster.ui.spec.ts >> UI · offender roster >> TC-02 pager reaches every offender and shows ceil(total/5) pages
- Location: tests\ui\01-roster.ui.spec.ts:44:7

# Error details

```
Error: [BUG-01] pages must be ceil(11/5)

expect(received).toBe(expected) // Object.is equality

Expected: 3
Received: 2
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
        - generic [ref=e11]: "11"
        - text: Offenders
      - generic [ref=e12]:
        - generic [ref=e13]: "10"
        - text: Active
      - generic [ref=e14]:
        - generic [ref=e15]: "872"
        - text: Trail points
  - generic [ref=e16]:
    - generic [ref=e17]:
      - generic [ref=e18]:
        - textbox "Search name or national ID…" [ref=e19]
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
          - row "Gabay, Eli 308912345 25/03/1978 Low Active Edit Delete" [ref=e31] [cursor=pointer]:
            - cell "Gabay, Eli" [ref=e32]
            - cell "308912345" [ref=e33]
            - cell "25/03/1978" [ref=e34]
            - cell "Low" [ref=e35]:
              - generic [ref=e36]: Low
            - cell "Active" [ref=e37]:
              - generic [ref=e38]: Active
            - cell "Edit Delete" [ref=e39]:
              - button "Edit" [ref=e40]
              - button "Delete" [ref=e41]
          - row "Levi, Sara 208934512 23/10/1990 Medium Active Edit Delete" [ref=e42] [cursor=pointer]:
            - cell "Levi, Sara" [ref=e43]
            - cell "208934512" [ref=e44]
            - cell "23/10/1990" [ref=e45]
            - cell "Medium" [ref=e46]:
              - generic [ref=e47]: Medium
            - cell "Active" [ref=e48]:
              - generic [ref=e49]: Active
            - cell "Edit Delete" [ref=e50]:
              - button "Edit" [ref=e51]
              - button "Delete" [ref=e52]
          - row "Malka, Itzik 303698521 15/09/1971 High Active Edit Delete" [ref=e53] [cursor=pointer]:
            - cell "Malka, Itzik" [ref=e54]
            - cell "303698521" [ref=e55]
            - cell "15/09/1971" [ref=e56]
            - cell "High" [ref=e57]:
              - generic [ref=e58]: High
            - cell "Active" [ref=e59]:
              - generic [ref=e60]: Active
            - cell "Edit Delete" [ref=e61]:
              - button "Edit" [ref=e62]
              - button "Delete" [ref=e63]
          - row "Mizrahi, Rachel 204518793 12/00/1988 Low Active Edit Delete" [ref=e64] [cursor=pointer]:
            - cell "Mizrahi, Rachel" [ref=e65]
            - cell "204518793" [ref=e66]
            - cell "12/00/1988" [ref=e67]
            - cell "Low" [ref=e68]:
              - generic [ref=e69]: Low
            - cell "Active" [ref=e70]:
              - generic [ref=e71]: Active
            - cell "Edit Delete" [ref=e72]:
              - button "Edit" [ref=e73]
              - button "Delete" [ref=e74]
          - row "Ohana, Maya 206789123 18/06/1992 Medium Active Edit Delete" [ref=e75] [cursor=pointer]:
            - cell "Ohana, Maya" [ref=e76]
            - cell "206789123" [ref=e77]
            - cell "18/06/1992" [ref=e78]
            - cell "Medium" [ref=e79]:
              - generic [ref=e80]: Medium
            - cell "Active" [ref=e81]:
              - generic [ref=e82]: Active
            - cell "Edit Delete" [ref=e83]:
              - button "Edit" [ref=e84]
              - button "Delete" [ref=e85]
      - generic [ref=e86]:
        - button "‹ Prev" [ref=e87] [cursor=pointer]
        - generic [ref=e88]: Page 2 of 2 (11 results)
        - button "Next ›" [disabled] [ref=e89]
    - generic [ref=e91]:
      - generic [ref=e92]: 🗺️
      - paragraph [ref=e93]: Select an offender to view their trail
```

# Test source

```ts
  1   | import { test, expect } from '@fixtures/test.fixtures';
  2   | import { environment } from '@config/environment';
  3   | import { SEED } from '@data/seed.data';
  4   | import { compareNames, expectedTotalPages, findDuplicates } from '@helpers/collection.helper';
  5   | import { isoDateToDdMmYyyy } from '@helpers/date.helper';
  6   | import { bugMessage, knownBug } from '@helpers/known-bug';
  7   | import { OffenderListComponent } from '@pages/components/offender-list.component';
  8   | 
  9   | const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
  10  | 
  11  | /**
  12  |  * FR-01 / FR-02 / FR-06 — the offender roster as an operator sees it.
  13  |  */
  14  | test.describe('UI · offender roster', () => {
  15  |   test.beforeEach(async ({ consolePage }) => {
  16  |     await consolePage.open();
  17  |   });
  18  | 
  19  |   /**
  20  |    * TC-01 · FR-01
  21  |    * WHAT : the first page shows exactly 5 offenders, sorted alphabetically by last name, and they
  22  |    *        are the first 5 of the full, sorted roster returned by the API.
  23  |    * URL  : UI {BASE_URL}  (roster table)   vs   GET {BASE_URL}api/offenders?page=1&pageSize=1000
  24  |    */
  25  |   test('TC-01 first page lists 5 offenders sorted by last name', { tag: ['@TC-01', '@FR-01', '@ui'] }, async ({ consolePage, api }) => {
  26  |     const all = await api.listAllOffendersOrThrow();
  27  |     const expectedFirstPage = [...all.items]
  28  |       .sort((a, b) => compareNames(a.lastName, b.lastName))
  29  |       .slice(0, environment.uiPageSize)
  30  |       .map((o) => `${o.lastName}, ${o.firstName}`);
  31  | 
  32  |     const shown = await consolePage.list.readNames();
  33  | 
  34  |     expect(shown).toHaveLength(Math.min(environment.uiPageSize, all.total));
  35  |     expect(shown).toEqual(expectedFirstPage);
  36  |   });
  37  | 
  38  |   /**
  39  |    * TC-02 · FR-01
  40  |    * WHAT : the pager gives access to every offender: page count = ceil(total / 5), and paging with
  41  |    *        "Next" until it is disabled shows every offender in the result count, each once.
  42  |    * URL  : UI {BASE_URL}  (pager "Page x of y (n results)", Next ›)
  43  |    */
  44  |   test('TC-02 pager reaches every offender and shows ceil(total/5) pages', { tag: ['@TC-02', '@FR-01', '@ui'] }, async ({ consolePage, api, testData }) => {
  45  |     knownBug('BUG-01');
  46  |     // Deterministic: the defect needs a partial last page (total not a multiple of 5).
  47  |     if ((await api.listAllOffendersOrThrow()).total % environment.uiPageSize === 0) {
  48  |       await testData.createOffender();
  49  |       await consolePage.open();
  50  |     }
  51  | 
  52  |     const pager = await consolePage.list.readPager();
  53  |     const reachable = (await consolePage.list.collectAllRowsByPaging()).map(OffenderListComponent.rowKey);
  54  | 
  55  |     expect(findDuplicates(reachable), 'no offender shown twice').toEqual([]);
> 56  |     expect(pager.totalPages, bugMessage('BUG-01', `pages must be ceil(${pager.total}/5)`)).toBe(expectedTotalPages(pager.total, environment.uiPageSize));
      |                                                                                            ^ Error: [BUG-01] pages must be ceil(11/5)
  57  |     expect(reachable.length, bugMessage('BUG-01', `all ${pager.total} offenders must be reachable by paging`)).toBe(pager.total);
  58  |   });
  59  | 
  60  |   /**
  61  |    * TC-10 · FR-02
  62  |    * WHAT : typing the last name in lower case in the search box finds the offender
  63  |    *        (case-insensitive search through the UI).
  64  |    * URL  : UI {BASE_URL}  search box  →  GET {BASE_URL}api/offenders?search=cohen&page=1&pageSize=5
  65  |    */
  66  |   test('TC-10 lower-case search in the UI finds the offender', { tag: ['@TC-10', '@FR-02', '@ui'] }, async ({ consolePage }) => {
  67  |     knownBug('BUG-07');
  68  |     const { lastName, firstName } = SEED.cohen;
  69  | 
  70  |     await consolePage.list.search(lastName.toLowerCase());
  71  | 
  72  |     // search() already waited for the API response and the re-render, so no long wait is needed.
  73  |     await expect(consolePage.list.row(lastName, firstName), bugMessage('BUG-07', `"${lastName.toLowerCase()}" must find ${lastName}, ${firstName}`)).toBeVisible({ timeout: 3_000 });
  74  |   });
  75  | 
  76  |   /**
  77  |    * TC-29 · FR-06
  78  |    * WHAT : every date of birth in the roster is displayed as DD/MM/YYYY and equals the value the
  79  |    *        API stores (checked across all reachable pages).
  80  |    * URL  : UI {BASE_URL}  "Date of birth" column   vs   GET {BASE_URL}api/offenders?page=1&pageSize=1000
  81  |    */
  82  |   test('TC-29 roster dates of birth are DD/MM/YYYY and match the stored value', { tag: ['@TC-29', '@FR-06', '@ui'] }, async ({ consolePage, api }) => {
  83  |     knownBug('BUG-04');
  84  |     // Key = name + national ID (names repeat). Records whose stored DOB is not even a date (BUG-11
  85  |     // test data) are out of scope here: FR-06 is about how a valid stored date is displayed.
  86  |     const stored = new Map(
  87  |       (await api.listAllOffendersOrThrow()).items
  88  |         .filter((o) => ISO_DATE.test(o.dateOfBirth))
  89  |         .map((o) => [OffenderListComponent.rowKey({ name: `${o.lastName}, ${o.firstName}`, nationalId: o.nationalId }), o.dateOfBirth]),
  90  |     );
  91  | 
  92  |     const rows = [...(await consolePage.list.readRows())];
  93  |     while (!(await consolePage.list.nextButton().isDisabled())) {
  94  |       await consolePage.list.goToNextPage();
  95  |       rows.push(...(await consolePage.list.readRows()));
  96  |     }
  97  | 
  98  |     const mismatches = rows
  99  |       .filter((row) => stored.has(OffenderListComponent.rowKey(row)))
  100 |       .map((row) => ({ name: row.name, shown: row.dateOfBirth, expected: isoDateToDdMmYyyy(stored.get(OffenderListComponent.rowKey(row))!) }))
  101 |       .filter((r) => r.shown !== r.expected);
  102 |     expect(mismatches, bugMessage('BUG-04', 'displayed DOB must equal the stored DOB')).toEqual([]);
  103 |   });
  104 | 
  105 |   /**
  106 |    * TC-30 · FR-06
  107 |    * WHAT : the DOB in the selected offender's header equals the stored value
  108 |    *        (Mizrahi, Rachel — a January birthday: 1988-01-12 → 12/01/1988).
  109 |    * URL  : UI {BASE_URL}  detail header "DOB:"   vs   GET {BASE_URL}api/offenders/4
  110 |    */
  111 |   test('TC-30 detail header shows the stored date of birth', { tag: ['@TC-30', '@FR-06', '@ui'] }, async ({ consolePage, api }) => {
  112 |     knownBug('BUG-04');
  113 |     const stored = await api.getOffenderOrThrow(SEED.mizrahi.id);
  114 | 
  115 |     await consolePage.openOffender(SEED.mizrahi);
  116 | 
  117 |     expect(await consolePage.trail.dateOfBirth(), bugMessage('BUG-04', 'header DOB must equal the stored DOB')).toBe(isoDateToDdMmYyyy(stored.dateOfBirth));
  118 |   });
  119 | });
  120 | 
```