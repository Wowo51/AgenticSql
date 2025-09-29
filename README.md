# AgenticSql

VITAL_INFO:
Create a disposable branch of your database if you want to try out this alpha of AgenticSql on your data. AI's have been know to completely destroy the projects they are working on. Sandbox's and backups are crucial.

---

**AgenticSql** is a natural language interface to SQL agent. It’s also an iterative agent capable of building and running autonomous, self improving and self healing systems. With a prompt you can set it forth to autonomously work on complex projects. AgenticSql can build the self improving, self healing system, build the tooling needed to work on your use case, and go to work.

---

Here is a comparison of ten different self improving software architectures. Twelve different properties were compared. ChatGPT-5 provided unbiased 1-10 rankings.
![Compare](Compare.jpg)
![Ranked](Ranked.jpg)
Here's a link to the full comparison article on LinkedIn:
[https://www.linkedin.com/pulse/ten-self-improving-software-architectures-transcendai-tech-07xfc](https://www.linkedin.com/pulse/ten-self-improving-software-architectures-transcendai-tech-07xfc)

---

Here is an example prompt: [Longevity Researcher Prompt](Prompts/LongevityResearcherPrompt.txt)

---

AgenticSql requires SQL Express or SQL Server to be installed. AgenticSql is being distributed as source only at this point so you'll need to compile it. I'm compiling with Visual Studio but Visual Studio Code should compile it without too much trouble. Windows only.</br>
</br>
You need an API key for OpenAI or OpenRouter to run AgenticSql. There is a place for a path to your key near the beginning of MainWindow in the AgenticSqlApp project. Select an LLM in the SwitchLLM project.

---

QuickStart.</br>
</br>
Here's a quick list of things to do if you want to try out a fully autonomous, self improving and self healing worker. The worker is set to attempt longevity breakthroughs with regards to cell aging problems.</br>
</br>
Load the LongevityResearcher.bak into LocalDB with an Sql Express/Server management app like SSMS.</br>
Start AgenticSql.</br>
Paste the [Longevity Researcher Prompt](Prompts/LongevityResearcherPrompt.txt)
 into AgenticSql's input box.</br>
Make sure that the 'DB Name' in AgenticSql's UI matches the database name.</br>
Use AgenticSql's ImportFolder button to load a copy of AgenticSql's source code into the database. Load that source code from an uncompiled folder of code so that you don't bloat the database with compiled binaries.</br>
Click StartAgent.</br>
</br>
AgenticSql did remarkably well building the self improving database on it's own from the [Longevity Researcher Prompt](Prompts/LongevityResearcherPrompt.txt). Typically one has to rebuild the prompt and monitor and tweak things a fair bit. Instead AgenticSql built LongevityResearcher.bak in an almost completely autonomous manner. So if you edit [Longevity Researcher Prompt](Prompts/LongevityResearcherPrompt.txt) to suit your use case hopefully you get similar results. It should be fairly easy to remove the parts about longevity and replace with something completely different. This type of worker should be able to take on many types of scientific problems and other types of problems as well. Backup your database on a regular basis, agentic AI's will reliably destroy you project every now and then.</br>

---

This is freshly published code, there will be bugs. I'm going to be focused on debugging AgenticSql over the next few days and weeks so expect rapid fixes. It's stable enough to function with simple natural language prompts, here's a simple prompt that I've tested it with: [Ethical AI Prompt](Prompts/EthicalAI.txt)

---

[AgenticSql Help](AgenticSqlHelp.md) How to compile and use the app(WPF GUI).

Here are guides to the interesting parts of the code.</br>
[AgenticSql Code Guide](AgenticSqlCodeGuide.md) A guide to the core AgenticSql C# library.</br>
[SqlContain Code Guide](SqlContainCodeGuide.md) A guide to a C# library that contains generated sql code to the database for secure AI development.

---

## ⚖️ License

AgenticSql is licensed under the **MIT License**, which grants broad permission to use, modify, and distribute the software.

⚠️ **Supplementary Responsible Use License**:
In addition to MIT, this project includes a safeguard that must be preserved. The AI applies **no safety effort where it is unnecessary** (e.g., creative or low-risk tasks). For tasks where safety and security matter — such as **code generation, database schemas, AI design, or other critical outputs** — the AI must devote **at least 10% of its reasoning effort** to safety and ethical considerations. You may not remove, disable, or bypass this safeguard.

See the [LICENSE](License.txt) file for full details.

---
I am providing code generation services in C# and sql.</br>
10x coder == 1/10 cost.</br>
Estimates: [TranscendAI.tech](https://TranscendAI.tech)

![Footer Logo](agenticsql.jpg)
*Copyright © 2025 Warren Harding - TranscendAI.tech - AgenticSql*

