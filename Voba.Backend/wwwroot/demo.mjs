import { ApiError, createApiClient } from '/api.mjs';

const api = createApiClient();
const byId = id => document.getElementById(id);
const auth = byId('auth');
const planner = byId('planner');
const welcome = byId('welcome');
const optionsPanel = byId('options');
const recipePanel = byId('recipe');
const savedPanel = byId('saved-list');
const status = byId('status');
const logoutButton = byId('logout');
let authMode = 'login';
let draftId = null;
let fullRecipe = null;
let viewRevision = 0;
let selectionBusy = false;
let savingBusy = false;

function syncWorkflowButtons() {
  const busy = selectionBusy || savingBusy;
  byId('find-recipes').disabled = busy;
  byId('save-recipe').disabled = busy;
  for (const button of byId('options-list').querySelectorAll('button'))
    button.disabled = busy;
}

function notice(message, error = false) {
  status.textContent = message;
  status.classList.toggle('error', error);
  status.hidden = !message;
}

function errorMessage(error) {
  if (error?.name === 'TimeoutError')
    return 'Request timed out. Check the backend and try again.';
  if (error instanceof TypeError)
    return 'Backend unavailable. Start the Voba backend and try again.';
  return error?.message ?? 'Request failed. Try again.';
}

function handleError(error) {
  if (error instanceof ApiError && error.code === 'session_changed')
    return;
  if (error instanceof ApiError && error.status === 401 && !api.session())
    showGuest();
  notice(errorMessage(error), true);
}

async function withButton(button, message, action) {
  const revision = viewRevision;
  button.disabled = true;
  notice(message);
  try {
    await action();
  } catch (error) {
    if (revision === viewRevision)
      handleError(error);
  } finally {
    button.disabled = false;
  }
}

function showGuest() {
  viewRevision++;
  selectionBusy = false;
  savingBusy = false;
  syncWorkflowButtons();
  auth.hidden = false;
  planner.hidden = true;
  welcome.hidden = false;
  optionsPanel.hidden = true;
  recipePanel.hidden = true;
  savedPanel.hidden = true;
  logoutButton.hidden = true;
  byId('password').value = '';
  draftId = null;
  fullRecipe = null;
}

function showKitchen() {
  viewRevision++;
  auth.hidden = true;
  planner.hidden = false;
  welcome.hidden = true;
  savedPanel.hidden = false;
  logoutButton.hidden = false;
  byId('password').value = '';
}

function setAuthMode(mode) {
  authMode = mode;
  const register = mode === 'register';
  byId('name-field').hidden = !register;
  byId('name').required = register;
  byId('password').autocomplete = register ? 'new-password' : 'current-password';
  byId('auth-submit').textContent = register ? 'Create account' : 'Log in';
  byId('show-register').classList.toggle('active', register);
  byId('show-login').classList.toggle('active', !register);
  byId('show-register').setAttribute('aria-pressed', String(register));
  byId('show-login').setAttribute('aria-pressed', String(!register));
  notice('');
}

function sourceLabel(costSource, nutritionSource, nutrition) {
  const label = source => source === 'Real' ? 'Spoonacular live'
    : source === 'Synthetic' ? 'demo data' : 'Gemma estimate';
  return `Cost: ${label(costSource)} · Nutrition: ${nutrition ? label(nutritionSource) : 'unavailable'}`;
}

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className)
    node.className = className;
  if (text !== undefined)
    node.textContent = text;
  return node;
}

function money(value) {
  const amount = Number(value);
  return Number.isFinite(amount) && amount > 0
    ? `$${amount.toFixed(2)}` : 'Price unavailable';
}

function addChip(container, text) {
  if (text !== undefined && text !== null && text !== '')
    container.append(element('span', '', String(text)));
}

