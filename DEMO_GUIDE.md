# GeoFarmland - Academic Demonstration Guide

## 1. What problem does the software solve?

GeoFarmland lets a farm manager draw field boundaries on a map, store them as spatial polygons, and
inspect the field's size, location, weather context and recent sensor readings.

## 2. Why does the problem matter?

Field records are often kept on paper or in spreadsheets that lose their spatial context. A GIS
backend makes boundaries queryable and allows the same plot to be linked to weather and IoT data.

## 3. Main objectives

- Authenticate users with JWT and role claims.
- Store farmland boundaries as PostGIS geometry through an ASP.NET Core API.
- Present plots on an interactive Leaflet map.
- Support plot creation by clicking map vertices, selection, zoom and details.
- Integrate a live-weather lookup when an API key is configured.
- Store and display IoT sensor readings, clearly separating synthetic demo data from live hardware.

## 4. Technologies used

| Concern | Technology |
|---|---|
| Backend | ASP.NET Core 9, FastEndpoints, MediatR, FluentValidation |
| Identity | ASP.NET Core Identity, JWT bearer |
| Database | PostgreSQL 18 + PostGIS, EF Core 9, NetTopologySuite |
| Front end | Vue 3, TypeScript, Vite 6, Pinia, Leaflet 1.9 |
| Realtime | SignalR |
| Tests | NUnit + WebApplicationFactory against `geofarmland_test` |

## 5. Architecture

```
Vue 3 SPA (Leaflet map, plot list, weather/IoT panels)
  -> Axios same-origin or Vite proxy
      -> ASP.NET Core API
          -> FastEndpoints feature slices
              -> EF Core ApplicationDbContext
                  -> PostgreSQL + PostGIS
          -> WeatherApiService (OpenWeatherMap)
          -> SensorDataRepository + SignalR hub
```

The server serves the built SPA from `src/geofarmland.client/dist` when present. Vite can also run
separately during development.

## 6. Database design

Main entities:

| Entity | Purpose | Fields |
|---|---|---|
| `Plot` | Farmland boundary | Id, Name, Geometry, OwnerId |
| `Device` | Registered field sensor | Id, DeviceId, Name, PlotId, OwnerId |
| `SensorData` | Stored IoT reading | Id, DeviceId, Latitude, Longitude, Rainfall, SoilMoisture, Timestamp, OwnerId |
| `WeatherSnapshot` | Stored weather history | Id, PlotId, Temperature, Humidity, Pressure, Condition, FetchedAtUtc |
| `ApplicationUser` | Identity account | Id, Email, FullName, CreatedAt |
| `RefreshToken` | JWT refresh state | Token, UserId, ExpiresAt, IsRevoked |

`Plot.Geometry` is mapped to a PostgreSQL `geometry` column. The API returns coordinates as
`[longitude, latitude]`; Leaflet displays them as `[latitude, longitude]`.

## 7. Main modules

- Authentication and account security (`/api/auth/*`).
- Owner-scoped plot CRUD and reshaping (`/plots`).
- Registered devices (`/devices`) and owner-scoped IoT readings (`/iot/sensor-data`).
- Weather, forecast and persisted weather history (`/weather/*`).
- Map, device, sensor and account management in the Vue dashboard.
- Administrator overview at `/admin`.
- Authenticated, server-push-only SignalR hub (`/signalr/hub`).

## 8. Demonstration sequence

1. Start PostgreSQL, build the Vue client and start the server.
2. Sign in as `admin@geofarmland.com` / `Admin@123456`.
3. Show the map, plot list, devices, weather history and selected-plot details.
4. Click **Draw plot**, click at least three corners on the map, name the plot and save it.
5. Select the new plot and click **Edit shape**: drag vertices, add a point and save.
6. Open **Devices**, register a sensor and link it to the selected plot.
7. Click **Check live weather at plot** and show current conditions, forecast and history (with a key), or the explicit unavailable message.
8. Submit a synthetic reading and show the live SignalR status and stored readings.
9. Open **Account** and **Admin**; then sign out and sign in as `farmer@geofarmland.com` / `Farmer@123456` to show isolated plots and devices.
10. Optional API demonstration: show `GET /plots` requires a Bearer token and returns 401 without it.

## 9. Data flow

1. The Vue dashboard logs in through `/api/auth/login` and stores the access token.
2. `GET /plots` loads polygons; the client reverses `[lng,lat]` into Leaflet `[lat,lng]`.
3. Drawing collects map clicks and posts an open coordinate ring to `POST /plots`.
4. The API closes the ring, creates an NTS polygon and stores it via Npgsql/PostGIS.
5. Weather calls the external API through the backend; a missing key returns a controlled 503.
6. A sensor post is stored in `SensorData` with its device ID, coordinates, rainfall and soil moisture, then pushed to the owner's authenticated SignalR connection; the monitor reloads the list.

## 10. Limitations and future improvements

- Weather and forecast require a private API key and internet access.
- The sensor monitor is synthetic; device registration exists, but there is no hardware provisioning or device-level credential.
- SignalR, weather and IoT ingestion require a signed-in user; plots, devices and readings are owner-scoped.
- Offline map tiles and background push notifications are not implemented.
- Future work: crop records, audit/consent trails and production notification infrastructure.


