# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ui\03-offender-crud.ui.spec.ts >> UI · create, edit, delete offender >> TC-22 changing risk level in the form persists the shown value
- Location: tests\ui\03-offender-crud.ui.spec.ts:90:7

# Error details

```
Error: [BUG-03] saved risk level must equal the value shown in the form

expect(received).toBe(expected) // Object.is equality

Expected: "Medium"
Received: "Low"
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
        - generic [ref=e15]: "639"
        - text: Trail points
  - generic [ref=e16]: Offender updated
  - generic [ref=e17]:
    - generic [ref=e18]:
      - generic [ref=e19]:
        - textbox "Search name or national ID…" [ref=e20]: "998526265"
        - button "+ Add Offender" [ref=e21] [cursor=pointer]
      - table [ref=e22]:
        - rowgroup [ref=e23]:
          - row "Name National ID Date of birth Risk Status" [ref=e24]:
            - columnheader "Name" [ref=e25]
            - columnheader "National ID" [ref=e26]
            - columnheader "Date of birth" [ref=e27]
            - columnheader "Risk" [ref=e28]
            - columnheader "Status" [ref=e29]
            - columnheader [ref=e30]
        - rowgroup [ref=e31]:
          - row "Barak, Ron 998526265 17/03/1985 Low Active Edit Delete" [ref=e32] [cursor=pointer]:
            - cell "Barak, Ron" [ref=e33]
            - cell "998526265" [ref=e34]
            - cell "17/03/1985" [ref=e35]
            - cell "Low" [ref=e36]:
              - generic [ref=e37]: Low
            - cell "Active" [ref=e38]:
              - generic [ref=e39]: Active
            - cell "Edit Delete" [ref=e40]:
              - button "Edit" [ref=e41]
              - button "Delete" [ref=e42]
      - generic [ref=e43]:
        - button "‹ Prev" [disabled] [ref=e44]
        - generic [ref=e45]: Page 1 of 1 (1 results)
        - button "Next ›" [disabled] [ref=e46]
    - generic [ref=e48]:
      - generic [ref=e49]: 🗺️
      - paragraph [ref=e50]: Select an offender to view their trail
```

# Test source