function renderOptions(options) {
  const list = byId('options-list');
  list.replaceChildren();
  for (const option of options.options ?? []) {
    const card = element('article', 'option-card');
    card.append(element('h3', '', option.name ?? 'Recipe'));
    card.append(element('p', '', (option.ingredients ?? []).join(', ')));
    const cost = element('div', 'cost-line');
    cost.append(element('strong', '', money(option.totalCost)));
    cost.append(element('span', '', 'total for your servings'));
    card.append(cost);
    card.append(element('p', 'source', sourceLabel(option.costSource,
      option.nutritionSource, option.nutrition)));
    const choose = element('button', '', 'Choose recipe');
    choose.type = 'button';
    choose.addEventListener('click', () => chooseRecipe(option, choose));
    card.append(choose);
    list.append(card);
  }
  syncWorkflowButtons();
  optionsPanel.hidden = false;
}

function renderRecipe(recipe, saved) {
  byId('recipe-name').textContent = recipe.title ?? 'Recipe';
  const meta = byId('recipe-meta');
  meta.replaceChildren();
  const option = saved ? null : recipe.selectedOption;
  const nutrition = option?.nutrition ?? recipe.nutrition;
  addChip(meta, money(option?.totalCost ?? recipe.totalCost));
  addChip(meta, recipe.servings ? `${recipe.servings} servings` : null);
  addChip(meta, recipe.budget ? `${money(recipe.budget)} budget` : null);
  addChip(meta, recipe.cuisinePreference);
  for (const restriction of recipe.dietaryRestrictions ?? [])
    addChip(meta, restriction);
  addChip(meta, sourceLabel(option?.costSource ?? recipe.costSource,
    option?.nutritionSource ?? recipe.nutritionSource,
    nutrition));
  if (nutrition?.calories)
    addChip(meta, `${Math.round(nutrition.calories)} kcal per serving`);
  const ingredients = byId('recipe-ingredients');
  ingredients.replaceChildren();
  for (const ingredient of option?.ingredients ?? recipe.ingredients ?? []) {
    const text = typeof ingredient === 'string' ? ingredient
      : [ingredient.amount > 0 ? ingredient.amount : '', ingredient.unit, ingredient.name]
        .filter(Boolean).join(' ');
    ingredients.append(element('li', '', text));
  }
  byId('recipe-instructions').textContent = recipe.instructions ?? '';
  byId('save-recipe').hidden = saved;
  recipePanel.hidden = false;
}

function renderSaved(recipes) {
  const list = byId('saved-items');
  list.replaceChildren();
  byId('saved-empty').hidden = recipes.length > 0;
  for (const recipe of recipes) {
    const card = element('article', 'saved-card');
    card.append(element('h3', '', recipe.title ?? 'Recipe'));
    card.append(element('p', '', `${money(recipe.totalCost)} · ${recipe.ingredients?.length ?? 0} ingredients`));
    card.append(element('p', 'source', sourceLabel(recipe.costSource,
      recipe.nutritionSource, recipe.nutrition)));
    const open = element('button', '', 'Open recipe');
    open.type = 'button';
    open.addEventListener('click', () => withButton(open, 'Opening saved recipe…', async () => {
      const revision = viewRevision;
      const current = await api.get(recipe.id);
      if (revision !== viewRevision)
        return;
      fullRecipe = null;
      renderRecipe(current, true);
      notice('Saved recipe opened.');
      recipePanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }));
    card.append(open);
    list.append(card);
  }
}

async function loadSaved() {
  const revision = viewRevision;
  const recipes = await api.list();
  if (revision === viewRevision)
    renderSaved(recipes);
}

