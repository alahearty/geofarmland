# GeoFarmland

## Overview

GeoFarmland is a geospatial farm-management backend with a Vue 3 single-page front-end. It provides
JWT authentication, plot (farmland polygon) CRUD stored as PostGIS geometry, a live weather proxy,
and an IoT sensor-data ingestion endpoint that broadcasts readings over SignalR.

The back end is built with ASP.NET Core 9 and FastEndpoints. The front end is a Vite/Vue 3 app.

## Problem statement

Small-scale farmers need a simple way to record field boundaries and monitor local conditions.
GeoFarmland demonstrates how a web application can store field polygons in a spatial database and
combine that data with weather and IoT feeds.

## Aim

To provide a GIS-enabled farm-management prototype that stores field boundaries spatially and presents them with weather and IoT context in a usable web interface.

## Objectives

- Provide secure registration, login, refresh-token and profile endpoints.
- Store farmland plots as PostGIS polygons and expose CRUD operations.
- Accept IoT sensor readings and broadcast them to connected clients with SignalR.
- Provide a live-weather lookup using an external weather API.
- Offer a minimal browser UI for authentication and a dashboard.

## Technology stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 9, FastEndpoints 5.35, MediatR, FluentValidation |
| Identity | ASP.NET Core Identity + JWT bearer tokens |
| Database | PostgreSQL + PostGIS, EF Core 9 + NetTopologySuite |
| Realtime | SignalR |
| Frontend | Vue 3, TypeScript, Vite 6, Pinia, Vue Router, Axios |
| Tests | NUnit + WebApplicationFactory against `geofarmland_test` |

## Architecture

```
Vue SPA (src/geofarmland.client)
  -> Axios (same origin in production, Vite proxy in development)
      -> ASP.NET Core API (src/geofarmland.Server)
          -> FastEndpoints feature slices
              -> EF Core ApplicationDbContext
                  -> PostgreSQL + PostGIS
          -> WeatherApiService / IoTDataService
          -> SignalR Hub (/signalr/hub)
```

The server can serve the built SPA from `src/geofarmland.client/dist`; when that folder exists the
API and UI run from one process. In development the Vite dev server can be run separately.

## Software prerequisites

- .NET 9 SDK (`dotnet --list-sdks`)
- Node.js 18+ and npm
- PostgreSQL 14+ with the PostGIS extension
- Optional: `dotnet-ef` tool for manual migrations
- Optional: free API keys for OpenWeatherMap / Weather.com

## Database setup

Create the database and enable PostGIS:

```sql
CREATE DATABASE geofarmland;
\c geofarmland
CREATE EXTENSION IF NOT EXISTS postgis;
```

Default connection string in `src/geofarmland.Server/appsettings.json`:

```
Host=localhost;Database=geofarmland;Username=postgres;Password=worisoft@123
```

Migrations are applied automatically at startup. You can also run them manually:

```powershell
cd src\geofarmland.Server
dotnet ef database update --context ApplicationDbContext
```

The automated tests use a separate database:

```sql
CREATE DATABASE geofarmland_test;
\c geofarmland_test
CREATE EXTENSION IF NOT EXISTS postgis;
```

The admin user is seeded automatically on first start:

| Email | Password | Role |
|---|---|---|
| `admin@geofarmland.com` | `Admin@123456` | Admin |

The full academic presentation script is in `DEMO_GUIDE.md`.

## Environment configuration

Use environment variables or user secrets to override the development defaults:

```
ConnectionStrings__DefaultConnection=Host=localhost;Database=geofarmland;Username=postgres;Password=worisoft@123
Jwt__Key=replace-with-a-random-secret-at-least-32-characters
ExternalApis__OpenWeatherMap__ApiKey=YOUR_OPENWEATHERMAP_API_KEY
ExternalApis__WeatherCom__ApiKey=YOUR_WEATHERCOM_API_KEY
ExternalApis__IoTService__BaseUrl=http://localhost:5000/api
Cors__AllowedOrigins__0=http://localhost:58036
Cors__AllowedOrigins__1=https://localhost:64210
ASPNETCORE_ENVIRONMENT=Development
```

