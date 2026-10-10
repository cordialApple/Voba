import { randomUUID } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
import { test, expect } from '@playwright/test';

const password = 'Voba-Dummy-2026!';

test('fresh account can generate, save, reopen, and log out', async ({ page }, testInfo) => {
  const browserErrors = [];
  page.on('pageerror', error => browserErrors.push(error.message));
  page.on('console', message => {
    if (message.type() === 'error' && !/401|Unauthorized/i.test(message.text()))
      browserErrors.push(message.text());
  });
  const email = `voba-browser-${randomUUID()}@example.invalid`;
  const response = await page.goto('/demo');
  expect(response?.status()).toBe(200);
  await expect(page.getByTestId('auth')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('01-auth.png'), fullPage: true });

  await page.getByRole('button', { name: 'New account' }).click();
  await page.getByLabel('Name').fill('Voba Browser Demo');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  const [registered] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/auth/register') &&
      result.request().method() === 'POST'),
    page.getByRole('button', { name: 'Create account' }).click()
  ]);
  expect(registered.status()).toBe(201);
  await expect(page.getByRole('button', { name: 'Find recipes' })).toBeVisible();
  const [firstLogout] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/auth/logout') &&
      result.request().method() === 'POST'),
    page.getByRole('button', { name: 'Log out' }).click()
  ]);
  expect(firstLogout.status()).toBe(204);

  await page.getByRole('button', { name: 'Existing account' }).click();
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('wrong-password');
  const [rejected] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/auth/login') &&
      result.request().method() === 'POST'),
    page.getByRole('button', { name: 'Log in' }).click()
  ]);
  expect(rejected.status()).toBe(401);
  await expect(page.getByTestId('auth')).toBeVisible();

  await page.getByLabel('Password').fill(password);
  const [loggedIn] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/auth/login') &&
      result.request().method() === 'POST'),
    page.getByRole('button', { name: 'Log in' }).click()
  ]);
  expect(loggedIn.status()).toBe(200);
  await expect(page.getByRole('button', { name: 'Find recipes' })).toBeVisible();
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);

  await page.getByLabel('Budget (USD)').fill('20');
  await page.getByLabel('Servings').fill('2');
  await page.getByLabel('Cuisine').fill('Italian');
  await page.getByLabel('Vegan').check();
  const [generated] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/generation/options') &&
      result.request().method() === 'POST', { timeout: 300_000 }),
    page.getByRole('button', { name: 'Find recipes' }).click()
  ]);
  expect(generated.status()).toBe(200);
  await expect(page.getByTestId('options')).toBeVisible();
  await expect(page.getByTestId('options')).toContainText(/synthetic|demo/i);
  await page.screenshot({ path: testInfo.outputPath('02-options.png'), fullPage: true });

  const [selected] = await Promise.all([
    page.waitForResponse(result => /\/api\/generation\/drafts\/[^/]+\/select$/.test(result.url()) &&
      result.request().method() === 'POST', { timeout: 300_000 }),
    page.getByRole('button', { name: 'Choose recipe' }).first().click()
  ]);
  expect(selected.status()).toBe(200);
  await expect(page.getByTestId('recipe')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('03-recipe.png'), fullPage: true });

  const [saved] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/recipes') &&
      result.request().method() === 'POST'),
    page.getByRole('button', { name: 'Save recipe' }).click()
  ]);
  expect(saved.status()).toBe(201);
  const savedRecipe = await saved.json();
  expect(savedRecipe.costSource).toBe('Synthetic');
  expect(savedRecipe.servings).toBe(2);
  expect(savedRecipe.budget).toBe(20);
  expect(savedRecipe.dietaryRestrictions.map(value => value.toLowerCase())).toContain('vegan');
  expect(savedRecipe.instructions.length).toBeGreaterThan(0);
  await expect(page.getByTestId('saved-list')).toBeVisible();
  await expect(page.getByTestId('saved-list')).toContainText(savedRecipe.title);
  await page.screenshot({ path: testInfo.outputPath('04-saved-list.png'), fullPage: true });

  const [reopened] = await Promise.all([
    page.waitForResponse(result => /\/api\/recipes\/[^/]+$/.test(result.url()) &&
      result.request().method() === 'GET'),
    page.getByRole('button', { name: 'Open recipe' }).first().click()
  ]);
  expect(reopened.status()).toBe(200);
  const reopenedRecipe = await reopened.json();
  expect(reopenedRecipe.id).toBe(savedRecipe.id);
  expect(reopenedRecipe.instructions).toBe(savedRecipe.instructions);
  await expect(page.getByTestId('recipe')).toBeVisible();
  await expect(page.getByTestId('recipe')).toContainText(savedRecipe.title);
  await page.screenshot({ path: testInfo.outputPath('05-reopened.png'), fullPage: true });

  const [loggedOut] = await Promise.all([
    page.waitForResponse(result => result.url().endsWith('/api/auth/logout') &&
      result.request().method() === 'POST'),
    page.getByRole('button', { name: 'Log out' }).click()
  ]);
  expect(loggedOut.status()).toBe(204);
  await expect(page.getByTestId('auth')).toBeVisible();
  await expect(page.getByTestId('saved-list')).toBeHidden();
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
  expect(browserErrors).toEqual([]);
  await writeFile(testInfo.outputPath('browser-walkthrough.json'),
    `${JSON.stringify({ account: email, saved: {
      id: savedRecipe.id,
      title: savedRecipe.title,
      ingredients: savedRecipe.ingredients,
      instructions: savedRecipe.instructions,
      costSource: savedRecipe.costSource,
      nutritionSource: savedRecipe.nutritionSource,
      servings: savedRecipe.servings,
      budget: savedRecipe.budget,
      dietaryRestrictions: savedRecipe.dietaryRestrictions
    } }, null, 2)}\n`);
});