async function chooseRecipe(option, button) {
  if (!draftId || selectionBusy || savingBusy)
    return;
  selectionBusy = true;
  const selectedDraft = draftId;
  const revision = viewRevision;
  syncWorkflowButtons();
  try {
    await withButton(button, 'Writing cooking steps with local Gemma…', async () => {
      const selected = await api.select(selectedDraft, option.optionId);
      if (revision !== viewRevision || draftId !== selectedDraft)
        return;
      if (!selected.instructions?.trim())
        throw new Error('No cooking steps returned. Choose recipe again.');
      fullRecipe = selected;
      renderRecipe(selected, false);
      notice('Cooking steps ready. Save recipe to keep it.');
      recipePanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  } finally {
    if (revision === viewRevision) {
      selectionBusy = false;
      syncWorkflowButtons();
    }
  }
}

byId('show-login').addEventListener('click', () => setAuthMode('login'));
byId('show-register').addEventListener('click', () => setAuthMode('register'));

byId('auth-form').addEventListener('submit', async event => {
  event.preventDefault();
  const button = byId('auth-submit');
  const email = byId('email').value.trim();
  const password = byId('password').value;
  const name = byId('name').value.trim();
  const revision = viewRevision;
  await withButton(button, authMode === 'register' ? 'Creating account…' : 'Logging in…', async () => {
    if (authMode === 'register') {
      await api.register(email, name, password);
      if (revision !== viewRevision)
        return;
      setAuthMode('login');
      notice('Account created. Logging in…');
    }
    await api.login(email, password);
    if (revision !== viewRevision)
      return;
    showKitchen();
    notice('Ready to plan dinner.');
    try {
      await loadSaved();
    } catch (error) {
      if (api.session())
        notice(`Logged in. Saved recipes unavailable: ${errorMessage(error)}`, true);
      else
        handleError(error);
    }
  });
});

byId('plan-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (selectionBusy || savingBusy)
    return;
  const button = byId('find-recipes');
  const budget = Number(byId('budget').value);
  const servings = Number(byId('servings').value);
  const dietaryRestrictions = [
    ...document.querySelectorAll('input[name="diet"]:checked')
  ].map(input => input.value);
  dietaryRestrictions.push(...byId('restrictions').value.split(',').map(value => value.trim()).filter(Boolean));
  const unique = [...new Map(dietaryRestrictions.map(value => [value.toLowerCase(), value])).values()];
  if (!Number.isFinite(budget) || budget <= 0 || budget > 1000000 ||
      !Number.isInteger(servings) || servings < 1 || servings > 1000 ||
      unique.length > 32 || unique.some(value => value.length > 200)) {
    notice('Use a positive budget up to $1,000,000, 1–1,000 servings, and up to 32 food needs.', true);
    return;
  }
  viewRevision++;
  const revision = viewRevision;
  draftId = null;
  fullRecipe = null;
  optionsPanel.hidden = true;
  recipePanel.hidden = true;
  await withButton(button, 'Finding recipes with local Gemma…', async () => {
    const result = await api.options({
      budget, servings, dietaryRestrictions: unique,
      cuisinePreference: byId('cuisine').value.trim() || null
    });
    if (revision !== viewRevision)
      return;
    if (!result.options?.length)
      throw new Error('No recipes fit. Try a larger budget or fewer food needs.');
    draftId = result.draftId;
    fullRecipe = null;
    recipePanel.hidden = true;
    renderOptions(result);
    notice(`${result.options.length} recipe choices ready.`);
    optionsPanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
});

byId('save-recipe').addEventListener('click', async () => {
  if (!fullRecipe || savingBusy || selectionBusy)
    return;
  savingBusy = true;
  const recipe = fullRecipe;
  const revision = viewRevision;
  syncWorkflowButtons();
  try {
    await withButton(byId('save-recipe'), 'Saving recipe…', async () => {
      try {
        const saved = await api.save(recipe.draftId, recipe.draftVersion);
        if (revision !== viewRevision || fullRecipe !== recipe)
          return;
        fullRecipe = null;
        renderRecipe(saved, true);
        await loadSaved();
        if (revision !== viewRevision)
          return;
        notice('Recipe saved. Open it from Saved recipes anytime during this session.');
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) {
          if (revision !== viewRevision)
            return;
          fullRecipe = null;
          recipePanel.hidden = true;
          notice('Selection changed. Choose recipe again before saving.', true);
          return;
        }
        throw error;
      }
    });
  } finally {
    if (revision === viewRevision) {
      savingBusy = false;
      syncWorkflowButtons();
    }
  }
});

logoutButton.addEventListener('click', async () => {
  showGuest();
  await withButton(logoutButton, 'Logging out…', async () => {
    try {
      await api.logout();
      notice('Logged out.');
    } catch (error) {
      notice('Logged out locally. Backend session could not be revoked.', true);
    }
  });
});

showGuest();
