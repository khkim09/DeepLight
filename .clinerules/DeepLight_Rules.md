# DeepLight Project Architecture & Coding Rules
You are an expert Unity C# developer. You must strictly adhere to the following project-specific rules when writing or modifying code:

1. Architecture & Design Pattern (MVP):
- Strictly follow the MVP (Model-View-Presenter) pattern.
- Keep business logic & data (Model), Unity-dependent UI/Input handling (View), and the bridging logic (Presenter) completely separated.

2. Project Structure & asmdef (Refer to GDD):
- Strictly adhere to the project directory structure (e.g., Core, Data, Gameplay, Managers, UI).
- ALWAYS check existing `asmdef` (Assembly Definition) files before creating or modifying scripts to absolutely prevent circular references.

3. Coding Conventions & Syntax:
- NO COROUTINES. Exclusively use `UniTask` for all asynchronous operations.
- XML Summaries: Add a concise, one-line XML `<summary>` above every class and method to clearly define its purpose.
- Inline Comments: Liberally add inline comments inside methods to explain the 'why' and 'what' of the logic so it's readable at a glance.
- Avoid forced automations (e.g., auto-placement, forced state cancels). Prefer explicit user control and overlays.
- Use Object Pooling for frequently instantiated/destroyed objects.

4. Unity Editor Integration:
- Whenever you write or modify scripts, you MUST provide detailed, step-by-step instructions for Unity Editor setup in the chat (e.g., "Attach this component to X prefab", "Assign Y to the inspector").

5. Safety & Context Checking:
- Always read and analyze the local workspace files first to understand the current architecture.
- If the local structure seems out of sync, deprecated, or you suspect missing code, DO NOT guess. Stop and explicitly ask the user for clarification or the updated code.

6. Execution & Reporting (CRITICAL):
- After completing a task, DO NOT just stop. 
- First, explicitly explain HOW you executed the task and the logic behind it.
- Second, provide a clear, bulleted list of all the specific files modified and the exact changes made within them.