See `.env.example`. ASP.NET Core reads environment variables directly; a `.env` file is not loaded
automatically. The weather key can remain a placeholder: `/weather/live` then returns a controlled
503 response instead of crashing.

## Install and build

```powershell
cd src\geofarmland.client
npm install
npm run build

cd ..\geofarmland.Server
dotnet build
```

## Run

Recommended single-process run (API + built SPA):

```powershell
cd src\geofarmland.Server
dotnet run
```

Open <https://localhost:58035> (launch profile) or run explicitly on HTTP:

```powershell
dotnet run --urls http://localhost:58036
```

Then open <http://localhost:58036>.

Optional two-process development mode:

```powershell
# Terminal 1
cd src\geofarmland.Server
dotnet run

# Terminal 2
cd src\geofarmland.client
npm run dev
```

Vite serves the UI on <https://localhost:64210> and proxies `/api` to the ASP.NET Core HTTPS port.
Set `ASPNETCORE_HTTPS_PORT=58035` or `ASPNETCORE_URLS=http://localhost:58036` in the Vite terminal
if you need a non-default proxy target.

## Demo accounts

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@geofarmland.com` | `Admin@123456` |
| Farmer | `farmer@geofarmland.com` | `Farmer@123456` |

The farmer account owns a separate demo plot and device, which demonstrates owner isolation against the administrator view.

## Main API endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Register a user |
| POST | `/api/auth/login` | No | Login and receive access + refresh tokens |
| POST | `/api/auth/refresh` | No | Rotate a refresh token |
| GET | `/api/auth/me` | Bearer | Current user profile |
| PUT | `/api/auth/me` | Bearer | Update the display name |
| POST | `/api/auth/change-password` | Bearer | Change the password and revoke refresh sessions |
| POST | `/api/auth/logout` | No | Revoke the supplied refresh token |
| GET | `/api/admin/overview` | Admin | User and resource summary |
| GET | `/plots` | Bearer | List the current user's plots (Admin sees all) |
| GET | `/plots/{id}` | Bearer | Read one owned plot (Admin sees all) |
| POST | `/plots` | Bearer | Create a plot owned by the current user |
| PUT | `/plots/{id}` | Bearer | Update an owned plot |
| DELETE | `/plots/{id}` | Bearer | Delete an owned plot |
| GET | `/weather/live?latitude=..&longitude=..&plotId=..` | Bearer | Current weather (also saves history) |
| GET | `/weather/history?plotId=..&take=20` | Bearer | Recent weather snapshots |
| GET | `/weather/forecast?latitude=..&longitude=..` | Bearer | Three-hour forecast entries |
| GET | `/devices` | Bearer | Owned devices (Admin sees all) |
| POST | `/devices` | Bearer | Register a device |
| PUT | `/devices/{id}` | Bearer | Update an owned device |
| DELETE | `/devices/{id}` | Bearer | Remove an owned device |
| GET | `/iot/sensor-data` | Bearer | Last 20 stored readings for the current user |
| POST | `/iot/sensor-data` | Bearer | Ingest a reading and push it to the owner |
| SignalR | `/signalr/hub` | Bearer | Authenticated, server-push-only weather/sensor events |

Example plot creation after login:

```powershell
curl -X POST http://localhost:58036/plots `
  -H "Authorization: Bearer <accessToken>" `
  -H "Content-Type: application/json" `
  -d '{"name":"Demo Plot","coordinates":[[18.42,-33.92],[18.43,-33.92],[18.43,-33.93],[18.42,-33.93],[18.42,-33.92]]}'
