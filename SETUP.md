# GeoFarmland Setup Guide

## Prerequisites

- .NET 9 SDK
- Node.js 18+
- PostgreSQL with PostGIS extension
- Database: `geofarmland`

## Database Setup

1. Create the database in PostgreSQL:

```sql
CREATE DATABASE geofarmland;
\c geofarmland
CREATE EXTENSION IF NOT EXISTS postgis;
```

2. Connection string (already in appsettings.json):
   - Host: localhost
   - Database: geofarmland
   - Username: postgres
   - Password: worisoft@123

## Run Migrations

```powershell
cd src\geofarmland.Server
dotnet ef database update --context ApplicationDbContext
```

Or run the app - migrations apply automatically on startup.

## Seed Admin User

The admin user is seeded automatically on first run:

- **Email:** admin@geofarmland.com
- **Password:** Admin@123456
- **Role:** Admin

## Run the Application

### Option 1: Visual Studio / Rider
Open `geofarmland.sln` and run the Server project (F5).

### Option 2: Command Line

```powershell
# Terminal 1 - Backend
cd src\geofarmland.Server
dotnet run

# Terminal 2 - Frontend (dev)
cd src\geofarmland.client
npm run dev
```

The SPA proxy will forward API requests when running from the Server project.

## API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | /api/auth/login | No | Login with email/password |
| POST | /api/auth/register | No | Register new user |
| POST | /api/auth/refresh | No | Refresh access token |
| GET | /api/auth/me | Yes | Get current user |
| GET | /plots | Yes | List plots |
| POST | /plots | Yes | Create plot |
| PUT | /plots/{id} | Yes | Update plot |
| DELETE | /plots/{id} | Yes | Delete plot |

## JWT Configuration

JWT settings in `appsettings.json`:

- Access token: 60 minutes
- Refresh token: 7 days
- For production: set `Jwt:Key` via environment or user secrets
