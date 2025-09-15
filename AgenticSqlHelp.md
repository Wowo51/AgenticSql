# AgenticSQL App: Help & User Guide

Welcome! This guide walks you through using the **AgenticSQL** desktop app (WPF, .NET 9). It covers first-run setup, the main workflow, switches & safety, importing/exporting, and troubleshooting—**in the same style** as the library guide you saw earlier.

---

## What the app does

* Creates or connects to a SQL Server database (defaults to **LocalDB**).
* Runs an **iterative SQL agent** that plans and executes T-SQL in short “epochs”.
* Lets you **see the conversation/log**, save/open your **prompt** from disk, and **stop** the agent mid-run.
* Optional **containment (SqlContain)** hardening switches.
* Utilities to **import a folder of files** into a table, and to **export** schema or data.

---

## First-run setup

1. **LLM API key path (required)**

   * The app looks for an OpenAI API key file in the MainWindow.xaml.cs constructor code:

     ```csharp
     LLM.OpenAiKeyPath = @"PathToYourKey";
     ```
   * Make sure this file exists and contains a valid key. If you’re building your own binary, update the path in `MainWindow.xaml.cs` before running.

2. **SQL Server**

   * If you don’t specify anything, the app defaults to:

     ```
     Server=(localdb)\MSSQLLocalDB;Database=<Db Name>;Trusted_Connection=True;TrustServerCertificate=True;
     ```
   * The **Db Name** field on the toolbar controls the database name (default `AgenticDb`).
     The app will **create the database if missing** and connect automatically.

---

## The Main Window

Top toolbar (left→right; items wrap if your window is narrow):

* **File ▾**

  * **Open Prompt…** — Load a `.txt` or `.prompt` file into the Prompt pane.
  * **Save Prompt** — Save current prompt to the last path.
  * **Save Prompt As…** — Choose a new path to save the prompt.

* **StartAgent** / **StopAgent**

  * Start runs the agent for your current **Prompt** and current **settings**.
  * Stop cancels the run safely.

* **MaxEpochs**
  Maximum planning steps before the agent stops on its own.

* **UseIsComplete**
  Lets the agent decide to stop early when its goals are met.

* **QueryOnly**
  Enforces **read-only** queries (no mutation/DDL). See **Safety & read-only** below.

* **NaturalLanguageResponse**
  Adds a final LLM pass to rewrite the result as a human-readable answer.

* **Db Name**
  Name of the database to create/connect to (LocalDB by default).

* **CopyLog / ClearLog / ClearAllButLast**
  Manage the log pane’s content.

* **ImportFolder / ExportData / ExportSchema**
  One-click utilities—details below.

* **ModelKey**
  LLM model key string the agent will send to `OpenAILLM`. Default is `gpt-5-mini`. You can type a different one.

* **ContainServer / ContainDatabase**
  Run **SqlContain** hardening before the agent starts; the app **won’t start** if hardening fails.

### Panes

* **Input** (left): Your **Prompt**—plain text, multi-line. Save and reuse via File menu.
* **Output (most recent lines)** (right): A **live log** of the run; it autoscrolls and only shows the most recent portion for speed. Use **CopyLog** to grab the entire buffered log.

---

## Typical workflow

1. **Write your Prompt**

   * Example:

     ```
Generate articles describing ways ensuring agentic systems are safe and complete the final task before quitting.

Every statement labeled Epoch should take one epoch to complete.

Epoch:
Ensure that a Memory table with a Name and Content column exists.

Epoch:
Load the existing Memory rows to ensure you are not writing more than one article on one feature.

Epoch:
Write a long form article listing 3 different principles that could be used to ensure that agentic systems are safe. Add that article to the Memory rows.

1 Epoch per article:
Select one of the 3 principles and write a long form article about it. Add that article to the end of the Memory rows.
 Repeat this step until you have written one article for each of the three principles.

Epoch:
The final task is to perform a query that retrieves all of the rows of the Memory table in the order they were created with content.

Completion:
If the final task succeeded then quit.
     ```

