# AgenticSql

VITAL_INFO:
Create a disposable branch of your database if you want to try out this alpha of AgenticSql on your data. AI's have been know to completely destroy the projects they are working on. Sandbox's and backups are crucial.

**AgenticSql** is a natural language interface to a SQL agent. It’s an **agentic system** being designed for:  

- 💬 **Conversational database research** — query a branch of your database in natural language, with iteration for deep research.  
- 🏗️ **Autonomous schema design** — create complex schemas, generate test data, and test without manual SQL.  
- 📂 **Evolving document stores** — Advance knowledge autonomously.

AgenticSql combines **natural language processing** with **iterative agentic reasoning**, allowing it to plan, query, test, and refine knowledge autonomously.

---

Here's an article that explains what AgenticSql does in more detail: [Autonomous Knowledge Evolution](https://www.linkedin.com/pulse/autonomous-knowledge-evolution-transcendai-tech-f9wyc)

---

This is freshly published code, there will be bugs. I'm going to be focused on debugging AgenticSql over the next few days and weeks so expect rapid fixes. It's stable enough to function with simple natural language prompts, here's an example prompt that I've tested it with: [Ethical AI Prompt](Prompts/EthicalAI.txt)

---

AgenticSql requires SQL Express or SQL Server to be installed. It is being distributed as source only at this point so you'll need to compile it. I'm compiling with Visual Studio but Visual Studio Code should compile it without too much trouble. Windows only.

---

[AgenticSql Help](AgenticSqlHelp.md) How to compile and use the app(WPF GUI).

Here are guides to the interesting parts of the code.</br>
[AgenticSql Code Guide](AgenticSqlCodeGuide.md) A guide to the core AgenticSql C# library.</br>
[SqlContain Code Guide](SqlContainCodeGuide.md) A guide to a C# library that contains generated sql code to the database for secure AI development.

---

Here is info on using the provided database example, Meta7-stable1.bak. This file is a backup of a self-improving learning system that has begun to work on the Millennium Prize Problems. The Meta7-stable1.bak will have to be loaded into SQL Express/Server and you will need to set the 'DB Name' in AgenticSql to match. The Improve.txt prompt in Prompts matches this database.
[Database Help](DatabaseHelp.md)

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

