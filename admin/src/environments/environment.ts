/**
 * Production environment (the default build configuration). Real values are
 * injected at deploy time; the admin console talks to the API through a
 * same-origin reverse proxy by default. See environment.development.ts for local.
 */
export const environment = {
  production: true,
  apiBaseUrl: '/api/v1',
};
