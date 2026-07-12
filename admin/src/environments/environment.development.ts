/**
 * Local development environment. Points at the API running from
 * docker/docker-compose.yml (or `dotnet run`) on localhost:8080. Swapped in for
 * environment.ts by the "development" build configuration (see angular.json).
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:8080/api/v1',
};
