# GeoFarmland Setup Guide

See `README.md` for the complete student handover guide. This file is a short quick-start.

## Prerequisites

- .NET 9 SDK
- Node.js 18+ and npm
- PostgreSQL 14+ with PostGIS (`CREATE EXTENSION postgis;`)
- Database name: `geofarmland`

## 1. Database

```sql
CREATE DATABASE geofarmland;
\c geofarmland
CREATE EXTENSION IF NOT EXISTS postgis;
```

Default connection string (see `src/geofarmland.Server/appsettings.json`):

```
Host=localhost;Database=geofarmland;Username=postgres;Password=worisoft@123
```

Migrations and the admin account are applied automatically when the server starts.

Seeded accounts:

- Administrator: `admin@geofarmland.com` / `Admin@123456`
- Farmer: `farmer@geofarmland.com` / `Farmer@123456`

## 2. Install and build the Vue client

```powershell
cd src\geofarmland.client
npm install
npm run build
```

## 3. Run the backend (serves the built client)

```powershell
cd src\geofarmland.Server
dotnet run
```

Open:

- HTTPS: `https://localhost:58035`
- HTTP: `http://localhost:58036`

The server serves `src/geofarmland.client/dist` when it exists, so a single process runs both the
API and the UI.

`SpaProxy` is **not** used by this project. To develop the UI with hot reload, run the Vite dev
server separately:

```powershell
cd src\geofarmland.client
npm run dev
```

Vite serves `https://localhost:64210` and proxies `/api` to the backend. Set
`ASPNETCORE_HTTPS_PORT=58035` (or `ASPNETCORE_URLS=http://localhost:58036`) in the Vite terminal
if the default proxy target is wrong.

## API endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | /api/auth/login | No | Login with email/password |
| POST | /api/auth/register | No | Register new user |
| POST | /api/auth/refresh | No | Rotate the refresh token |
| POST | /api/auth/logout | No | Revoke the supplied refresh token |
| GET | /api/auth/me | Yes | Get current user |
| GET | /plots | Yes | List owned plots (Admin sees all) |
| GET | /plots/{id} | Yes | Read one owned plot |
| POST | /plots | Yes | Create an owned plot |
| PUT | /plots/{id} | Yes | Update an owned plot |
| DELETE | /plots/{id} | Yes | Delete an owned plot |
| GET | /weather/live | Yes | Live weather proxy |
| GET | /iot/sensor-data | Yes | Recent readings for the current user |
| POST | /iot/sensor-data | Yes | Ingest an owner-scoped IoT reading |

## JWT configuration

JWT settings live in `appsettings.json`:

- Access token: 60 minutes
- Refresh token: 7 days
- For production, override `Jwt:Key` with an environment variable or user secret.

## Troubleshooting

- `type "geometry" does not exist`: enable PostGIS.
- Blank UI on the server port: run `npm run build` in `src\geofarmland.client`.
- Protected endpoints return 401: confirm you are sending `Authorization: Bearer <accessToken>`.
- `401` with a valid token after modifying `Program.cs`: ensure JWT is the default authentication
  scheme (see `Program.cs`).




