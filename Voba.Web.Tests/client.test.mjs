import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient } from '../Voba.Backend/wwwroot/api.mjs';

const json = (data, status = 200) => Response.json(data, { status });

test('concurrent expired requests share one refresh and use rotated token', async () => {
  let refreshCalls = 0;
  const fetcher = async (path, init) => {
    if (path === '/api/auth/login')
      return json({ userId: 'a', accessToken: 'old', refreshToken: 'r1' });
    if (path === '/api/auth/refresh') {
      refreshCalls++;
      return json({ userId: 'a', accessToken: 'fresh', refreshToken: 'r2' });
    }
    if (path === '/api/generation/options')
      return init.headers.Authorization === 'Bearer old'
        ? json({ code: 'expired' }, 401)
        : json({ draftId: 'draft', options: [] });
    throw new Error(`Unexpected path ${path}`);
  };
  const api = createApiClient(fetcher);
  await api.login('a@example.invalid', 'password');

  const [first, second] = await Promise.all([
    api.options({ budget: 20, servings: 2, dietaryRestrictions: [] }),
    api.options({ budget: 20, servings: 2, dietaryRestrictions: [] })
  ]);

  assert.equal(first.draftId, 'draft');
  assert.equal(second.draftId, 'draft');
  assert.equal(refreshCalls, 1);
  assert.equal(api.session()?.accessToken, 'fresh');
});

test('refresh finishing after another login cannot replace new session', async () => {
  let releaseRefresh;
  let refreshEntered;
  const entered = new Promise(resolve => { refreshEntered = resolve; });
  const release = new Promise(resolve => { releaseRefresh = resolve; });
  let loginCount = 0;
  const fetcher = async (path, init) => {
    if (path === '/api/auth/login') {
      loginCount++;
      return loginCount === 1
        ? json({ userId: 'a', accessToken: 'old', refreshToken: 'r1' })
        : json({ userId: 'b', accessToken: 'new', refreshToken: 'r-new' });
    }
    if (path === '/api/auth/refresh') {
      refreshEntered();
      await release;
      return json({ userId: 'a', accessToken: 'stale', refreshToken: 'r2' });
    }
    if (path === '/api/generation/options')
      return json({ code: 'expired' }, 401);
    throw new Error(`Unexpected path ${path}`);
  };
  const api = createApiClient(fetcher);
  await api.login('a@example.invalid', 'password');
  const pending = api.options({ budget: 20, servings: 2, dietaryRestrictions: [] });
  await entered;
  await api.login('b@example.invalid', 'password');
  releaseRefresh();

  await assert.rejects(pending);
  assert.equal(api.session()?.userId, 'b');
  assert.equal(api.session()?.accessToken, 'new');
});

test('logout revokes token and clears local state even when request fails', async () => {
  let sentToken;
  const fetcher = async (path, init) => {
    if (path === '/api/auth/login')
      return json({ userId: 'a', accessToken: 'access', refreshToken: 'refresh' });
    if (path === '/api/auth/logout') {
      sentToken = init.headers.Authorization;
      return json({ code: 'unavailable', message: 'Backend unavailable.' }, 503);
    }
    throw new Error(`Unexpected path ${path}`);
  };
  const api = createApiClient(fetcher);
  await api.login('a@example.invalid', 'password');

  await assert.rejects(() => api.logout());
  assert.equal(sentToken, 'Bearer access');
  assert.equal(api.session(), null);
});

test('older login response cannot replace newer login', async () => {
  let releaseFirst;
  const firstResponse = new Promise(resolve => { releaseFirst = resolve; });
  const fetcher = async (path, init) => {
    if (path !== '/api/auth/login')
      throw new Error(`Unexpected path ${path}`);
    const email = JSON.parse(init.body).email;
    if (email === 'a@example.invalid') {
      await firstResponse;
      return json({ userId: 'a', accessToken: 'old', refreshToken: 'r-old' });
    }
    return json({ userId: 'b', accessToken: 'new', refreshToken: 'r-new' });
  };
  const api = createApiClient(fetcher);

  const older = api.login('a@example.invalid', 'password');
  await api.login('b@example.invalid', 'password');
  releaseFirst();

  await assert.rejects(older);
  assert.equal(api.session()?.userId, 'b');
});

test('logout during pending login prevents session revival', async () => {
  let releaseLogin;
  const loginResponse = new Promise(resolve => { releaseLogin = resolve; });
  const fetcher = async path => {
    if (path !== '/api/auth/login')
      throw new Error(`Unexpected path ${path}`);
    await loginResponse;
    return json({ userId: 'a', accessToken: 'late', refreshToken: 'r-late' });
  };
  const api = createApiClient(fetcher);

  const pending = api.login('a@example.invalid', 'password');
  await api.logout();
  releaseLogin();

  await assert.rejects(pending);
  assert.equal(api.session(), null);
});

test('response decoded after logout cannot return old account data', async () => {
  let releaseBody;
  let bodyEntered;
  const entered = new Promise(resolve => { bodyEntered = resolve; });
  const released = new Promise(resolve => { releaseBody = resolve; });
  const fetcher = async path => {
    if (path === '/api/auth/login')
      return json({ userId: 'a', accessToken: 'access', refreshToken: 'refresh' });
    if (path === '/api/auth/logout')
      return new Response(null, { status: 204 });
    if (path === '/api/recipes')
      return {
        status: 200,
        ok: true,
        json: async () => {
          bodyEntered();
          await released;
          return [{ title: 'Old account recipe' }];
        }
      };
    throw new Error(`Unexpected path ${path}`);
  };
  const api = createApiClient(fetcher);
  await api.login('a@example.invalid', 'password');

  const pending = api.list();
  await entered;
  await api.logout();
  releaseBody();

  await assert.rejects(pending);
  assert.equal(api.session(), null);
});
