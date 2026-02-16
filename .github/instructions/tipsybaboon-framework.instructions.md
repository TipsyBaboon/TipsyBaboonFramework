---
applyTo: "TipsyBaboon.Core/**,TipsyBaboon.SqlServer/**,TipsyBaboon.UI/**"
---

# TipsyBaboon Framework Principles

These principles apply to all framework libraries (Core, SqlServer, UI) and any future additions.

## Key Principles

### 1. ModuleName+ModelName as Primary Identity

Type is a second-class citizen. **ModuleName + ModelName** are the primary source of truth; Type exists for facilitation only.

- Use typed methods ONLY for known framework types: Governance, Config, Changelog, etc.
- All other code: use module/model name-based methods
- Runtime-generated models are planned — code depending on compile-time types will break

**Rationale:** Testing from day one must validate untyped paths work correctly.

### 2. Dogfooding

Framework code uses the same public methods consumers use.

- Insert/Update/Delete/Query operations go through consumer-facing methods
- Internal method variants exist only for option differences (e.g., Schema Sync updating invariants)
- UI special controls use same hooks available to consumers

**Exception:** Schema Sync schema updates (not exposed externally).

### 3. No Silent Failures

All failures propagate and emit visibly:

- Validation errors → propagate
- User permission/privilege failures → propagate
- Exceptions → propagate
- Failed bindings (UI listeners missed invocation timing) → propagate
- Runtime exceptions → modal notifications in UI
- Startup failures (e.g., Schema Sync) → fail loud

**Exception:** User preferences are soft storage — return empty string for missing keys (not 404). Preference checks for unset values are expected.

**Neither consumer nor user should ever question if something worked.**

### 4. No Hardcoding

Framework operates from consumer config and discovery only.

- No hardcoded overrides
- No values masking framework usage from consumer usage
- All behavior configurable through same mechanisms consumers use

## Layer Separation

| Layer | Responsibility | Agnostic To |
|-------|---------------|-------------|
| **Core** | Defines contracts, minimal interfaces for other layers | Persistence, UI framework |
| **Persistence** (SqlServer) | DB operations, Schema Sync | UI framework |
| **UI** (Razor/Bootstrap) | Customizable UI layer, API implementations | Persistence implementation |

Core defines minimal contracts that persistence and UI layers implement.

## SqlServer Query Filtering

**Registered Module Isolation**: SqlServer automatically filters queries for governance tables and permission views to only show registered modules/models.

Filtered types (in `TipsyBaboonModelStore.QueryCoreAsync`, `QueryTypedAsync`, `CountCoreAsync`):
- **RolePermission** & **ModelPermission** views: Filtered by `ModuleName` IN registered modules
- **RolePrivilegeView** view: Filtered by `ModelId` IN registered model IDs
- **RADModule** table: Filtered by `Name` IN registered modules  
- **ModelRecord** table: Filtered by `Id` IN registered model IDs from registered modules
- **Privilege** table: Filtered by `RecordId` IN registered model IDs from registered modules

**Rationale**: Supports monolith pattern with multiple entry points where different apps register different module subsets. Database may contain historical module/model records from previous registrations; only currently registered modules should be visible in UI and queries.

**Implementation**: Filters injected at SQL query level (WHERE clause) to maintain correct paging/counting. Uses parameterized queries to prevent SQL injection.
