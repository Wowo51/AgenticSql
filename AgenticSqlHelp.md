# AgenticSQL App: Help & User Guide (with Web Search)

Welcome! This guide walks you through the **AgenticSQL** desktop app (WPF, .NET 9). It covers setup, workflow, safety (read-only and containment), import/export, the **web search** option, and troubleshooting—without putting ordinary text inside code boxes.

---

## What the app does

* Creates or connects to a SQL Server database (defaults to LocalDB).
* Runs an iterative SQL agent that plans and executes T-SQL in short “epochs.”
* Shows a live log, lets you open/save prompts, and lets you stop mid-run.
* Optional containment hardening (SqlContain).
* **New:** optional **web search** to gather external context that can improve planning.
* Utilities to import a folder of files into a table, and to export schema or data.

---

## First-run setup

### 1) LLM API key path (required)

Set the OpenAI API key path in `MainWindow.xaml.cs` (look for `LLM.OpenAiKeyPath`) and make sure the file exists and contains a valid key.

### 2) SQL Server

If you don’t specify anything, the app uses LocalDB with a connection string equivalent to: `Server=(localdb)\MSSQLLocalDB;Database=<Db Name>;Trusted_Connection=True;TrustServerCertificate=True;`.
The **Db Name** toolbar field controls the database name (default `AgenticDb`). The app will create the database if it’s missing.

---

## The Main Window

**File ▾**
Open Prompt… (load `.txt`/`.prompt`), Save Prompt, Save Prompt As…

**StartAgent / StopAgent**
Start runs the agent with your current Prompt and settings. Stop cancels safely and resets UI state.

**MaxEpochs**
Hard cap on planning steps.

**UseIsComplete**
Let the agent stop early when objectives are met.

**QueryOnly**
Enforce read-only SQL (no DML/DDL). See **Safety & read-only**.

**NaturalLanguageResponse**
Add a final LLM pass for a concise, human-readable answer.

**Db Name**
Database to create/connect.

**CopyLog / ClearLog / ClearAllButLast**
Log utilities.

**ImportFolder / ExportData / ExportSchema**
One-click utilities (see below).

**ModelKey**
Model identifier (e.g., `gpt-5-mini`). You can change it per run.

**ContainServer / ContainDatabase**
Run SqlContain hardening before starting; if hardening fails, the agent won’t run.

**UseSearch** (New)
Allow the agent to do small web lookups during planning (e.g., best-practice patterns, tricky SQL idioms). Search actions are summarized in the log.

### Panes

* **Input** (left): your Prompt (plain text, multi-line).
* **Output (most recent lines)** (right): live log showing the latest slice for responsiveness. Use CopyLog to capture everything.

---

## Typical workflow

1. **Write your Prompt**
   Describe the goal and the sequence of epochs. Example structure:

> Generate articles describing ways to ensure agentic systems are safe and complete the final task before quitting.
>
> Epoch: Ensure that a `Memory` table with `Name` and `Content` exists.
> Epoch: Load existing `Memory` rows to avoid duplicate topics.
> Epoch: Write a long article listing 3 safety principles and insert it into `Memory`.
> One epoch per article: For each principle, write a long article and insert it into `Memory`.
> Epoch (final): Query all rows from `Memory` in creation order with content.
> Completion: If the final task succeeded, quit.

2. **Pick settings**

* MaxEpochs: 5–10 is a good start.
* UseIsComplete: usually on.
* QueryOnly: on for analytics-only; off if schema changes are expected.
* NaturalLanguageResponse: on if you want a clean prose answer.
* Db Name: accept default or set per project.
* ModelKey: set your preferred model.
* Containment: toggle as needed (requires SQL permissions).
* UseSearch: enable when an external reference would help (examples below).

3. **StartAgent**
   Watch the log for each epoch’s plan and execution. With UseSearch on, you’ll see concise notes about searches and how findings influenced the plan.

4. **StopAgent (optional)**
   Cancel anytime; the UI resets and logs “Agent canceled.”

5. **Review results**
   Read the final state (and NL summary if enabled). Use CopyLog to archive the full transcript.

---

## Safety & read-only

Turn **QueryOnly** on to force read-only analytics:

* The agent must avoid DML and DDL.
* A mutation scanner strips comments/strings and checks for dangerous constructs (INSERT/UPDATE/DELETE/MERGE, CREATE/ALTER/DROP/TRUNCATE, SELECT…INTO, temp objects, EXEC/sp\_executesql/xp\_\*, GRANT/REVOKE/DENY, transactions, DBCC, backup/restore, linked servers, etc.).
* Risky SQL is blocked; the agent adjusts (or stops on MaxEpochs).

**Tip:** Combine QueryOnly with **ContainDatabase** or **ContainServer** (SqlContain). If hardening fails, the run is aborted (fail-closed).

---

## Web Search (new)

When **UseSearch** is enabled:

