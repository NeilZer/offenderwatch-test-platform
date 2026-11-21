# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\01-roster.ui.spec.ts >> UI · offender roster >> TC-30 detail header shows the stored date of birth
- Location: tests\ui\01-roster.ui.spec.ts:111:7

# Error details

```
Error: [BUG-04] header DOB must equal the stored DOB

expect(received).toBe(expected) // Object.is equality

Expected: "12/01/1988"
Received: "12/00/1988"
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
        - generic [ref=e15]: "812"
        - text: Trail points
  - generic [ref=e16]:
    - generic [ref=e17]:
      - generic [ref=e18]:
        - textbox "Search name or national ID…" [ref=e19]: "204518793"
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
          - row "Mizrahi, Rachel 204518793 12/00/1988 Low Active Edit Delete" [ref=e31] [cursor=pointer]:
            - cell "Mizrahi, Rachel" [ref=e32]
            - cell "204518793" [ref=e33]
            - cell "12/00/1988" [ref=e34]
            - cell "Low" [ref=e35]:
              - generic [ref=e36]: Low
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
          - heading "Rachel Mizrahi" [level=2] [ref=e50]
          - generic [ref=e51]:
            - text: "National ID: 204518793 · DOB: 12/00/1988 ·"
            - generic [ref=e52]: Low
            - generic [ref=e53]: Active
        - generic [ref=e54]:
          - generic [ref=e55]: Latest reading
          - generic [ref=e56]:
            - generic [ref=e57]: 16.4 km/h
            - generic [ref=e58]:
              - generic [ref=e59]: 87%
              - text: battery
            - generic [ref=e60]:
              - generic [ref=e61]: 2/5
              - text: signal
          - generic [ref=e62]: "Last seen: 09/08/2026, 15:16"
      - generic [ref=e63]:
        - generic:
          - generic:
            - img
        - generic:
          - generic [ref=e77]:
            - button "Zoom in" [ref=e78] [cursor=pointer]: +
            - button "Zoom out" [ref=e79] [cursor=pointer]: −
          - generic [ref=e80]:
            - link "Leaflet" [ref=e81] [cursor=pointer]:
              - /url: https://leafletjs.com
              - img [ref=e82]
              - text: Leaflet
            - text: "| © OpenStreetMap contributors"
      - generic [ref=e86]:
        - heading "Trail points (12)" [level=3] [ref=e87]
        - button "+ Add location" [ref=e88] [cursor=pointer]
      - table [ref=e89]:
        - rowgroup [ref=e90]:
          - row "# Timestamp Latitude Longitude Speed Battery Signal" [ref=e91]:
            - columnheader "#" [ref=e92]
            - columnheader "Timestamp" [ref=e93]
            - columnheader "Latitude" [ref=e94]
            - columnheader "Longitude" [ref=e95]
            - columnheader "Speed" [ref=e96]
            - columnheader "Battery" [ref=e97]
            - columnheader "Signal" [ref=e98]
        - rowgroup [ref=e99]:
          - row "49 09/08/2026, 14:09 32.06209 34.77087 34.6 km/h 77% 5/5" [ref=e100]:
            - cell "49" [ref=e101]
            - cell "09/08/2026, 14:09" [ref=e102]
            - cell "32.06209" [ref=e103]
            - cell "34.77087" [ref=e104]
            - cell "34.6 km/h" [ref=e105]
            - cell "77%" [ref=e106]
            - cell "5/5" [ref=e107]
          - row "50 09/08/2026, 12:53 32.06131 34.76588 32.6 km/h 83% 2/5" [ref=e108]:
            - cell "50" [ref=e109]
            - cell "09/08/2026, 12:53" [ref=e110]
            - cell "32.06131" [ref=e111]
            - cell "34.76588" [ref=e112]
            - cell "32.6 km/h" [ref=e113]
            - cell "83%" [ref=e114]
            - cell "2/5" [ref=e115]
          - row "51 09/08/2026, 12:26 32.06053 34.76670 53.2 km/h 86% 2/5" [ref=e116]:
            - cell "51" [ref=e117]
            - cell "09/08/2026, 12:26" [ref=e118]
            - cell "32.06053" [ref=e119]
            - cell "34.76670" [ref=e120]
            - cell "53.2 km/h" [ref=e121]
            - cell "86%" [ref=e122]
            - cell "2/5" [ref=e123]
          - row "52 09/08/2026, 15:00 32.06560 34.77483 43.4 km/h 71% 1/5" [ref=e124]:
            - cell "52" [ref=e125]
            - cell "09/08/2026, 15:00" [ref=e126]
            - cell "32.06560" [ref=e127]
            - cell "34.77483" [ref=e128]
            - cell "43.4 km/h" [ref=e129]
            - cell "71%" [ref=e130]
            - cell "1/5" [ref=e131]
          - row "53 09/08/2026, 15:16 32.06830 34.77653 15.8 km/h 68% 3/5" [ref=e132]:
            - cell "53" [ref=e133]
            - cell "09/08/2026, 15:16" [ref=e134]
            - cell "32.06830" [ref=e135]
            - cell "34.77653" [ref=e136]
            - cell "15.8 km/h" [ref=e137]
            - cell "68%" [ref=e138]
            - cell "3/5" [ref=e139]
          - row "54 09/08/2026, 14:31 32.06457 34.77372 27.2 km/h 74% 1/5" [ref=e140]:
            - cell "54" [ref=e141]
            - cell "09/08/2026, 14:31" [ref=e142]
            - cell "32.06457" [ref=e143]
            - cell "34.77372" [ref=e144]
            - cell "27.2 km/h" [ref=e145]
            - cell "74%" [ref=e146]
            - cell "1/5" [ref=e147]
          - row "55 09/08/2026, 13:28 32.05717 34.76941 48.3 km/h 83% 5/5" [ref=e148]:
            - cell "55" [ref=e149]
            - cell "09/08/2026, 13:28" [ref=e150]
            - cell "32.05717" [ref=e151]
            - cell "34.76941" [ref=e152]
            - cell "48.3 km/h" [ref=e153]
            - cell "83%" [ref=e154]
            - cell "5/5" [ref=e155]
          - row "56 09/08/2026, 13:16 32.05909 34.76749 31.7 km/h 83% 5/5" [ref=e156]:
            - cell "56" [ref=e157]
            - cell "09/08/2026, 13:16" [ref=e158]
            - cell "32.05909" [ref=e159]
            - cell "34.76749" [ref=e160]
            - cell "31.7 km/h" [ref=e161]
            - cell "83%" [ref=e162]
            - cell "5/5" [ref=e163]
          - row "57 09/08/2026, 14:41 32.06344 34.77266 21.3 km/h 74% 4/5" [ref=e164]:
            - cell "57" [ref=e165]
            - cell "09/08/2026, 14:41" [ref=e166]
            - cell "32.06344" [ref=e167]
            - cell "34.77266" [ref=e168]
            - cell "21.3 km/h" [ref=e169]
            - cell "74%" [ref=e170]
            - cell "4/5" [ref=e171]
          - row "58 09/08/2026, 13:56 32.06062 34.77187 2.3 km/h 80% 2/5" [ref=e172]:
            - cell "58" [ref=e173]
            - cell "09/08/2026, 13:56" [ref=e174]
            - cell "32.06062" [ref=e175]
            - cell "34.77187" [ref=e176]
            - cell "2.3 km/h" [ref=e177]
            - cell "80%" [ref=e178]
            - cell "2/5" [ref=e179]
          - row "59 09/08/2026, 12:00 32.06242 34.76819 16.4 km/h 87% 2/5" [ref=e180]:
            - cell "59" [ref=e181]
            - cell "09/08/2026, 12:00" [ref=e182]
            - cell "32.06242" [ref=e183]
            - cell "34.76819" [ref=e184]
            - cell "16.4 km/h" [ref=e185]
            - cell "87%" [ref=e186]
            - cell "2/5" [ref=e187]
          - row "60 09/08/2026, 13:44 32.05805 34.77002 18.8 km/h 82% 3/5" [ref=e188]:
            - cell "60" [ref=e189]
            - cell "09/08/2026, 13:44" [ref=e190]
            - cell "32.05805" [ref=e191]
            - cell "34.77002" [ref=e192]
            - cell "18.8 km/h" [ref=e193]
            - cell "82%" [ref=e194]
            - cell "3/5" [ref=e195]
```

# Test source

```ts
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
  56  |     expect(pager.totalPages, bugMessage('BUG-01', `pages must be ceil(${pager.total}/5)`)).toBe(expectedTotalPages(pager.total, environment.uiPageSize));
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
> 117 |     expect(await consolePage.trail.dateOfBirth(), bugMessage('BUG-04', 'header DOB must equal the stored DOB')).toBe(isoDateToDdMmYyyy(stored.dateOfBirth));
      |                                                                                                                 ^ Error: [BUG-04] header DOB must equal the stored DOB
  118 |   });
  119 | });
  120 | 
```