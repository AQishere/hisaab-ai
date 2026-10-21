// The last response per URL for the signed-in session, so a page opened again shows its data at once while
// useFetch reloads it. Emptied on sign-in and sign-out (session.save / session.clear), so no user sees another's data.
const responses = new Map<string, unknown>();

export const responseCache = {
  get: (url: string) => responses.get(url),
  has: (url: string) => responses.has(url),
  set: (url: string, data: unknown) => responses.set(url, data),
  clear: () => responses.clear(),
};
