import { readFile } from 'node:fs/promises';
import { test, expect } from '@playwright/test';

const root = new URL('../Voba.Backend/wwwroot/', import.meta.url);
const assets = new Map(await Promise.all([
  ['/demo', 'index.html', 'text/html'],
  ['/demo.css', 'demo.css', 'text/css'],
  ['/demo.mjs', 'demo.mjs', 'text/javascript'],
  ['/api.mjs', 'api.mjs', 'text/javascript']
].map(async ([path, file, contentType]) =>
  [path, { body: await readFile(new URL(file, root)), contentType }])));

const unsafeName = '<img src=x onerror="window.__xss = true">';
const unsafeInstructions = '<svg onload="window.__xss = true"> Cook beans.';
const option = {
  optionId: 'option-one', name: unsafeName, ingredients: [unsafeName, 'beans'],
  estimatedCost: 1.5, totalCost: 3, nutrition: null,
  costSource: 'Synthetic', nutritionSource: 'Synthetic'
};
const full = {
  draftId: 'draft-one', draftVersion: 1, title: unsafeName,
  instructions: unsafeInstructions, selectedOption: option,
  servings: 2, budget: 20, dietaryRestrictions: ['vegan'],
  cuisinePreference: 'Italian'
};
const saved = {
  id: 'recipe-one', title: unsafeName,
  ingredients: [{ name: unsafeName, amount: 1, unit: 'cup' }],
  totalCost: 3, instructions: unsafeInstructions, nutrition: null,
  costSource: 'Synthetic', nutritionSource: 'Synthetic',
  savedAtUtc: '2026-10-10T00:00:00Z', servings: 2, budget: 20,
  dietaryRestrictions: ['vegan'], cuisinePreference: 'Italian'
};

test('model text stays inert and pending operations block overlap with 409 recovery', async ({ page }, testInfo) => {
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  let selectRoute;
  let saveRoute;
  let selectCount = 0;
  let saveCount = 0;
  let optionCount = 0;
  let savedRecipes = [];
  let resolveSelect;
  let resolveSave;
  const selectArrived = new Promise(resolve => { resolveSelect = resolve; });
  const saveArrived = new Promise(resolve => { resolveSave = resolve; });

  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const asset = assets.get(path);
    if (asset)
      return route.fulfill({ status: 200, ...asset });

    const reply = (status, body) => route.fulfill({ status,
      contentType: 'application/json', body: JSON.stringify(body) });
    if (path === '/api/auth/login')
      return reply(200, { userId: 'user-one', accessToken: 'access-one',
        refreshToken: 'refresh-one' });
    if (path === '/api/auth/logout')
      return route.fulfill({ status: 204 });
    if (path === '/api/recipes' && route.request().method() === 'GET')
      return reply(200, savedRecipes);
    if (path === '/api/generation/options') {
      optionCount++;
      return reply(200, { draftId: 'draft-one', options: [option] });
    }
    if (path === '/api/generation/drafts/draft-one/select') {
      selectCount++;
      if (selectCount === 1) {
        selectRoute = route;
        resolveSelect();
        return;
      }
      return reply(200, full);
    }
    if (path === '/api/recipes' && route.request().method() === 'POST') {
      saveCount++;
      if (saveCount === 1) {
        saveRoute = route;
        resolveSave();
        return;
      }
      savedRecipes = [saved];
      return reply(201, saved);
    }
    return reply(404, { code: 'missing', message: 'Mock route missing.' });
  });

  await page.goto('/demo');
  await page.getByLabel('Email').fill('mock@example.invalid');
  await page.getByLabel('Password').fill('mock-password');
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page.getByRole('button', { name: 'Find recipes' })).toBeVisible();
  await page.getByRole('button', { name: 'Find recipes' }).click();
  await expect(page.getByTestId('options')).toContainText(unsafeName);
  expect(await page.locator('#options-list img, #options-list svg').count()).toBe(0);
  expect(await page.evaluate(() => window.__xss ?? false)).toBe(false);

  await page.getByRole('button', { name: 'Choose recipe' }).click();
  await selectArrived;
  await expect(page.getByRole('button', { name: 'Choose recipe' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Find recipes' })).toBeDisabled();
  await expect(page.locator('#save-recipe')).toBeDisabled();
  expect(optionCount).toBe(1);
  await selectRoute.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify(full) });
  await expect(page.getByTestId('recipe')).toContainText(unsafeInstructions);
  expect(await page.locator('#recipe img, #recipe svg').count()).toBe(0);
  expect(await page.evaluate(() => window.__xss ?? false)).toBe(false);

  await page.getByRole('button', { name: 'Save recipe' }).click();
  await saveArrived;
  await expect(page.getByRole('button', { name: 'Save recipe' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Choose recipe' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Find recipes' })).toBeDisabled();
  await saveRoute.fulfill({ status: 409, contentType: 'application/json',
    body: JSON.stringify({ code: 'stale', message: 'Selection changed.' }) });
  await expect(page.getByTestId('status')).toContainText('Choose recipe again');
  await expect(page.getByRole('button', { name: 'Find recipes' })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Choose recipe' })).toBeEnabled();
  await page.getByRole('button', { name: 'Choose recipe' }).click();
  await expect(page.getByRole('button', { name: 'Save recipe' })).toBeEnabled();
  await page.getByRole('button', { name: 'Save recipe' }).click();
  await expect(page.getByTestId('saved-list')).toContainText(unsafeName);
  expect(optionCount).toBe(1);
  expect(selectCount).toBe(2);
  expect(saveCount).toBe(2);
  expect(await page.evaluate(() => window.__xss ?? false)).toBe(false);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByTestId('saved-list')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('mobile-saved.png'), fullPage: true });
  expect(pageErrors).toEqual([]);
});
