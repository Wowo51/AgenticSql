# AgenticSql: Developer Guide

This guide shows how to use the library in real code, with **`SqlAgent`** as the primary entry point. It also covers the lower-level XML/string façade (`SqlStrings`) if you need finer control.
New: the agent can optionally call your LLM’s **Search** endpoint to use *internet results* during planning.

---

## Quickstart

```csharp
using AgenticSql;
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // 1) Create (if needed) and connect to a database
        var agent = await AgentFactory.CreateAgentWithDefaultServerAsync(
            dbName: "AgenticDb",
            maxEpochs: 5 // hard stop after N planning steps
        );

        // 2) Optional agent configuration (see “SqlAgent knobs” below)
        agent.ModelKey = "gpt-5-nano";
        agent.UseIsComplete = true;            // let the agent decide when done
        agent.QueryOnly = false;               // set true for read-only mode
        agent.NaturalLanguageResponse = false; // last-pass humanized answer
        agent.UseSearch = true;                // <-- NEW: enable internet search

        // 3) Run an objective
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string final = await agent.RunAsync(
            prompt: "Create a Customers table if missing, then list the first 10 rows.",
            CallLog: msg => Console.WriteLine("[Agent] " + msg),
            ct: cts.Token
        );

        Console.WriteLine("\n=== Agent Final Output ===\n" + final);
    }
}
```

---

## Creating an `SqlAgent`

`SqlAgent` instances are created through **`AgentFactory`** (constructors are private):

* **Create (if missing) & connect using an explicit server connection string**

  ```csharp
  var agent = await AgentFactory.CreateAgentAsync(
      serverConnectionString: "Server=localhost;Integrated Security=True;TrustServerCertificate=True",
      dbName: "AgenticDb",
      maxEpochs: 5
  );
  ```

* **Connect to an existing database using a full connection string**

  ```csharp
  var agent = await AgentFactory.CreateAgentFromConnectionStringAsync(
      connectionString: "Server=localhost;Database=AgenticDb;Integrated Security=True;TrustServerCertificate=True",
      maxEpochs: 5
  );
  ```

* **Use a default server (good for dev/localdb)**

  ```csharp
  // Env overrides honored: AGENTICSQL_SERVER_CONNSTR or AGENTICSQL_CONNSTR
  var agent = await AgentFactory.CreateAgentWithDefaultServerAsync("AgenticDb", maxEpochs: 5);
  ```

> Under the hood the factory:
>
> 1. emits `<CreateDatabaseInput/>` XML to ensure the DB exists,
> 2. emits `<ConnectInput/>` XML to attach, and
> 3. returns a ready `SqlAgent`.

---

## Running the Agent

```csharp
string output = await agent.RunAsync(
    prompt: "Import CSV files from ./data into tables and summarize totals per month.",
    CallLog: s => System.Diagnostics.Debug.WriteLine(s),
    ct: CancellationToken.None
);
```

* The agent iterates up to `maxEpochs`.

* Each epoch it:

  1. Retrieves schema (`GetSchemaAsyncStr`)
  2. **Plans**: asks your LLM to produce a strict `<SqlXmlRequest>` payload

     * If `agent.UseSearch == true` → calls `OpenAILLM.LLM.SearchAsync(...)`
     * Else → calls `OpenAILLM.LLM.Query(...)`
  3. Executes it (`ExecuteToXmlAsync`)
  4. If it was a query, folds results into context
  5. Optionally checks **“Done?”** with `UseIsComplete`

* All steps, inputs, and outputs are **logged** through your `CallLog` delegate.

* The agent writes a rolling **Episodic** trail to `dbo.Episodics` (auto-created).

**Return value**:

* If `NaturalLanguageResponse == false` (default), you get a **Final State** text that includes the last query XML.
* If `NaturalLanguageResponse == true`, the agent makes one last LLM pass to produce a **clean, human-readable answer**.

---

## Internet Search (what it changes)

When **`UseSearch = true`**, planning uses the LLM’s search-capable endpoint:

* The agent sends the same planning prompt, but through `LLM.SearchAsync(...)`.
* The LLM may ground its reasoning on fresh web snippets, then must still return **only** a valid `<SqlXmlRequest>...</SqlXmlRequest>`.
* Execution against SQL is unchanged; only the **planning** call differs.
* The agent appends the **reported token/search cost** to the `QueryInput` episodic field each epoch.

