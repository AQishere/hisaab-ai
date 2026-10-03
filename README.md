# Kratix ERP - Crescent Flour Mills demo

ERP for a flour mill: purchasing, gate entry and weighbridge, quality check, stock, milling batches, sales, invoices and payments, with role-based access.

- `QadoosFlourMill/Kratix.ERP/` - backend API (ASP.NET Core, .NET 10, EF Core, SQL Server)
- `kratix-ui/` - frontend (React 19, TypeScript, Vite, Material UI)
- `PROGRESS.md` - what has been built, open issues and the roadmap
- `DATABASE_UPGRADE.md` - backing up, pre-checking and upgrading an existing database

## Requirements

- .NET 10 SDK
- Node.js 20 or newer
- SQL Server (Express is fine). The default connection string in `appsettings.json` uses `.\SQLEXPRESS` with Windows authentication.

## Setup

Secrets are not in the repository. Create the two local files below from their examples.

### 1. Backend config

```bash
cd QadoosFlourMill/Kratix.ERP
cp appsettings.Development.example.json appsettings.Development.json
```

Edit `appsettings.Development.json`:

- `Jwt:Key` - a random string of at least 32 characters, used to sign login tokens. For example:
  `node -e "console.log(require('crypto').randomBytes(48).toString('base64'))"`
- `DemoUsers` - a password for each demo account (`owner`, `admin`, `operations`, `sales`, `worker`). Accounts are created on first start; accounts with an empty password are skipped.

### 2. Frontend demo accounts (optional)

The login page can show one-click demo account buttons. To enable them:

```bash
cd kratix-ui/src/config
cp demoAccounts.example.ts demoAccounts.local.ts
```

Fill in the same passwords as in `DemoUsers`. Without this file the buttons are simply hidden.

## Run

```bash
# backend - http://localhost:5069 (creates/migrates the database on start)
cd QadoosFlourMill/Kratix.ERP
dotnet run

# frontend - http://localhost:5173
cd kratix-ui
npm install
npm run dev
```

Starting the backend applies any pending migrations to the database. For an existing database, follow `DATABASE_UPGRADE.md` first.

Regression checks (isolated test database only, never the working one): start a second backend on port 5071 with `ConnectionStrings__DefaultConnection` pointing at a `KratixReview_` database, then run `ERP_TEST_BASE_URL=http://localhost:5071 ERP_TEST_DATABASE=KratixReview_<name> node scripts/verify-workflows.cjs`.

The frontend reads the API address from `kratix-ui/.env.local` (`VITE_API_BASE_URL`, see `.env.example`); copy `.env.example` to `.env.local` (it points at `http://localhost:5069/api`).

## Roles

| Role | Access |
|---|---|
| Owner, Admin | Everything, including the dashboard |
| Operations | Suppliers, products, warehouses, purchase orders, receiving, production, inventory |
| Sales | Customers, sales orders, invoices, payments |
| Worker | Receiving, production, inventory |

Access is enforced by the API (`Kratix.ERP/Auth/Roles.cs`) and mirrored in the UI (`kratix-ui/src/auth/permissions.ts`).
