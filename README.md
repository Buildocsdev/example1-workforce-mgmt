# Workforce Management — Buildocs Example

A full-stack reference application that demonstrates how to build an internal workforce management system using the [Buildocs](https://buildocs.com) platform. Forms, screens, and layouts are designed and iterated in the Buildocs visual editor; all business logic, data persistence, and file storage run locally in your own infrastructure.

---

## What this example covers

| Area | Description |
|---|---|
| **Employee records** | Create, edit, and list employees (EMPL / EMPLTBL forms) |
| **Certifications** | Track employee certifications with linked records (CERT) |
| **Training** | Log training events per employee (TRAIN) |
| **Record types** | Configurable lookup values (TYPE) |
| **File storage** | Document attachments via S3-compatible storage (MinIO locally) |
| **Localisation** | UI available in English, Estonian, and Russian |

---

## Architecture

```
┌─────────────────────────────────────────┐
│  Buildocs.com (visual form designer)    │
│  — define screens, fields, layouts      │
└───────────────┬─────────────────────────┘
                │ API key
┌───────────────▼─────────────────────────┐
│  React Frontend  (frontend/)            │
│  @buildocsdev/sdk renders forms         │
└───────────────┬─────────────────────────┘
                │ HTTP
┌───────────────▼─────────────────────────┐
│  .NET 8 API  (SampleApi/)               │
│  Plugin event handlers  (Plugins/)      │
│  FormEventHandler library               │
└──────────┬────────────────┬─────────────┘
           │                │
    ┌──────▼──────┐  ┌──────▼──────┐
    │  DynamoDB   │  │   MinIO     │
    │  (local)    │  │   (local)   │
    └─────────────┘  └─────────────┘
```

- **Forms and screens** are configured in Buildocs.com and streamed to the SDK at runtime — no redeploy needed when you change a layout.
- **Event handlers** (save, delete, table load, etc.) are plain C# classes that extend `GenericFormHandler` and live entirely in your codebase.
- **Data** is stored in a local DynamoDB instance seeded automatically with 1 500 randomised employee records on first run.
- **Files** are stored in a local MinIO bucket (S3-compatible).

---

## Prerequisites

| Tool | Version |
|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Any recent |
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| [Node.js](https://nodejs.org/) | 18+ |
| A free [Buildocs](https://buildocs.com) account | — |

---

## Getting started

### 1. Get your Buildocs API key

1. Register and log in at [buildocs.com](https://buildocs.com).
2. Open the **Demo** project that is created for you automatically.
3. Copy the project **API key** from the project settings.

### 2. Configure the frontend

Open `frontend/src/index.tsx` and paste your API key:

```tsx
<BuildocsProvider apiKey="YOUR_API_KEY_HERE">
```

### 3. Start the local infrastructure

```bash
docker compose up -d
```

This starts:

- **DynamoDB Local** on `http://localhost:8000`
- **MinIO** on `http://localhost:9000` (console at `http://localhost:9001`)

### 4. Run the API

```bash
cd SampleApi
dotnet run
```

The API starts on `http://localhost:7000`. On first run, `SeedService` automatically creates the DynamoDB table and populates it with 1 500 sample employee records.

### 5. Run the frontend

```bash
cd frontend
npm install
npm start
```

The app opens at `http://localhost:3000` and displays the employee table form.

---

## Live form editing (no redeploy required)

One of the core capabilities this example demonstrates:

1. In **Buildocs.com → Demo project**, open any screen in the visual editor.
2. Add a field, change a label, reorder a section — whatever you like.
3. **Refresh the browser** on your locally running app.

The updated form renders immediately. No API restart, no frontend rebuild, no deployment.

---

## Project structure

```
├── docker-compose.yml          # DynamoDB Local + MinIO
├── SampleApi/                  # ASP.NET Core 8 host
│   ├── Controllers/            # HTTP endpoints
│   ├── Services/               # AWS wiring + seed data
│   └── Localization/           # Translation files (en / et / ru)
├── FormEventHandler/           # Core form event library
├── FileStorage/                # S3 file storage abstraction
├── Plugins/
│   └── Intra/
│       ├── EMPL/               # Employee form handler
│       ├── EMPLTBL/            # Employee table handler
│       ├── CERT/               # Certification handler
│       ├── TRAIN/              # Training handler
│       └── TYPE/               # Record type handler
└── frontend/                   # React + TypeScript app
    └── src/
        ├── index.tsx           # Entry point — set your API key here
        ├── provider/           # Form host context
        └── i18n.ts             # Internationalisation setup
```

---

## Writing your own event handlers

Each form has a corresponding C# class that extends `GenericFormHandler`. Override only the lifecycle methods you need:

```csharp
public class EventHandler : GenericFormHandler
{
    public override async Task Form_onInit(bool loadFromDb)
    {
        await base.Form_onInit(loadFromDb);
        // Populate dropdowns, set defaults, etc.
        cmd.PopulateSelectBoxList("status", new Dictionary<string, string>
        {
            { "Active", Translate("Active") },
            { "Draft",  Translate("Draft")  },
        });
    }

    public override async Task Form_onAfterSave()
        => cmd.SuccessMessage(Translate("record.submit.success"));

    public override Task Form_onBeforeDelete()
    {
        // Throw UserWarningException to block deletion with a user-visible message.
        return Task.CompletedTask;
    }
}
```

### Lifecycle hooks

All hooks are defined in `FormEventHandler/AbstractHandler.cs`. Override only what you need — every method has a no-op default.

**Form lifecycle**

| Hook | Signature | When it fires |
|---|---|---|
| `Form_onInit` | `(bool loadFromDb)` | Form opens or refreshes |
| `Form_onBeforeSave` | `()` | Before the record is written — validate here |
| `Form_onSave` | `()` | Persists the record to DynamoDB (override to customise storage) |
| `Form_onAfterSave` | `()` | Record saved successfully |
| `Form_onBeforeDelete` | `()` | Before deletion — throw `UserWarningException` to cancel |
| `Form_onDelete` | `()` | Deletes the record (override to customise) |
| `Form_onAfterDelete` | `()` | Record deleted successfully |
| `Form_onCancel` | `()` | User cancels the form |
| `Form_onPrint` | `()` | Print is triggered |

**Field events**

| Hook | Signature | When it fires |
|---|---|---|
| `Form_onClick` | `(string fieldName)` | A button or clickable field is activated |
| `Form_onRefresh` | `(string fieldName, object value)` | A field value changes and triggers a refresh |

**File upload**

| Hook | Signature | When it fires |
|---|---|---|
| `Form_onBeforeUpload` | `(string fieldName, object files)` | Before files are uploaded — use to assign a guid to a new record |
| `Form_onAfterUpload` | `(string fieldName, List<string>? uploadedFiles)` | After upload completes |

**Table widget events**

Table hooks can be scoped to a specific widget by prefixing the widget name (e.g. `certificatestbl_onTableLoadData`), or declared without a prefix to apply to all table widgets on the form.

| Hook | Signature | When it fires |
|---|---|---|
| `{widget}_onTableLoadData` | `()` → `List<Dictionary<string, string>>` | Table widget requests its rows |
| `{widget}_onTableCreateRecordEvent` | `()` | User clicks "Add" in the table |
| `Form_onTableRunActionEvent` | `(string action, RowData rowData)` | A row-level action button is clicked |
| `Form_onTableRunBatchActionEvent` | `(string action, object[] data)` | A batch action is applied to selected rows |
| `Form_onTableEditRunActionEvent` | `(string action, string data)` | An action fires from an inline table edit |

### Helper utilities available in every handler

| Member | Purpose |
|---|---|
| `cmd` | Build response commands — show messages, navigate, populate dropdowns |
| `record` | Read and write the current DynamoDB record |
| `screen` | Read or modify screen-level configuration |
| `contextHandlerInstance` | Access request context values (guid, pluginCode, formCode, …) |
| `Translate(key)` | Resolve a localisation key via the active plugin's translation files |
| `SaveCurrentAndUpdateOriginator()` | Save the current record and propagate its guid to child forms — use when opening a linked record from a table widget |

---

## Local service endpoints

| Service | URL |
|---|---|
| .NET API | http://localhost:7000 |
| React app | http://localhost:3000 |
| DynamoDB Local | http://localhost:8000 |
| MinIO API | http://localhost:9000 |
| MinIO Console | http://localhost:9001 |

MinIO default credentials (local only): `admin` / `adminpassword`

---

## License

Licensed under the [Apache License 2.0](LICENSE).
