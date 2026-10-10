export class ApiError extends Error {
  constructor(status, code, message) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

export function createApiClient(fetcher = fetch) {
  let state = null;
  let nextEpoch = 0;
  let authIntent = 0;
  let refreshJob = null;

  async function read(response) {
    if (response.status === 204)
      return null;
    let body;
    try {
      body = await response.json();
    } catch {
      throw new ApiError(response.status, 'invalid_response', 'Backend returned an invalid response.');
    }
    if (!response.ok)
      throw new ApiError(response.status, body?.code ?? 'request_failed',
        body?.message ?? `Request failed (${response.status}).`);
    return body;
  }

  function send(path, method, body, token) {
    const headers = {};
    if (body !== undefined)
      headers['Content-Type'] = 'application/json';
    if (token)
      headers.Authorization = `Bearer ${token}`;
    return fetcher(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: 'omit',
      cache: 'no-store',
      signal: AbortSignal.timeout(path.startsWith('/api/generation/') ? 300000 : 30000)
    });
  }

  async function refresh(original) {
    if (!state || state.epoch !== original.epoch)
      throw new ApiError(401, 'session_changed', 'Session changed. Log in again.');
    if (state.accessToken !== original.accessToken)
      return state;
    if (refreshJob?.epoch === original.epoch)
      return refreshJob.promise;

    const current = state;
    const job = (async () => {
      const response = await send('/api/auth/refresh', 'POST',
        { refreshToken: current.refreshToken });
      if (response.status === 401) {
        if (state === current)
          state = null;
        throw new ApiError(401, 'session_expired', 'Session expired. Log in again.');
      }
      const tokens = await read(response);
      if (!tokens?.accessToken || !tokens?.refreshToken || !tokens?.userId)
        throw new ApiError(response.status, 'invalid_response', 'Backend returned an invalid session.');
      if (state !== current || tokens.userId !== current.userId)
        throw new ApiError(401, 'session_changed', 'Session changed. Log in again.');
      state = { ...current, accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken };
      return state;
    })();
    refreshJob = { epoch: original.epoch, promise: job };
    try {
      return await job;
    } finally {
      if (refreshJob?.promise === job)
        refreshJob = null;
    }
  }

  async function authorized(path, method = 'GET', body) {
    const original = state;
    if (!original)
      throw new ApiError(401, 'session_missing', 'Log in to continue.');
    let response = await send(path, method, body, original.accessToken);
    if (state?.epoch !== original.epoch)
      throw new ApiError(401, 'session_changed', 'Session changed. Log in again.');
    if (response.status === 401) {
      const current = await refresh(original);
      response = await send(path, method, body, current.accessToken);
      if (state?.epoch !== original.epoch)
        throw new ApiError(401, 'session_changed', 'Session changed. Log in again.');
      if (response.status === 401 && state === current)
        state = null;
    }
    const result = await read(response);
    if (state?.epoch !== original.epoch)
      throw new ApiError(401, 'session_changed', 'Session changed. Log in again.');
    return result;
  }

  return {
    session: () => state ? { ...state } : null,
    async register(email, username, password) {
      return read(await send('/api/auth/register', 'POST',
        { email, username, password }));
    },
    async login(email, password) {
      const intent = ++authIntent;
      const tokens = await read(await send('/api/auth/login', 'POST', { email, password }));
      if (!tokens?.accessToken || !tokens?.refreshToken || !tokens?.userId)
        throw new ApiError(200, 'invalid_response', 'Backend returned an invalid session.');
      if (intent !== authIntent)
        throw new ApiError(401, 'session_changed', 'Session changed. Log in again.');
      state = { ...tokens, email, epoch: ++nextEpoch };
      return { ...state };
    },
    async logout() {
      authIntent++;
      const original = state;
      if (!original)
        return;
      try {
        await authorized('/api/auth/logout', 'POST');
      } finally {
        if (state?.epoch === original.epoch)
          state = null;
      }
    },
    options: request => authorized('/api/generation/options', 'POST', request),
    select: (draftId, optionId) => authorized(
      `/api/generation/drafts/${encodeURIComponent(draftId)}/select`,
      'POST', { optionId }),
    save: (draftId, draftVersion) => authorized('/api/recipes', 'POST',
      { draftId, draftVersion }),
    list: () => authorized('/api/recipes'),
    get: recipeId => authorized(`/api/recipes/${encodeURIComponent(recipeId)}`),
    delete: recipeId => authorized(`/api/recipes/${encodeURIComponent(recipeId)}`, 'DELETE')
  };
}
