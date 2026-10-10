import { mkdir, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';

const base = process.env.VOBA_WEB_BASE_URL ?? 'http://127.0.0.1:5057';
const email = `voba-web-${randomUUID()}@example.invalid`;
const password = 'Voba-Dummy-2026!';
const steps = [];
let accessToken;
let loggedOut = false;

async function request(method, path, body, expected) {
  const response = await fetch(new URL(path, base), {
    method,
    signal: AbortSignal.timeout(300_000),
    headers: {
      ...(body === undefined ? {} : { 'content-type': 'application/json' }),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {})
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const result = response.status === 204 ? null : await response.json().catch(() => null);
  steps.push({ method, path: path.replace(/\/[0-9a-f]{24,}/g, '/:id'), status: response.status });
  if (response.status !== expected)
    throw new Error(`${method} ${path} returned ${response.status}; expected ${expected}`);
  return result;
}

try {
  await request('POST', '/api/auth/register', {
    email, username: 'Voba Web Demo', password
  }, 201);
  await request('POST', '/api/auth/login', { email, password: 'wrong-password' }, 401);
  const tokens = await request('POST', '/api/auth/login', { email, password }, 200);
  accessToken = tokens.accessToken;
  const options = await request('POST', '/api/generation/options', {
    budget: 20, servings: 2, dietaryRestrictions: ['vegan'], cuisinePreference: 'Italian'
  }, 200);
  if (!options?.draftId || !options.options?.length)
    throw new Error('Generation returned no recipe options.');
  if (options.options.some(option => option.costSource !== 'Synthetic' ||
      option.nutritionSource !== 'Synthetic'))
    throw new Error('Synthetic enrichment source label mismatch.');
  const selected = await request('POST',
    `/api/generation/drafts/${options.draftId}/select`,
    { optionId: options.options[0].optionId }, 200);
  const saved = await request('POST', '/api/recipes', {
    draftId: selected.draftId, draftVersion: selected.draftVersion
  }, 201);
  if (!saved.instructions || !saved.ingredients?.length ||
      saved.costSource !== 'Synthetic' || saved.nutritionSource !== 'Synthetic' ||
      saved.servings !== 2 || saved.budget !== 20 ||
      !saved.dietaryRestrictions?.some(value => value.toLowerCase() === 'vegan'))
    throw new Error('Saved recipe metadata or provenance mismatch.');
  const list = await request('GET', '/api/recipes', undefined, 200);
  if (!list.some(recipe => recipe.id === saved.id))
    throw new Error('Saved recipe missing from list.');
  const reopened = await request('GET', `/api/recipes/${saved.id}`, undefined, 200);
  if (reopened.id !== saved.id || reopened.instructions !== saved.instructions)
    throw new Error('Reopened recipe differs from saved recipe.');
  await request('POST', '/api/auth/logout', undefined, 204);
  loggedOut = true;
  await request('GET', '/api/recipes', undefined, 401);

  const report = {
    success: true,
    base,
    account: email,
    requested: { budget: 20, servings: 2, dietaryRestrictions: ['vegan'], cuisinePreference: 'Italian' },
    options: options.options.map(option => ({
      name: option.name,
      ingredients: option.ingredients,
      costSource: option.costSource,
      nutritionSource: option.nutritionSource,
      estimatedCost: option.estimatedCost,
      totalCost: option.totalCost
    })),
    saved: {
      id: saved.id,
      title: saved.title,
      ingredients: saved.ingredients,
      instructions: saved.instructions,
      costSource: saved.costSource,
      nutritionSource: saved.nutritionSource,
      servings: saved.servings,
      budget: saved.budget,
      dietaryRestrictions: saved.dietaryRestrictions
    },
    steps
  };
  await mkdir(new URL('./walkthrough-results/', import.meta.url), { recursive: true });
  await writeFile(new URL('./walkthrough-results/api-walkthrough.json', import.meta.url),
    `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
} catch (error) {
  await mkdir(new URL('./walkthrough-results/', import.meta.url), { recursive: true });
  await writeFile(new URL('./walkthrough-results/api-walkthrough.json', import.meta.url),
    `${JSON.stringify({ success: false, base, account: email,
      error: error.message, steps }, null, 2)}\n`);
  process.stderr.write(`API walkthrough failed: ${error.message}\n`);
  process.exitCode = 1;
} finally {
  if (accessToken && !loggedOut) {
    await fetch(new URL('/api/auth/logout', base), {
      method: 'POST',
      headers: { authorization: `Bearer ${accessToken}` },
      signal: AbortSignal.timeout(10_000)
    }).catch(() => {});
  }
}
