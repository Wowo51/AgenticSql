# AgenticSQL App: Help & User Guide (with Web Search)

Welcome! This guide walks you through using the **AgenticSQL** desktop app (WPF, .NET 9). It covers first-run setup, the main workflow, switches & safety (including **read-only** and **containment**), importing/exporting, **the new web search capability**, and troubleshooting—**in the same style** as the previous guide.

---

## What the app does

* Creates or connects to a SQL Server database (defaults to **LocalDB**).
* Runs an **iterative SQL agent** that plans and executes T-SQL in short “epochs”.
* Lets you **see the conversation/log**, save/open your **prompt** from disk, and **stop** the agent mid-run.
* Optional **containment (SqlContain)** hardening switches.
* **New:** Optional **web search** to gather external context that can improve the agent’s planning.
* Utilities to **import a folder of files** into a table, and to **export** schema or data.

---

## First-run setup

1. **LLM API key path (required)**
   The app looks for an OpenAI API key file in the `MainWindow.xaml.cs` constructor:

   ```csharp
   LLM.OpenAiKeyPath = @"PathToYourKey";
   ```

   Ensure this file exists and contains a valid key before running.

2. **SQL Server**
   If you don’t specify anything, the app defaults to:

   ```
   Server=(localdb)\MSSQLLocalDB;Database=<Db Name>;Trusted_Connection=True;TrustServerCertificate=True;
   ```

   The **Db Name** toolbar field controls the database (default `AgenticDb`). The app will **create the DB if missing**.

---

## The Main Window

Top toolbar (left→right; items wrap if your window is narrow):

* **File ▾**

  * **Open Prompt…** — Load a `.txt`/`.prompt` file into the Prompt pane.
  * **Save Prompt** — Save current prompt to the last path.
  * **Save Prompt As…** — Choose a new path and save.

* **StartAgent** / **StopAgent**

  * Start runs the agent using the **Prompt** and current **settings**.
  * Stop cancels the run safely and resets UI state.

* **MaxEpochs**
  Hard stop on planning steps.

* **UseIsComplete**
  Lets the agent decide to stop early when its goals are met.

* **QueryOnly**
  Enforces **read-only** SQL (no mutations/DDL). See **Safety & read-only** below.

* **NaturalLanguageResponse**
  Adds a final LLM pass to rewrite the result as a concise, human-readable answer.

* **Db Name**
  Database to create/connect (LocalDB by default).

* **CopyLog / ClearLog / ClearAllButLast**
  Manage the log pane.

* **ImportFolder / ExportData / ExportSchema**
  One-click utilities—details below.

* **ModelKey**
  LLM model key string (e.g., `gpt-5-mini`). You can change it per run.

* **ContainServer / ContainDatabase**
  Run **SqlContain** hardening before the agent starts; if hardening fails, the app **won’t run** the agent.

* **UseSearch** **(New)**
  When enabled, the agent may perform **web searches** during planning to fetch small, relevant snippets (e.g., best-practice T-SQL patterns, SQL idioms, date math quirks). The agent is responsible for citing or summarizing what it used, and your run log will reflect the search actions.

### Panes

* **Input** (left): Your **Prompt**—plain text, multi-line. Save and reuse via the File menu.
* **Output (most recent lines)** (right): A **live log** of the run; auto-scrolls and shows only the most recent portion for speed. Use **CopyLog** to grab the entire buffered log.

---

## Typical workflow