```ts
  3   | import { bugMessage, knownBug } from '@helpers/known-bug';
  4   | 
  5   | /**
  6   |  * FR-03 / FR-04 / FR-05 / FR-11 — create, edit and delete through the console.
  7   |  */
  8   | test.describe('UI · create, edit, delete offender', () => {
  9   |   /**
  10  |    * TC-12 · FR-03, FR-11
  11  |    * WHAT : creating a valid offender through the Add Offender form saves it, shows a success
  12  |    *        message, lists it with the entered values and raises the top-bar counters by one.
  13  |    * URL  : UI {BASE_URL}  "+ Add Offender" → Create  →  POST {BASE_URL}api/offenders
  14  |    */
  15  |   test('TC-12 operator creates a valid offender', { tag: ['@TC-12', '@FR-03', '@FR-11', '@ui'] }, async ({ consolePage, testData }) => {
  16  |     const input = validOffender({ riskLevel: 'High', status: 'Active' });
  17  |     await consolePage.open();
  18  |     const before = await consolePage.topBar.readStats();
  19  | 
  20  |     await consolePage.clickAddOffender();
  21  |     await consolePage.form.fill(input);
  22  |     const outcome = await consolePage.form.submitCreate();
  23  |     await testData.register(outcome.responseBody);
  24  | 
  25  |     expect(outcome.status).toBe(201);
  26  |     await expect(consolePage.toast()).toHaveText('Offender created');
  27  |     await expect.poll(() => consolePage.topBar.readStats()).toMatchObject({
  28  |       totalOffenders: before.totalOffenders + 1,
  29  |       activeOffenders: before.activeOffenders + 1,
  30  |     });
  31  |     await consolePage.list.search(input.nationalId!);
  32  |     expect(await consolePage.list.readRow(input.lastName!, input.firstName!)).toMatchObject({
  33  |       nationalId: input.nationalId,
  34  |       risk: input.riskLevel,
  35  |       status: input.status,
  36  |     });
  37  |   });
  38  | 
  39  |   /**
  40  |    * TC-19 · FR-03 (negative)
  41  |    * WHAT : submitting an empty Add Offender form is rejected with a clear error message, the form
  42  |    *        stays open and nothing is saved (offender counter unchanged).
  43  |    * URL  : UI {BASE_URL}  "+ Add Offender" → Create (empty)  →  POST {BASE_URL}api/offenders → 400
  44  |    */
  45  |   test('TC-19 empty create form shows an error and saves nothing', { tag: ['@TC-19', '@FR-03', '@ui', '@negative'] }, async ({ consolePage, api, testData }) => {
  46  |     await consolePage.open();
  47  |     const before = await api.getStatsOrThrow();
  48  | 
  49  |     await consolePage.clickAddOffender();
  50  |     const outcome = await consolePage.form.submitCreate();
  51  |     await testData.register(outcome.responseBody);
  52  | 
  53  |     expect(outcome.status).toBe(400);
  54  |     await expect(consolePage.errorBanner()).toContainText('required');
  55  |     expect(await consolePage.form.isOpen(), 'form must stay open for correction').toBe(true);
  56  |     expect((await api.getStatsOrThrow()).totalOffenders).toBe(before.totalOffenders);
  57  |   });
  58  | 
  59  |   /**
  60  |    * TC-21 · FR-04
  61  |    * WHAT : editing ONE field (first name) keeps every untouched field — in particular a High risk
  62  |    *        level — unchanged after Save.
  63  |    * URL  : UI {BASE_URL}  Edit → Save  →  PUT {BASE_URL}api/offenders/{id}   (verified with GET {BASE_URL}api/offenders/{id})
  64  |    */
  65  |   test('TC-21 editing the first name keeps the other fields unchanged', { tag: ['@TC-21', '@FR-04', '@ui'] }, async ({ consolePage, api, testData }, testInfo) => {
  66  |     knownBug('BUG-03');
  67  |     const offender = await testData.createOffender({ riskLevel: 'High', status: 'Active' });
  68  |     await consolePage.open();
  69  |     await consolePage.list.search(offender.nationalId);
  70  | 
  71  |     await consolePage.list.clickEdit(offender.lastName, offender.firstName);
  72  |     expect(await consolePage.form.read()).toMatchObject({ riskLevel: 'High' }); // form shows the stored value
  73  |     const newFirstName = differentFirstName(offender.firstName);
  74  |     await consolePage.form.fill({ firstName: newFirstName });
  75  |     const outcome = await consolePage.form.submitSave();
  76  |     await testInfo.attach('PUT request sent by the UI', { body: JSON.stringify(outcome.requestBody, null, 2), contentType: 'application/json' });
  77  | 
  78  |     const stored = await api.getOffenderOrThrow(offender.id);
  79  |     expect(stored.firstName).toBe(newFirstName);
  80  |     expect(stored.riskLevel, bugMessage('BUG-03', 'untouched risk level must stay High')).toBe('High');
  81  |     expect({ ...stored, firstName: offender.firstName }).toMatchObject({ ...offender });
  82  |   });
  83  | 
  84  |   /**
  85  |    * TC-22 · FR-04
  86  |    * WHAT : the values shown in the form are exactly what gets saved — changing Risk level to
  87  |    *        Medium in the form persists Medium.
  88  |    * URL  : UI {BASE_URL}  Edit → Risk level = Medium → Save  →  PUT {BASE_URL}api/offenders/{id}
  89  |    */
  90  |   test('TC-22 changing risk level in the form persists the shown value', { tag: ['@TC-22', '@FR-04', '@ui'] }, async ({ consolePage, api, testData }, testInfo) => {
  91  |     knownBug('BUG-03');
  92  |     const offender = await testData.createOffender({ riskLevel: 'High' });
  93  |     await consolePage.open();
  94  |     await consolePage.list.search(offender.nationalId);
  95  | 
  96  |     await consolePage.list.clickEdit(offender.lastName, offender.firstName);
  97  |     await consolePage.form.fill({ riskLevel: 'Medium' });
  98  |     const shown = await consolePage.form.read();
  99  |     const outcome = await consolePage.form.submitSave();
  100 |     await testInfo.attach('PUT request sent by the UI', { body: JSON.stringify(outcome.requestBody, null, 2), contentType: 'application/json' });
  101 | 
  102 |     const stored = await api.getOffenderOrThrow(offender.id);
> 103 |     expect(stored.riskLevel, bugMessage('BUG-03', 'saved risk level must equal the value shown in the form')).toBe(shown.riskLevel);
      |                                                                                                               ^ Error: [BUG-03] saved risk level must equal the value shown in the form
  104 |     await expect(consolePage.list.row(offender.lastName, offender.firstName)).toContainText(shown.riskLevel!);
  105 |   });
  106 | 
  107 |   /**
  108 |    * TC-25 · FR-05
  109 |    * WHAT : Delete asks for confirmation; dismissing keeps the offender, confirming removes it
  110 |    *        from the roster and from the API.
  111 |    * URL  : UI {BASE_URL}  Delete → confirm dialog  →  DELETE {BASE_URL}api/offenders/{id}
  112 |    */
  113 |   test('TC-25 delete requires confirmation and removes the offender', { tag: ['@TC-25', '@FR-05', '@ui'] }, async ({ consolePage, api, testData }) => {
  114 |     const offender = await testData.createOffender();
  115 |     await consolePage.open();
  116 |     await consolePage.list.search(offender.nationalId);
  117 |     const row = consolePage.list.row(offender.lastName, offender.firstName);
  118 | 
  119 |     const dismissedPrompt = await consolePage.list.clickDelete(offender.lastName, offender.firstName, 'dismiss');
  120 |     expect(dismissedPrompt, 'a confirmation prompt must appear').toContain(`${offender.firstName} ${offender.lastName}`);
  121 |     await expect(row, 'dismissing must keep the offender').toBeVisible();
  122 | 
  123 |     const acceptedPrompt = await consolePage.list.clickDelete(offender.lastName, offender.firstName, 'accept');
  124 |     await testData.forget(offender.id);
  125 |     expect(acceptedPrompt).not.toBeNull();
  126 |     await expect(row).toHaveCount(0);
  127 |     await expect(consolePage.toast()).toHaveText('Offender deleted');
  128 |     const remaining = (await api.listAllOffendersOrThrow(offender.nationalId)).items;
  129 |     expect(remaining.map((o) => o.id)).not.toContain(offender.id);
  130 |   });
  131 | });
  132 | 
```