import { expect, test } from '@playwright/test'

test('login workflow', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('login-email').fill('coach@example.com')
  await page.getByTestId('login-submit').click()

  await expect(page.getByTestId('login-status')).toHaveText(
    'Logged in as coach@example.com'
  )
})

test('create event workflow', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('event-name').fill('Field cleanup')
  await page.getByTestId('event-date').fill('2025-01-15')
  await page.getByTestId('event-submit').click()

  await expect(page.getByTestId('event-status')).toContainText(
    'Field cleanup'
  )
})

test('bulk import workflow', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('import-file').setInputFiles({
    name: 'slots.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from('division,offeringTeamId\nA,TEAM1')
  })

  await expect(page.getByTestId('import-status')).toContainText('slots.csv')
})