> Tip: Use Search for open-ended analytics (“industry-standard date dimensions,” “best-practice indexing steps”) or when your prompt benefits from up-to-date references. Keep `QueryOnly=true` if you just want safe exploration.

---

## `SqlAgent` knobs (properties you can set)

```csharp
agent.ModelKey = "gpt-5-nano";   // passed to OpenAILLM.LLM.* calls
agent.UseIsComplete = true;      // ask LLM <Done>true/false</Done> each epoch
agent.QueryOnly = false;         // true = hard read-only mode (see below)
agent.NaturalLanguageResponse = false;  // final NL synthesis pass
agent.MaximumLastQueryOutputLength = 25_000; // context size guard
agent.UseSearch = true;          // NEW: use internet search during planning
```

### Read-Only Mode (`QueryOnly = true`)

* Blocks anything that could change state.
* Enforcement layers:

  * Requires `CommandType == Text`
  * Runs **`AgenticSql.Security.SqlMutationScanner`** on the planned SQL
  * If anything risky is detected (INSERT/UPDATE/DDL/etc.), the step is rejected
* When enabled, the agent returns the **LLM’s `<SqlXmlRequest>`** it produced rather than the final-state XML.

Use this in UIs or when exploring a production database.

---

## What gets saved to `dbo.Episodics`?

One row per epoch:

* `EpisodeId` (guid), `EpochIndex`, `Time`
* `PrepareQueryPrompt` (full planning prompt)
* `QueryInput` (the `<SqlXmlRequest>` the LLM returned **plus cost**)
* `QueryResult` (the `<SqlResult>` XML from execution)
* `EpisodicText` (rolling StateOfProgress/NextStep)
* `DatabaseSchema` (schema snapshot)

The table is **auto-created** and **cleared at run start**:

```csharp
await _sql.EnsureEpisodicsTableAsync();
await SqlAgent.ClearEpisodicsAsync(_sql.Database);
```

---

## Advanced: Use the string/XML façade (`SqlStrings`)

Everything the agent does is available directly via **`SqlStrings`**. Useful if you:

* generate XML yourself,
* want to drive the DB without the agent loop,
* or need to unit test specific commands.

### Connect

```csharp
var sql = new SqlStrings();
string connectXml = $@"<ConnectInput><ConnectionString>{System.Security.SecurityElement.Escape(connStr)}</ConnectionString></ConnectInput>";
string result = await sql.ConnectAsyncStr(connectXml); // <Result success="true" ...
```

### Get schema (as structured XML)

```csharp
string schemaXml = await sql.GetSchemaAsyncStr("<Empty/>"); // see InputXmlSchemas.EmptyXsd()
```

### Execute text SQL (query or non-query)

```csharp
var req = new SqlStrings.SqlTextRequest {
    Sql = "SELECT TOP (10) name, object_id FROM sys.tables ORDER BY name",
    ExecutionType = SqlStrings.SqlExecutionType.Query
};
string xml = await sql.ExecuteAsync(req); // <Result success="true"><Rows>...
```

**String-in helper**:

```csharp
string inXml = """
<SqlTextRequest>
  <Sql>UPDATE dbo.Customers SET IsActive = 1 WHERE Id = @id</Sql>
  <ExecutionType>NonQuery</ExecutionType>
  <Parameters>
    <NameValue><Name>@id</Name><Value>42</Value></NameValue>
  </Parameters>
</SqlTextRequest>
""";
string outXml = await sql.ExecuteAsyncStr(inXml);
```

### Execute with full `<SqlXmlRequest>` (richer shape, result-sets, params echo, messages)

```csharp
var x = new SqlXmlRequest {
    Sql = "SELECT TOP (3) * FROM sys.databases",
    CommandType = System.Data.CommandType.Text
};
string xml = await sql.ExecuteToXmlAsync(x); // returns <SqlResult> ... </SqlResult>
```

**String-in**:

```csharp
string inXml = """
<SqlXmlRequest>
  <Sql>EXEC sys.sp_who</Sql>
  <CommandType>StoredProcedure</CommandType>
  <Parameters>
    <XmlSerializableSqlParameter>
      <ParameterName>@RETURN_VALUE</ParameterName>
      <Direction>ReturnValue</Direction>
      <SqlDbType>Int</SqlDbType>
    </XmlSerializableSqlParameter>
  </Parameters>
</SqlXmlRequest>
""";
string xml = await sql.ExecuteToXmlAsyncStr(inXml);
```

