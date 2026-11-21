# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api\zz-temp-rule-check.api.spec.ts >> ZZRULECHECK known-bug test failing WITHOUT its tag
- Location: tests\api\zz-temp-rule-check.api.spec.ts:4:5

# Error details

```
Error: something unrelated broke (no bug tag)

expect(received).toBe(expected) // Object.is equality

Expected: 2
Received: 1
```

# Test source

```ts
  1  | import { test, expect } from '@fixtures/test.fixtures';
  2  | import { knownBug } from '@helpers/known-bug';
  3  | // TEMPORARY — verifies the platform reporter's classification rule; deleted right after the check.
  4  | test('ZZRULECHECK known-bug test failing WITHOUT its tag', async () => {
  5  |   knownBug('BUG-01');
> 6  |   expect(1, 'something unrelated broke (no bug tag)').toBe(2);
     |                                                       ^ Error: something unrelated broke (no bug tag)
  7  | });
  8  | test('ZZRULECHECK known-bug test failing WITH its tag', async () => {
  9  |   knownBug('BUG-01');
  10 |   expect(1, '[BUG-01] the documented defect').toBe(2);
  11 | });
  12 | 
```