* The agent may do small web lookups to reduce guesswork (e.g., window-function idioms, date math, safe paging).
* Findings are summarized and used to refine the next SQL step.
* The log notes why a search was done, what was learned (briefly), and how the plan changed.
* **Privacy:** Only the prompt and minimal keywords are used for search. Don’t include secrets in prompts.
* **Cost/latency:** Search adds calls; expect a bit more time/cost. Turn it off when you don’t need it.

Good uses: clarifying edge-case SQL behaviors, confirming canonical T-SQL patterns, verifying best practices.
Search supplements planning; it doesn’t override your actual schema or computed results.

---

## Database connection logic

You only set **Db Name** in the UI. Connection resolution:

1. If a full `ConnectionString` exists in the ViewModel, it’s used.
2. Else, if a `ServerConnectionString` exists, the app appends `Database=<Db Name>` if missing.
3. Else, defaults to LocalDB with trusted connection.

On StartAgent, the app creates the database if needed, then connects.

---

## Containment (SqlContain)

If **ContainServer** or **ContainDatabase** is checked:

* The app derives server/auth/db from the same connection logic.
* Scope is chosen based on checkboxes: Both, Instance (server), or Database.
* The hardener runs; non-zero return or exceptions are logged and the run is aborted. Ensure you have required permissions.

---

## Import / Export utilities

**ImportFolder**

* Choose a folder to load its files into a table named after the folder (sanitized; schema `dbo`).
* Each row includes `Name` (relative path), `Time` (UTC), `Content` (file text).
* `SqlTools.FileImporter.ImportAsync` creates the table if needed.
* Use cases: analytics on docs/code, indexing, snapshots.

**ExportSchema**

* Generates CREATE/ALTER DDL via `SqlTools.ExportSchema.Export(conn)`.
* Output is chunked in the log; use CopyLog to save.

**ExportData**

* Exports data as XML via `SqlTools.ExportDataXml.Export(conn, includeEmptyTables: true)`.
* Handy for quick backups and diffs on small datasets.

---

## Prompts: open/save

* **Open Prompt…** loads text and remembers its path.
* **Save Prompt** writes to the remembered path.
* **Save Prompt As…** lets you choose a new file and becomes the new remembered path.
* The log confirms success or shows errors.

---

## Logging

* The app buffers many lines but shows only the latest chunk for responsiveness.
* **CopyLog** copies the full buffer; **ClearLog** wipes it; **ClearAllButLast** keeps only the final line.
* Large outputs (DDL, XML) are chunked for a smooth UI.
* With UseSearch on, search rationales/summaries are included for auditability.

---

## Tips for effective prompts

* Be goal-oriented: “Create X if missing, then do Y and summarize Z.”
* Provide ordering/limits/filters to keep result sets small.
* For read-only analysis, enable QueryOnly and request summarized results.
* If you’re unsure about a tricky detail, enable UseSearch and mention what to verify (for example: “verify canonical greatest-n-per-group with ties using window functions, then implement…”).

---

## Troubleshooting

* **“LLM.OpenAiKeyPath is not set.”**
  Set the path in `MainWindow.xaml.cs` and ensure the file contains a valid key.

* **Agent won’t start with containment on**
  Check permissions for SqlContain. The log shows scope/errors. Fix them or disable containment temporarily.

* **“Read-only mode: Potentially mutating SQL detected; blocked.”**
  Turn off QueryOnly (not recommended on production) or rephrase the prompt to pure analytics.

* **Output pane looks incomplete**
  It shows only the newest lines. Use CopyLog for the entire transcript.

* **Search feels slow or flaky**
  Web search adds latency and may fail occasionally. The agent logs failures and continues. Turn off UseSearch if you need minimal latency.

---

## Power-user notes

* `ModelKey` is passed straight to your LLM client; you can change models per run.
* `UseIsComplete` requests a `<Done>true</Done>` when the objective is met; `MaxEpochs` still hard-caps the run.
* `NaturalLanguageResponse` adds a final LLM pass for clean prose.
* The agent records per-epoch trails in `dbo.Episodics` (plans, inputs, results, notes, schema snapshot).
* `UseSearch` is a planning assist; the agent favors your live data over external claims.

---

## Example scenarios

**Safe analytics on production**

* Enable QueryOnly (and optionally containment).
* Prompt: summarize total sales by month for a year and list the top customers with totals.

**Import a docs folder and analyze**

* Use ImportFolder on your repo/docs folder.
* Prompt: count files mentioning a term (case-insensitive) and list top file names by occurrences.

**Verify tricky SQL behavior with web search**

* Enable UseSearch.
* Prompt: verify a canonical pattern (e.g., top-k per group with ties via window functions) and implement it for your tables.

**Export for migration**

* Use ExportSchema for DDL and ExportData for small XML snapshots.

---

## Final notes

* Target framework: `net9.0-windows` (WPF).
* Defaults favor developer convenience (LocalDB + trusted connection).
* For production, combine least privilege, QueryOnly, SqlContain hardening, and only enable UseSearch when it truly adds value.