---

## Schema/XSD helpers (great for LLM prompts)

Embed live XSDs to make the LLM produce valid XML:

```csharp
string xsd = InputXmlSchemas.SqlXmlRequestXsd();   // XSD for <SqlXmlRequest>
string xsd2 = InputXmlSchemas.SqlTextRequestXsd(); // XSD for <SqlTextRequest>
var all = InputXmlSchemas.All();                   // Dictionary<string,string> of all XSDs
```

General-purpose XML helpers in `Common`:

* `Common.ExtractXml(string)` – pulls the first XML blob from noisy text
* `Common.FromXml<T>(string)` – deserialize if valid
* `Common.ToXmlSchema<T>()` – generate an XSD for a .NET type

---

## Security posture

* The agent does **not** request external IO *inside SQL* (no files, network, CLR, `xp_cmdshell`, etc.).
* With **`UseSearch = true`**, the LLM’s **planning** call may fetch web content, but SQL execution remains local and constrained.
* In **read-only** mode:

  * forces `CommandType=Text`
  * runs **`SqlMutationScanner`** to detect DML/DDL, temp tables, transactions, perms, external/linked server usage, etc.
* Still follow best practice:

  * Use least-privilege credentials (e.g., `SELECT` only for read-only).
  * Consider running in a never-commit transaction for exploration.
  * Keep `TrustServerCertificate=True` only where appropriate.

---

## Cancellation & logging

* `RunAsync` accepts a `CancellationToken`. If you cancel mid-epoch, the current step throws and the agent stops.
* All significant steps are surfaced via `CallLog(string)` so you can mirror progress into a UI.

---

## Troubleshooting

* **Malformed XML from LLM / Search**
  The agent uses `Common.FromXml<T>` with `ExtractXml` to tolerate leading chatter. If models still emit broken XML, show the **XSD** (`InputXmlSchemas.SqlXmlRequestXsd()`) in your prompt and require: “Return ONLY one well-formed `<SqlXmlRequest>...</SqlXmlRequest>`.”

* **Read-only block**
  “Read-only mode: Potentially mutating SQL detected; blocked.” → The mutation scanner matched INSERT/UPDATE/DDL/etc. Rephrase the objective for descriptive analytics only, or disable `QueryOnly` (not recommended on prod).

* **Reserved word column names**
  If you store schema text, use a non-reserved column like `DatabaseSchema` (already implemented).

* **Local dev connection**
  Defaults to `(localdb)\MSSQLLocalDB` with Integrated Security. Override via `AGENTICSQL_SERVER_CONNSTR` or pass an explicit connection string to the factory.

---

## Minimal examples by task

**List tables (read-only + search):**

```csharp
agent.QueryOnly = true;
agent.UseSearch = true;
string final = await agent.RunAsync(
    "Show me all tables and their row counts. If helpful, ground on best-practice queries.",
    s => Console.WriteLine(s)
);
```

**Create table then query (no search):**

```csharp
agent.QueryOnly = false;
agent.UseSearch = false;
string final = await agent.RunAsync(
    "If dbo.Customers is missing, create it (Id int PK, Name nvarchar(200)), then select TOP(5) *.",
    Console.WriteLine
);
```

**Natural language final answer + search:**

```csharp
agent.NaturalLanguageResponse = true;
agent.UseSearch = true;
string final = await agent.RunAsync(
    "Summarize total sales by month for 2024, then produce a short plain-text explanation.",
    Console.WriteLine
);
```

**Drive the DB yourself (no agent):**

```csharp
var sql = new SqlStrings();
await sql.ConnectAsyncStr($@"<ConnectInput><ConnectionString>{System.Security.SecurityElement.Escape(connStr)}</ConnectionString></ConnectInput>");
string result = await sql.ExecuteAsyncStr("""
<SqlTextRequest>
  <Sql>SELECT TOP(10) name FROM sys.tables ORDER BY name</Sql>
  <ExecutionType>Query</ExecutionType>
</SqlTextRequest>
""");
```

---

## Notes

* Target framework: **.NET 9.0**
* ADO provider: **Microsoft.Data.SqlClient 6.1.1**
* The agent logs the **token/search cost** it gets back from `OpenAILLM.LLM.Query/SearchAsync` into the `QueryInput` episodic field for each epoch.
