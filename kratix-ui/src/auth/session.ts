// "Remember me" keeps the session in localStorage (survives closing the browser);
// otherwise it lives in sessionStorage and ends when the browser is closed.
const TOKEN = 'authToken';
const USER = 'user';
const REMEMBERED_USERNAME = 'rememberedUsername';

const safe = <T>(fn: () => T, fallback: T): T => {
  try {
    return fn();
  } catch {
    return fallback;
  }
};

const read = (key: string) => safe(() => sessionStorage.getItem(key) ?? localStorage.getItem(key), null);

export const session = {
  token: () => read(TOKEN),

  isRemembered: () => safe(() => localStorage.getItem(TOKEN) !== null, false),

  user: <T>(): T | null => {
    const raw = read(USER);
    return raw ? safe(() => JSON.parse(raw) as T, null) : null;
  },

  save(token: string, user: unknown, remember: boolean) {
    this.clear();
    const store = remember ? localStorage : sessionStorage;
    safe(() => {
      store.setItem(TOKEN, token);
      store.setItem(USER, JSON.stringify(user));
    }, undefined);
  },

  clear() {
    safe(() => {
      for (const store of [localStorage, sessionStorage]) {
        store.removeItem(TOKEN);
        store.removeItem(USER);
      }
    }, undefined);
  },

  rememberedUsername: () => safe(() => localStorage.getItem(REMEMBERED_USERNAME) ?? '', ''),

  setRememberedUsername(username: string | null) {
    safe(() => (username ? localStorage.setItem(REMEMBERED_USERNAME, username) : localStorage.removeItem(REMEMBERED_USERNAME)), undefined);
  },
};