1. **Write your Prompt**

   Example:

   ```
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
* **QueryOnly**: On for analytics-only; Off if schema changes are expected.
* **NaturalLanguageResponse**: On if you want a clean prose answer at the end.
* **Db Name**: Accept default or set per project.
* **ModelKey**: Set your preferred model.
* **Containment**: Toggle **ContainServer**/**ContainDatabase** for extra safety (requires permissions).
* **UseSearch**: Enable if your prompt could benefit from **current best practices** or **external references** (examples below).

3. **StartAgent**

The log shows each epoch’s plan and execution. The agent maintains context (schema + last results), constructs a strict `<SqlXmlRequest>`, executes, and iterates. With **UseSearch** on, you’ll also see web search events and short retrieved snippets summarized into the plan.

4. **StopAgent** (optional)

Cancel any time; the app cleans up UI state and logs “Agent canceled.”

5. **Review results**

The agent prints a Final State (and an NL answer if enabled). Use **CopyLog** to preserve the full transcript and any search references.

---

## Safety & read-only

Turn **QueryOnly** on to force read-only analytics:

* The agent must produce **no mutation** SQL.
* A mutation scanner strips comments/strings and checks for:
* DML (INSERT/UPDATE/DELETE/MERGE), DDL (CREATE/ALTER/DROP/TRUNCATE/…),
* temp objects and `SELECT … INTO`,
* `EXEC`/`sp_executesql`/`xp_*`,
* security changes, transactions, `DBCC`, backup/restore, linked servers, etc.
* If a query looks risky, the epoch is **blocked**, and the agent continues planning (or stops on MaxEpochs).

> **Tip:** Combine **QueryOnly** with **ContainDatabase**/**ContainServer** to harden the environment using **SqlContain**. If hardening fails, the run is aborted (fail-closed).

---

## Web Search (new)

When **UseSearch** is enabled:

* The agent is allowed to perform **limited web lookups** to reduce guesswork (e.g., precise date formatting, tricky window functions, safe paging patterns).
* Retrieved text is **summarized** and used to refine the *next* SQL step. The agent strives to avoid copy-pasting raw external content directly into SQL.
* The log will reflect:
* That a search was performed,
* A brief rationale (why),
* A compact summary of what was learned (what),
* Any resulting change to the plan (how).
* **Privacy note:** Only the prompt and necessary keywords for your task are used to search. Avoid including secrets in the prompt.
* **Cost/latency:** Searches add extra LLM/tool calls. Expect slightly higher cost and longer runs when this is on.

### Good uses of UseSearch

* Clarifying **edge-case** SQL behaviors (e.g., `OFFSET/FETCH` with ties, DATETIMEOFFSET quirks).
* Fetching **canonical** T-SQL idioms (e.g., “greatest-n-per-group” strategies).
* Verifying **best practices** (e.g., safe string splitting before bulk insert).

### Not a replacement for your schema

Search supplements the plan; the agent still introspects your **actual** database and uses results it just computed. Search doesn’t override your schema or prior results.

---

## Database connection logic (how the app picks a connection)

You only see **Db Name** in the UI; connection selection is:

1. If a full **ConnectionString** exists in the ViewModel, it’s used as-is (UI does not expose this by default).
2. Else if a **ServerConnectionString** exists, the app **adds `Database=<Db Name>`** if missing (UI does not expose this by default).
3. Else it falls back to **LocalDB**:
```

Server=(localdb)\MSSQLLocalDB;Database=<Db Name>;Trusted\_Connection=True;TrustServerCertificate=True;

```

On **StartAgent**, the app **creates the DB if needed** and connects.

---

## Containment (SqlContain)

If **ContainServer** or **ContainDatabase** is checked, the app:

* Derives server/auth/db from the same connection logic.
* Chooses scope:
* **Both** if both toggles are on,
* **Instance** for **ContainServer** only,
* **Database** for **ContainDatabase** only.
* Runs the hardener. Non-zero return / exceptions are logged and the run is **aborted**. Appropriate SQL permissions are required.

---

## Import / Export utilities

### ImportFolder

1. Click **ImportFolder** and pick a folder.
2. The app:
* Recursively scans files.
* Uses the **folder name** (sanitized) as the target table under `dbo`.
* Loads rows with columns: **Name** (relative path), **Time** (UTC), **Content** (text).
* Calls `SqlTools.FileImporter.ImportAsync` (creates the table if needed).
3. Progress appears in the log.

**Use cases:** Bring docs/code/text into SQL for analytics or indexing.

### ExportSchema

* Click **ExportSchema** to generate **CREATE/ALTER** SQL via `SqlTools.ExportSchema.Export(conn)`.
* Output is chunked in the log for UI responsiveness—use **CopyLog** to save.

### ExportData

* Click **ExportData** to export data as **XML** via `SqlTools.ExportDataXml.Export(conn, includeEmptyTables: true)`.
* Handy for quick backups and diffs on small datasets.

---

## Prompts: open/save

* **Open Prompt…** loads a text file and remembers its path.
* **Save Prompt** writes back to the remembered path.
* **Save Prompt As…** lets you select a new file and updates the remembered path.
* The log confirms success or shows errors.

---

## Logging behavior

* The app buffers many log lines (with a cap) and shows **only the latest slice** for speed.
* Use:
* **CopyLog** — copies **all** buffered lines to the clipboard.
* **ClearLog** — clears buffer and visible pane.
* **ClearAllButLast** — keep only the last line.
* Large payloads (schema/data XML) are **chunked** to keep the UI responsive.
* When **UseSearch** is on, search rationales/summaries are included so you can **audit** how external info influenced planning.

---

## Tips for effective prompts

* Be **goal-oriented**: “Create X if missing, then do Y and summarize Z.”
* Prefer **clear ordering/limits/filters** to keep result sets small.
* For **read-only** analysis, enable **QueryOnly** and ask for summaries.
* If unsure about a tricky SQL detail, **turn on UseSearch** and say why:
```