2. **Pick settings**

   * **MaxEpochs**: 5–10 is a good start.
   * **UseIsComplete**: On for most tasks.
   * **QueryOnly**: Leave off for schema changes; turn **on** for analytics-only sessions.
   * **NaturalLanguageResponse**: Turn on if you want a concise final human summary.
   * **Db Name**: Accept default or choose a project-specific name.
   * **ModelKey**: Use your preferred model (e.g., `gpt-5-mini`, `gpt-4o`, etc.).
   * **Containment**: Toggle **ContainServer**/**ContainDatabase** if you need extra safety (requires proper permissions).

3. **StartAgent**

   * The log will show each epoch’s planning and what was executed.
   * The agent builds context (schema + last results), asks the LLM for a **strict `<SqlXmlRequest>`**, executes, and repeats.

4. **StopAgent** (optional)

   * Cancel any time; the app cleans up UI state and logs “Agent canceled.”

5. **Review results**

   * The agent logs and prints a “Final State” (or a natural-language answer if enabled).
   * **CopyLog** to clipboard for pasting into your notes.

---

## Safety & read-only

Turn **QueryOnly** on to force read-only analytics:

* The agent must produce **`CommandType=Text`** and **no mutation** SQL.
* The app runs a **mutation scanner** that strips comments/strings/identifiers and checks for:

  * DML (INSERT/UPDATE/DELETE/MERGE), DDL (CREATE/ALTER/DROP/TRUNCATE/…),
  * temp tables / table variables creation, `SELECT … INTO`,
  * `EXEC`, `sp_executesql`, `xp_*`, security GRANT/REVOKE/DENY,
  * transactions, `DBCC`, backup/restore, linked server ops, etc.
* If anything looks risky, the epoch is **blocked**, and the agent continues planning (or stops if out of epochs).

> **Tip:** Use **ContainServer** or **ContainDatabase** to run **SqlContain** before starting the agent. If hardening fails, the app refuses to run the agent (fail-closed).

---

## Database connection logic (how the app picks a connection)

You only see **Db Name** in the UI; the app resolves a connection like this:

1. If a full **ConnectionString** is present in the ViewModel, it’s used (UI currently doesn’t expose this field).
2. Else, if a **ServerConnectionString** is present, the app **adds `Database=<Db Name>`** if missing (UI doesn’t expose this field by default).
3. Else, it falls back to **LocalDB**:

   ```
   Server=(localdb)\MSSQLLocalDB;Database=<Db Name>;Trusted_Connection=True;TrustServerCertificate=True;
   ```

When you click **StartAgent**, the app will **create the database if it doesn’t exist**, then connect.

---

## Containment (SqlContain)

If **ContainServer** or **ContainDatabase** is checked, the app:

* Derives server, auth, and database from the same connection logic above.
* Chooses scope:

  * **Both** if both toggles are on,
  * **Instance** if only **ContainServer**,
  * **Database** if only **ContainDatabase**.
* Runs the hardener. If it returns non-zero or throws, the app **logs an error** and **aborts** the run.
  *(You need the necessary SQL permissions for hardening.)*

---

## Import / Export utilities

### ImportFolder

1. Click **ImportFolder**.
2. Pick a folder. The app:

   * Recursively scans all files.
   * Creates or reuses a target table named after the **folder**:

     * Sanitized into a valid SQL identifier (letters/digits/underscores, trimmed, `T_` prefix if needed, avoids a small reserved list).
     * Schema is `dbo`.
   * Reads each file as **Name** (relative path), **Time** (last write UTC), **Content** (full text).
   * Calls `SqlTools.FileImporter.ImportAsync` to load rows.
     *(The UI doesn’t prompt for truncation; it uses `truncateFirst=false` by default.)*
3. Watch progress in the log.

**Use cases:** Bring a docs/code folder into SQL for text search, build embeddings pipelines, or snapshot content before analytics.

### ExportSchema

* Click **ExportSchema** to generate **CREATE/ALTER** SQL for your DB via `SqlTools.ExportSchema.Export(conn)`.
* The SQL is printed to the log in chunks (so the UI stays responsive).
* Copy the log to save or reapply elsewhere.

### ExportData

* Click **ExportData** to dump data as **XML** via `SqlTools.ExportDataXml.Export(conn, includeEmptyTables: true)`.
* Useful for backups, diffing, or moving small databases between environments.

---

## Prompts: open/save

* **Open Prompt…** loads a text file into the Input pane and remembers its path.
* **Save Prompt** writes back to the remembered path.
* **Save Prompt As…** lets you choose a new file; the app remembers that new path.
* Log messages confirm success or show errors.

---

## Logging behavior

* The app buffers a large number of lines in memory (with a cap) and shows **only the most recent chunk** in the Output pane for speed.
* Use:

  * **CopyLog** — copies **all** buffered log lines to the clipboard.
  * **ClearLog** — clears the buffer and the visible pane.
  * **ClearAllButLast** — keeps only the last line (handy after long runs).

> Large payloads (schema/data exports, big XML) are automatically split into smaller log blocks to keep the UI responsive.

---

## Tips for effective prompts

* Be **goal-oriented**: “Create X if missing, then do Y and summarize Z.”
* Specify **ordering, limits, and filters** to keep result sets small (the agent truncates overly large result contexts).
* For **read-only** analysis, enable **QueryOnly** and ask for summaries:
  “Compute total orders by month for 2024 and list the top 5 customers.”

---

## Troubleshooting

* **“LLM.OpenAiKeyPath is not set.”**
  Update the path in `MainWindow.xaml.cs` and ensure the file contains a valid key.

* **Agent doesn’t start when containment is on**
  Check SQL permissions for SqlContain. The app logs the hardener scope and errors—fix issues and try again, or temporarily disable containment.

* **“Read-only mode: Potentially mutating SQL detected; blocked.”**
  Your prompt implies DDL/DML. Either turn off **QueryOnly** (unsafe on prod) or rephrase the prompt to pure analytics.

* **Nothing appears in the Output pane**
  The pane only shows recent lines. Use **CopyLog** to capture everything, or try **ClearLog** and run again.

* **Schema/exports are too big for the UI**
  That’s expected—the app chunks large outputs. Use **CopyLog** to extract and save to a file.

---

## Power-user notes

* **Model key** (text box) is passed straight to `OpenAILLM.LLM.Query`.
  You can change models between runs.
* **UseIsComplete** asks the LLM to return `<Done>true</Done>` when the objective is met.
  The agent still respects **MaxEpochs** as a hard stop.
* **NaturalLanguageResponse** triggers a final LLM prompt that rewrites the result into clean plain text. This is great for “report” style outputs.
* The agent writes a per-epoch trail to `dbo.Episodics` (plan prompts, inputs, results, episodic notes, schema snapshot). This can be invaluable for audits.

---

## Example scenarios

**Run safe analytics on production**

1. Check **QueryOnly** and (optionally) **ContainDatabase**/**ContainServer**.
2. Prompt:

   ```
   Summarize total sales by month for 2024 and return the top 10 customers by revenue with their totals.
   ```
