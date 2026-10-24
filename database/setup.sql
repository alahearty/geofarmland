-- GeoFarmland database setup (run with psql as a PostgreSQL superuser).
CREATE DATABASE geofarmland;
\connect geofarmland
CREATE EXTENSION IF NOT EXISTS postgis;

-- Isolated database used by the automated tests.
CREATE DATABASE geofarmland_test;
\connect geofarmland_test
CREATE EXTENSION IF NOT EXISTS postgis;

-- Connection used by appsettings.json:
--   Host=localhost;Database=geofarmland;Username=postgres;Password=worisoft@123
-- Change the password before any deployment.