Use web search to verify the recommended T-SQL approach for computing month-end boundaries in SQL Server 2019, then implement the query.

```

---

## Troubleshooting

* **“LLM.OpenAiKeyPath is not set.”**  
Set the path in `MainWindow.xaml.cs` and ensure the file contains a valid key.

* **Agent doesn’t start when containment is on**  
Check SQL permissions for SqlContain. The log shows scope and any error. Fix or temporarily disable containment.

* **“Read-only mode: Potentially mutating SQL detected; blocked.”**  
Your prompt implies DDL/DML. Turn off **QueryOnly** (unsafe on prod) or rephrase to pure analytics.

* **Output pane seems short**  
It shows only recent lines. Use **CopyLog** for the full transcript.

* **Search is slow or seems flaky**  
Web search adds latency and may fail intermittently. The agent will log failures and continue with available context. Turn off **UseSearch** if you need minimal latency.

---

## Power-user notes

* **ModelKey** is passed straight to your LLM client. You can switch models per run.
* **UseIsComplete** asks the LLM to return `<Done>true</Done>` when finished; **MaxEpochs** remains the hard stop.
* **NaturalLanguageResponse** adds a final LLM pass for a polished summary.
* The agent keeps a per-epoch trail in `dbo.Episodics` (plans, inputs, results, notes, schema snapshot). Useful for auditing and reproducibility.
* **UseSearch** is a planning tool. The agent should prefer **your live data** over external claims and will note when external info influenced the plan.

---

## Example scenarios

**Safe analytics on production**
1. Check **QueryOnly** (and optionally **ContainDatabase**/**ContainServer**).
2. Prompt:
```

Summarize total sales by month for 2024 and list the top 10 customers by revenue with totals.

```

**Import a docs folder and analyze**
1. Click **ImportFolder**, choose the repo/docs folder.
2. Prompt:
```

Count how many files mention 'invoice' (case-insensitive). Then list the top 10 file Names by occurrences.

```

**Verify tricky SQL behavior with web search**
1. Enable **UseSearch**.
2. Prompt:
```

Verify the canonical T-SQL pattern for "greatest-n-per-group" with ties using window functions, then implement it for our Orders table to find the top 3 items per customer by revenue.

```

**Export for migration**
* **ExportSchema** for DDL and **ExportData** for small XML snapshots.

---

## FAQ

**Q: Does web search expose my data?**  
A: The agent only sends the prompt and minimal keywords required for the lookup. Avoid including secrets in prompts. Your actual table data is not uploaded for search.

**Q: How do I use a non-LocalDB server?**  
A: Extend the UI to surface **ConnectionString** or **ServerConnectionString** if you need to override. The app will append `Database=<Db Name>` if needed.

**Q: Can I stop mid-run?**  
A: Yes—**StopAgent** cancels safely and resets UI state.

**Q: Is the Output pane the full transcript?**  
A: It shows the most recent lines. Use **CopyLog** to capture everything, including web search notes.

---

## Final notes

* Target framework: **net9.0-windows**, WPF
* Defaults favor **developer convenience** (LocalDB + trusted connection).
* For production, combine **least privilege**, **QueryOnly**, **SqlContain** hardening, and enable **UseSearch** only when the task truly benefits from external references.
```