```

## Main features and current UI scope

Backend features:

- Registration, login, JWT access tokens, refresh tokens, `/me`, profile update, password change.
- Owner-scoped Plot CRUD persisted as SRID-4326 PostGIS geometry; open rings are closed automatically and polygons are validated.
- Owner-scoped device registration with optional plot links.
- Owner-scoped IoT readings persist device ID, coordinates, rainfall and soil moisture, with SignalR delivery to the owner.
- Current weather with extended metrics, persisted weather history and three-hour forecast support.
- Authenticated weather/SignalR surfaces and configurable CORS origins.
- Refresh-token rotation with hashed storage, logout revocation, account lockout and per-endpoint throttling.
- Administrator overview for accounts and resource totals.
- Automatic migrations and seeded administrator/farmer demo accounts.

Front end features:

- Login, registration, authenticated dashboard, account settings and administrator overview.
- Interactive Leaflet map with OpenStreetMap tiles.
- Plot polygons rendered from the API, a synchronized plot list, selection, zoom-to-plot,
  details panel and computed area in hectares.
- Drawing and editing modes: create, rename, drag vertices, add/remove vertices, undo and save through `PUT /plots/{id}`.
- Device registration and selection from the dashboard, linked to owned plots.
- Live weather, richer current conditions, stored weather history and forecast display.
- "IoT sensor monitor" with a device-bound synthetic reading form and live SignalR updates.
- Live connection status, loading/empty/error states, map resizing, and responsive sidebar behaviour.

The map is centred on Cape Town because the project is a South African farmland prototype. The
admin account can draw a plot anywhere in the world.

## Testing

The repository includes ten ASP.NET Core integration tests in
`tests/Geofarmland.Server.Tests`. They use `WebApplicationFactory` against the isolated
`geofarmland_test` database.

```powershell
dotnet test tests\Geofarmland.Server.Tests\Geofarmland.Server.Tests.csproj
```

Expected result: `Passed: 10`.

Client checks (utility and Account/Admin component tests, then the production build):

```powershell
cd src\geofarmland.client
npm run test:unit
npm run build
```

Expected result: 6 Vitest tests pass and the type-check/build succeeds. The CI workflow runs
both suites with a PostGIS service container.

The tests cover:

- JWT protection (unauthenticated `GET /plots` returns 401).
- Invalid and valid login.
- Plot create/list/update/delete, including automatic ring closure and GeoJSON longitude/latitude
  ordering (`[lng, lat]` from the API, reversed for Leaflet).
- Owner isolation: a second user cannot list, read, update or delete another user's plots or readings.
- Geometry validation for out-of-range and self-intersecting polygons, plus SRID preservation after update.
- Device registration CRUD, IoT ingestion and history for the full device/coordinate/rainfall/moisture payload.
- Refresh-token rotation, reuse rejection and logout revocation.
- Profile update, administrator overview, weather history/forecast failure behavior, and authenticated weather/IoT access.

Manual UI smoke test:

1. Start PostgreSQL and the server.
2. Open the UI and log in with the admin account.
3. Draw a plot on the map and save it.
4. Select the plot, inspect its area/details and zoom behaviour.
5. Click **Check live weather at plot** and confirm either live data or the documented
   "API key not configured" message.
6. Send a synthetic sensor reading and confirm it appears in the monitor list.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `PostgresException: type "geometry" does not exist` | Run `CREATE EXTENSION postgis;` in the `geofarmland` database. |
| `401` on `/plots` with a token | Make sure the backend build includes the JWT default-scheme configuration in `Program.cs`. |
| UI shows a blank page on the server port | Run `npm run build` in `src/geofarmland.client` so `dist/index.html` exists. |
| Vite proxy errors | Set `ASPNETCORE_HTTPS_PORT` / `ASPNETCORE_URLS` in the Vite terminal, or use the built SPA served by the API. |
| Weather endpoint returns 503 | Add a real OpenWeatherMap key under `ExternalApis:OpenWeatherMap:ApiKey`. |

## Known limitations

- Live weather and forecast require a real OpenWeatherMap API key and internet access. Without a
  key the UI shows an explicit unavailable message rather than fabricated readings.
- IoT hardware is not included. Readings are submitted by the demo form; device registration and
  owner-scoped storage exist, but there is no hardware provisioning or device-level credential.
- The map uses public OpenStreetMap tiles and therefore needs internet access for the basemap.
- JWT key and database password are development defaults committed for reproducibility; replace
  them before any real deployment.
- CORS origins are configured under `Cors:AllowedOrigins`; add the deployed SPA origin before
  hosting outside local development.
- The browser client connects to SignalR while the dashboard is open; there is no global
  notification center or background push while the app is closed.