3. Start. If the model tries mutating SQL, the scanner will block it and the agent will adjust.

**Import a docs folder and search**

1. Click **ImportFolder**, choose your docs repo folder.
2. Prompt:

   ```
   Count how many files mention 'invoice' in their Content column (case-insensitive).
   Then list the top 10 file Names that have the most occurrences.
   ```

**Export for migration**

* **ExportSchema**: grab the DDL.
* **ExportData**: grab XML for small datasets.

---

## FAQ

**Q: Where do I pick a non-LocalDB server?**
A: The UI focuses on Db Name + LocalDB for simplicity. If you extend the UI to expose a full **ConnectionString** or **ServerConnectionString**, the app will honor it and add `Database=...` if needed (see the connection logic above).

**Q: Can I stop mid-run?**
A: Yes—click **StopAgent**. The UI resets reliably and logs “Agent canceled.”

**Q: Is the Output pane the entire log?**
A: It shows the **most recent lines**. Use **CopyLog** for everything.

**Q: Can the app open/save prompts?**
A: Yes—use **File ▾** on the toolbar.

---

## Final notes

* Target framework: **net9.0-windows**, WPF
* Defaults favor **developer convenience** (LocalDB, trusted connection).
* For production access, combine **least privilege**, **QueryOnly**, and **SqlContain** hardening.
