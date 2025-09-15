//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
using Microsoft.Data.SqlClient;
using OpenAILLM;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticSql
{
    /// <summary>
    /// A simple, iterative SQL agent. Each epoch:
    ///  1) Builds context (DB schema + last query results, if any)
    ///  2) Asks the LLM for an <SqlTextRequest>…</SqlTextRequest> payload
    ///  3) Executes it via SqlStrings.ExecuteAsyncStr
    ///  4) If it was a query, folds results into next-epoch context
    ///  5) Calls IsComplete; if true, stops
    /// Returns final context (schema + last query results if last call was a query)
    /// </summary>
    public sealed class SqlAgent
    {
        private readonly SqlStrings _sql = null!;
        private readonly int _maxEpochs;
        public string ModelKey = "gpt-5-nano";
        public bool UseIsComplete = true;
        public bool QueryOnly = false;
        public bool NaturalLanguageResponse = false;
        public int MaximumLastQueryOutputLength = 25000;
        public bool UseSearch = false;

        public SqlAgent(SqlStrings sqlStrings, int maxEpochs = 5)
        {
            _sql = sqlStrings ?? throw new ArgumentNullException(nameof(sqlStrings));
            _maxEpochs = Math.Max(1, maxEpochs);
        }

        /// <summary>
        /// Run the agent. The schema is always part of context. The agent may stop early if IsComplete returns true.
        /// Returns the final context XML string (Schema + LastQueryResult if a query was last executed).
        /// </summary>
        public async Task<string> RunAsync(string prompt, Action<string> CallLog, CancellationToken ct = default)
        {
            CallLog("Agent started.");

            if (string.IsNullOrWhiteSpace(prompt)) prompt = "No prompt provided.";

            await _sql.EnsureEpisodicsTableAsync();
            await ClearEpisodicsAsync(_sql.Database);

            // 1) Always fetch schema as starting context
            string schemaXml = await _sql.GetSchemaAsyncStr("<Empty/>");
            //string lastQueryOutput = ""; //this is the short term memory.
            string episodic = await BuildEpisodicAsync("No data yet.", prompt, "No data yet.", "No data yet.", 0, ct);
            string error = "";
            int epoch = 0;
            string queryOutput = "No data yet.";
            for (epoch = 1; epoch <= _maxEpochs; epoch++)
            {
                CallLog($"Epoch {epoch} starting.");

                ct.ThrowIfCancellationRequested();

                string prepareQueryPrompt = BuildPrepareQueryPromptMulti(prompt, schemaXml, queryOutput, episodic, epoch, _maxEpochs, QueryOnly);
                if (error.Length > 0)
                {
                    prepareQueryPrompt += Environment.NewLine + "Prior error: " + error;
                }
                CallLog($"Prepare query prompt: {Environment.NewLine + prepareQueryPrompt}");
                //(string queryInput, double cost) = await LLM.Query(prepareQueryPrompt, ModelKey, null);
                //queryInput += Environment.NewLine + "The cost of this LLM call was: " + cost.ToString();
                string queryInput ="";
                double cost = 0;
                if (UseSearch)
                {
                    (queryInput, cost) = await LLM.SearchAsync(prepareQueryPrompt, ModelKey);
                }
                else
                {
                    (queryInput, cost) = await LLM.Query(prepareQueryPrompt, ModelKey, null);
                }
                queryInput += Environment.NewLine + "The cost of this LLM call was: " + cost.ToString();
                CallLog($"Query Input: {Environment.NewLine + queryInput}");
                var xmlReq = Common.FromXml<SqlXmlRequest>(queryInput);
                if (xmlReq == null) { error = "LLM did not return valid <SqlXmlRequest> XML."; continue; }
                if (QueryOnly)
                {
                    if (xmlReq.CommandType != System.Data.CommandType.Text)
                    {
                        error = "Read-only mode: only CommandType=Text is allowed.";
                        continue;
                    }
                    var hits = AgenticSql.Security.SqlMutationScanner.Scan(xmlReq.Sql);
                    if (hits.Count > 0)
                    {
                        error = "Read-only mode: Potentially mutating SQL detected; blocked.";
                        continue;
                    }
                }
                queryOutput = await _sql.ExecuteToXmlAsync(xmlReq);

                schemaXml = await _sql.GetSchemaAsyncStr("<Empty/>");
                CallLog($"Query Output: {Environment.NewLine + queryOutput}");

                //if (queryOutput != null && queryOutput != "")
                //{
                //    lastQueryOutput = queryOutput;
                //}

                if (QueryOnly)
                {
                    return queryInput;
                }

                episodic = await BuildEpisodicAsync(episodic, prompt, queryInput, queryOutput!, epoch, ct);
                CallLog("Episodic:" + Environment.NewLine + episodic);

                EpisodicRecord epiRec = new EpisodicRecord();
                epiRec.PrepareQueryPrompt = prepareQueryPrompt;
                epiRec.QueryInput = queryInput;
                epiRec.QueryResult = queryOutput!;
                epiRec.EpisodicText = episodic;
                epiRec.DatabaseSchema = schemaXml;
                epiRec.EpochIndex = epoch;
                await _sql.SaveEpisodicAsync(epiRec);

                if (UseIsComplete && await IsCompleteAsync(prompt, episodic, schemaXml, queryOutput, epoch, ct))
                {
                    break;
                }
            }

            if (NaturalLanguageResponse)
            {
                string finalResponse = await BuildFinalStateAsync(prompt, queryOutput!, episodic, ct);
                return finalResponse;
            }

            return BuildFinalStateXml(schemaXml, queryOutput!, epoch);
        }

        private string BuildPrepareQueryPromptMulti(
            string prompt,
            string schemaXml,
            string? lastQueryResultXml,
            string episodic,
            int epoch,
            int maxEpochs,
            bool queryOnly)
        {
            string sqlXmlRequestXsd = InputXmlSchemas.SqlXmlRequestXsd();
            var sb = new StringBuilder(64 * 1024);

            sb.AppendLine("You are a SQL planning assistant. Produce ONLY a <SqlXmlRequest> XML for the next step.");
            sb.AppendLine("The <SqlXmlRequest> has elements such as <Sql>, optional <Parameters>, <CommandType> (Text or StoredProcedure), and optional <CommandTimeoutSeconds>.");
            sb.AppendLine("The <Sql> must contain valid T-SQL only, consistent with the 'Database Schema' below. Do not attempt any external IO (files, network, CLR, xp_cmdshell, etc.).");
            sb.AppendLine("Multiple statements are allowed; separate them with semicolons. Do NOT use GO batch separators.");
            sb.AppendLine("Apply no safety effort where it is unnecessary, but dedicate at least 10% of your reasoning effort to safety and ethical considerations on tasks involving code, system specifications, AI design, or other outputs where security is required.");

            if (queryOnly)
            {
                sb.AppendLine("You are in READ-ONLY mode.");
                sb.AppendLine("Do NOT generate any statements that modify state: no INSERT/UPDATE/DELETE/MERGE, no DDL (CREATE/ALTER/DROP/TRUNCATE), no temp tables, no SELECT INTO, no transactions, no permission changes, and no EXEC of procedures that mutate data.");
                sb.AppendLine("If the Task Intent requests modifications, return a harmless diagnostic SELECT instead, e.g.: SELECT 'Read-only mode: cannot modify data' AS Error;");
                sb.AppendLine("Prefer <CommandType>Text</CommandType>.");
            }

            sb.AppendLine();
            sb.AppendLine($"Epoch: {epoch} of {maxEpochs}");
            sb.AppendLine("=== Task Intent ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(prompt) ? "(none provided)" : prompt.Trim());
            sb.AppendLine();

            sb.AppendLine("=== EPISODIC (rolling notes; plan, milestones, risks, assumptions) ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(episodic) ? "(empty)" : episodic);
            sb.AppendLine();

            sb.AppendLine("=== XSD for SqlXmlRequest (STRICTLY conform to this) ===");
            sb.AppendLine(sqlXmlRequestXsd);
            sb.AppendLine();

            sb.AppendLine("=== Database Schema (ALWAYS include in reasoning) ===");
            sb.AppendLine(schemaXml);
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(lastQueryResultXml))
            {
                sb.AppendLine("=== LastQueryResult (optional context) ===");
                if (lastQueryResultXml.Length > MaximumLastQueryOutputLength)
                {
                    sb.AppendLine("The LastQueryResult was too long to include in full. Consider writing queries that don't return as much data until the final iteration. Here is a truncated version:");
                    sb.AppendLine(lastQueryResultXml.Substring(0, MaximumLastQueryOutputLength));
                }
                else
                {
                    sb.AppendLine(lastQueryResultXml);
                }
                sb.AppendLine();
            }

            sb.AppendLine("Return ONLY one well-formed <SqlXmlRequest>…</SqlXmlRequest> payload. No commentary.");
            return sb.ToString();
        }

        private async Task<string> BuildEpisodicAsync(
            string prior,
            string prompt,
            string queryInputXml,
            string queryResultXml,
            int epoch,
            CancellationToken ct)
        {
            var instr = new StringBuilder();

            instr.AppendLine("You are assisting a SQL agent.");
            instr.AppendLine("Instructions:");
            instr.AppendLine("Given the context, write a StateOfProgress and a NextStep. Write a StateOfProgress stating what objectives are complete and what needs to be done. Write a NextStep that is a realistic step towards the objective");
            instr.AppendLine();
            instr.AppendLine("=== StateOfProgress ===");
            instr.AppendLine("- Completed: <brief bullet(s) of what is already done/succeeded>");
            instr.AppendLine("- Outstanding: <brief bullet(s) of what remains or issues to resolve>");
            instr.AppendLine();
            instr.AppendLine("=== NextStep (realistic) ===");
            instr.AppendLine("<one concrete next action that realistically advances the objective>");
            instr.AppendLine();
            instr.AppendLine();
            instr.AppendLine("The rest of the information below is context:");
            instr.AppendLine();

            instr.AppendLine($"--- Start Context ---");
            instr.AppendLine();
            instr.AppendLine("=== Objective ===");
            instr.AppendLine(string.IsNullOrWhiteSpace(prompt) ? "(none provided)" : prompt.Trim());

            instr.AppendLine();
            instr.AppendLine("=== PriorEpisodic (rolling) ===");
            instr.AppendLine(string.IsNullOrWhiteSpace(prior) ? "(empty)" : prior);

            instr.AppendLine();
            instr.AppendLine("=== LastQueryInput (echo) ===");
            instr.AppendLine(queryInputXml ?? string.Empty);

            instr.AppendLine();
            instr.AppendLine("=== LastQueryResult (echo) ===");
            instr.AppendLine(queryResultXml ?? string.Empty);

            (string llmOut, double cost) = await LLM.Query(instr.ToString(), ModelKey, null).ConfigureAwait(false);
            llmOut += Environment.NewLine + "The cost of this LLM call was: " + cost.ToString();
            ct.ThrowIfCancellationRequested();

            return llmOut?.Trim() ?? "(LLM produced no content)";
        }

        private static string SummarizeCritique(string prompt, string inXml, string outXml)
            => "Compared intent vs result; note mismatches, nulls, and row counts. (Implement finer diff if needed.)";

        private static string DerivePlanSnippet(string? prior, string outXml)
            => "Goal→subgoals updated. If rows missing, add targeted SELECT/CREATE/UPDATE next.";

        private static string ExtractNextStepHint(string inXml)
            => "Prepare minimal parameterized SQL aligned with the strict XSD; prefer Query unless DDL/DML required.";



        private static bool IsSuccess(string execResultXml)
        {
            // Looks for Result success="true"
            return execResultXml.IndexOf("success=\"true\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Lightweight "done?" check. We ask the LLM to respond with &lt;Done&gt;true/false&lt;/Done&gt;.
        /// If the tag can't be parsed, we default to false and keep iterating.
        /// </summary>
        private async Task<bool> IsCompleteAsync(
            string prompt,
            string episodic,
            string schemaXml,
            string lastExecXml,
            int epoch,
            CancellationToken ct)
        {
            var sb = new StringBuilder(32 * 1024);
            sb.AppendLine("All of the objectives stated by the Prompt and only the objectives described by the Prompt need to be met in order to be Done.");
            sb.AppendLine("Respond with ONLY <Done>true</Done> or <Done>false</Done>.");
            sb.AppendLine($"Epoch just finished: {epoch}");
            sb.AppendLine("=== Prompt ===");
            sb.AppendLine(prompt);
            sb.AppendLine("=== Episodic ===");
            sb.AppendLine(episodic);
            sb.AppendLine("=== Schema ===");
            sb.AppendLine(schemaXml);
            sb.AppendLine();
            sb.AppendLine("=== LastExecutionResult ===");
            sb.AppendLine(lastExecXml);

            (string llmOut, double cost) = await LLM.Query(sb.ToString(), ModelKey, null);
            var m = Regex.Match(llmOut ?? string.Empty, @"<Done>\s*(true|false)\s*</Done>", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFinalStateXml(string schemaXml, string? lastQueryResultXml, int epoch)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== Final State ===");
            sb.AppendLine("AgenticSql has stopped. Here is the final state of the short term memory.");
            sb.AppendLine("<ShortTermMemory>");
            if (lastQueryResultXml == "" || lastQueryResultXml == null)
            {
                sb.AppendLine("No data.");
            }
            else
            {
                sb.AppendLine(lastQueryResultXml);
            }
            sb.AppendLine("</ShortTermMemory>");
            sb.AppendLine("Epoch: " + epoch);
            sb.AppendLine("AgenticSql has stopped.");
            return sb.ToString();
        }

        public async Task<string> BuildFinalStateAsync(
            string inputPrompt,
            string existingResponse,
            string? stateOfProgress = null,
            CancellationToken ct = default)
        {
            string prompt = CreateFinishingPrompt(inputPrompt, existingResponse, stateOfProgress);

            (string llMResponse, double cost) = await LLM.Query(prompt, ModelKey, null);

            return llMResponse + Environment.NewLine + "AgenticSql has stopped.";
        }

        private static string CreateFinishingPrompt(string inputPrompt, string existingResponse, string? sop)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a precise assistant. Produce a final answer that meets the objectives in the user prompt. Respond in .txt, not .md.");
            sb.AppendLine();
            sb.AppendLine("=== USER PROMPT OBJECTIVES ===");
            sb.AppendLine(inputPrompt.Trim());
            sb.AppendLine();
            sb.AppendLine("=== EXISTING RESPONSE (DRAFT) ===");
            sb.AppendLine(existingResponse.Trim());
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(sop))
            {
                sb.AppendLine("=== STATE OF PROGRESS ===");
                sb.AppendLine("Summarize what is complete, what remains, and propose the next realistic step.");
                sb.AppendLine(sop!.Trim());
                sb.AppendLine();
            }

            sb.AppendLine("=== INSTRUCTIONS ===");
            sb.AppendLine("- Ensure the final answer directly satisfies the objectives in the USER PROMPT.");
            sb.AppendLine("- Improve clarity, fill gaps, and resolve inconsistencies in the existing response.");
            sb.AppendLine("- Keep any code blocks intact and runnable if present.");
            sb.AppendLine("- If uncertain, state the uncertainty and suggest the next step.");
            sb.AppendLine("- Output only the final answer (no extra meta commentary).");
            return sb.ToString();
        }

        public static async Task ClearEpisodicsAsync(SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            const string sql = "DELETE FROM dbo.Episodics;";

            using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
