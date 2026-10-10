# AGENTS.md — Comink

Rules for AI coding agents working in this repository. Read this file fully before doing any work. These rules apply to every task unless the user explicitly overrides them in the current conversation.

---

## 1. Project at a glance

**Comink** is a small social network for students and small communities, with **real-time messaging and voice/video calls as the core experience**. It is a university specialization project (đồ án chuyên ngành phần mềm).

- Team: 2 full-stack developers, 3 months.
- Platforms: Web (React) first, then a mobile app (Flutter). Both use one shared backend.
- Development rule: **basic level (CB) of a feature group first, advanced level (NC) later.** Web before mobile. AI features last.

The repository is currently **empty of code** (only `docs/` exists). Scaffolding will be created step by step following `docs/04_roadmap.md`.

---

## 2. Source of truth (read before working)

All project documents are in `docs/` and are written in Vietnamese.

| File | Purpose | Read when |
|---|---|---|
| `docs/COMINK_SPEC.md` | Product scope and features (CB / NC) | Always, at the start of a task |
| `docs/02_tech_stack_decisions.md` | Architecture, tech stack, development principles | Before writing any code |
| `docs/03_database_design.md` | PostgreSQL schema, enums, indexes, migrations | Before touching data, entities, or migrations |
| `docs/04_roadmap.md` | Phases, checklists, current priorities | Before starting a feature, to check phase order |

Rules:
- Follow the documents. **If code and documents disagree, or the documents are unclear or incomplete, stop and ask the user.** Do not silently pick one.
- Decisions marked ❓ in `02_tech_stack_decisions.md` are **not decided yet**. Do not choose for the user; ask (see section 13).
- Decisions marked 💡 are proposed defaults and may be used unless the user says otherwise.

---

## 3. Language rules

| Item | Language |
|---|---|
| This file and other agent instructions | English |
| Conversation with the user (explanations, questions, summaries) | **Vietnamese** |
| Identifiers (class, method, variable, file, table, column names) | English |
| Commit messages | English (Conventional Commits) |
| Code comments | **Vietnamese** |
| API error codes and SignalR event names | English constants (e.g. `AUTH_INVALID_CREDENTIALS`); the clients translate them for display |
| Project documents in `docs/` | Vietnamese |
| User-facing UI text | Never hard-coded; always through i18n (`vi` and `en`) |

---

## 4. Repository layout (planned)

```
comink/
├── AGENTS.md
├── docs/                  # project documents (source of truth)
├── backend/               # ASP.NET Core solution (Clean Architecture)
├── web/                   # React + TypeScript + Vite
├── mobile/                # Flutter
├── infra/                 # docker-compose, LiveKit config, proxy
├── .github/workflows/     # CI/CD
├── .env.example
└── README.md
```

Do not create directories outside this layout without asking.

---

## 5. Tech stack summary

Details and rationale are in `docs/02_tech_stack_decisions.md`. Do not replace any decided technology.

- **Backend:** ASP.NET Core (latest LTS), modular monolith with Clean Architecture, SignalR, EF Core + Npgsql, PostgreSQL, Redis, ASP.NET Core Identity + JWT with rotating refresh tokens, FluentValidation, Serilog.
- **Web:** React + TypeScript (strict) + Vite, TanStack Query for server state, Zustand for UI/session state only, i18next.
- **Mobile:** Flutter, Riverpod, dio, go_router, secure storage for tokens.
- **Third-party services:** a managed call provider (LiveKit proposed) with TURN, Cloudinary/Firebase for media, Firebase Cloud Messaging for push, an external LLM API for AI features.
- **Infra:** Docker / Docker Compose, CI/CD.

Pin dependency versions. Check the license of any new library before adding it; do **not** add commercial or license-restricted libraries without approval.

---

## 6. Autonomy: what you may and may not do

### 6.1. You may do without asking
- Read, create, edit and delete source files inside the repository, within the scope of the task.
- Run build, test, lint and format commands.
- Install packages (after checking the license and the stack rules above).
- Create EF Core migrations and run them against the **local development** database.
- Run local Docker Compose services.
- `git commit` on the current branch (see section 11).

### 6.2. You must NOT do
- `git push`, create branches, or open pull requests. The user does these.
- Edit, rename or delete a migration that is already merged into `main`. Create a new migration instead.
- Read, print, edit or commit `.env` files or any secret (API keys, JWT secrets, connection strings, tokens). Only `.env.example` with placeholder values may be edited.
- Delete data or volumes (database, Docker volumes, uploaded media) or run destructive commands (`DROP`, `docker compose down -v`, `git reset --hard`, `git clean -fd`, force operations).
- Change an already-decided technology or architecture choice.
- Add commercial or license-restricted libraries.

If a task seems to require any of the above, **stop and ask the user first**. Approval must come from the user in the current conversation.

### 6.3. Ask-first situations
- The request is ambiguous or can be read in more than one way. **Ask; do not guess and do not silently assume.**
- A document is missing, outdated or contradicts another.
- A change would affect the other developer's area (different feature group) or the shared API contract.
- An open decision (section 13) is needed to continue.

---

## 7. Working scope and process

1. **Do exactly the task you were given.** Do not refactor, rename, reformat or "improve" code outside the requested scope. If you notice other problems, **report them** instead of fixing them.
2. **Respect the roadmap order** (`docs/04_roadmap.md`):
   - Finish the basic level (CB) of the current feature group before starting the next group's CB.
   - Advanced (NC) work is allowed at any time, but only after the CB of **that same group** is done.
   - Web before mobile. AI features only after the main features are stable.
   - Login and authorization come before everything else (documented exception).
3. Work in small, reviewable steps. Prefer several small changes over one large one.
4. Before large changes, briefly state your plan and the files you will touch, then proceed.
5. Do not invent endpoints, tables, enums, or events that are not in the documents. If something is needed, propose it, then ask.

---

## 8. Engineering rules

### 8.1. Backend (ASP.NET Core)
- **Dependency direction:** `Api → Application → Domain` and `Infrastructure → Application → Domain`. Domain and Application never reference Infrastructure.
- Organize code **by feature inside each layer** (Identity, Social, Messaging, Calls, Media, Admin, Notifications, Ai), as described in `02_tech_stack_decisions.md`.
- Business logic lives in the Application layer, never in controllers or SignalR hubs.
- Never return EF entities from the API; use DTOs.
- All third-party services go through interfaces defined in Application (`IMediaStorage`, `ICallProvider`, `IPushSender`, `IEmailSender`, `IAiProvider`).
- Use `async/await` end to end, pass `CancellationToken`, never block with `.Result` or `.Wait()`.
- Validate every input on the server with FluentValidation. Return consistent, machine-readable error codes.
- **Communication model:** writes go through REST; SignalR is used to push events to clients. Only lightweight transient signals (typing, presence ping) go directly through hubs.
- Lists must use **cursor pagination** `(created_at, id)`, never `OFFSET`.
- Operations that can be repeated (send message, send friend request) must be idempotent or protected against duplicates (e.g. `client_message_id`).
- Avoid N+1 queries; use projection or `Include` deliberately.
- Nullable reference types enabled; no warnings left behind.

### 8.2. Database and migrations
Follow `docs/03_database_design.md`. Key points:
- `snake_case` tables and columns; primary keys are `uuid` generated as **UUIDv7** in the backend; times are `timestamptz` in UTC.
- Enums are stored as `varchar` with a `CHECK` constraint (not native PostgreSQL enums).
- Soft delete (`deleted_at`) for posts, comments, messages; hard delete for join tables.
- Create tables **only when the milestone needs them** (see section 6.3 of the database document); do not add advanced-level tables early.
- Migration names: `YYYYMMDD_<Module>_<Description>`. One purpose and one module per migration. Review the generated SQL before finishing.
- Only one migration may be in progress at a time. Before creating one, make sure the latest `main` migrations are applied locally.
- Do not run migrations automatically on startup in non-development environments.
- **If you change schema, enums, or indexes, update `docs/03_database_design.md` in the same change.**

### 8.3. Web (React + TypeScript + Vite)
- TypeScript `strict`; no `any` unless justified in a comment.
- Server data through TanStack Query; Zustand only for UI/session state.
- Prefer an API client generated from the OpenAPI contract over hand-written fetch calls.
- Every screen that loads data handles **loading, empty and error** states.
- All displayed strings go through i18next (`vi` and `en`); no hard-coded UI text.
- Responsive layout; theme-aware colors.
- Admin area is part of the web app under `/admin`, protected by role.

### 8.4. Mobile (Flutter)
- Riverpod for state, dio with an interceptor for token refresh, go_router for navigation.
- Tokens only in secure storage.
- Localization with `intl` (`vi` and `en`); no hard-coded UI text.
- Handle permission denial for microphone, camera and notifications gracefully.
- `flutter analyze` must pass.
- Start mobile work for a feature only after its web version is stable.

### 8.5. Calls, media and AI
- Calls: the backend only issues short-lived provider tokens and tracks call state; media goes directly between clients and the provider. Verify the user is a member of the conversation before issuing a token.
- Media: clients upload directly to the cloud provider with a signature issued by the backend. Validate type and size on **both** client and server.
- AI: API keys exist **only on the backend**. Apply per-user rate limits. Only send the model data that the requesting user is allowed to access.

---

## 9. Security rules (mandatory for every feature)

1. Never commit secrets. Use `.env` (git-ignored), `dotnet user-secrets` for local work, and keep `.env.example` with placeholders only.
2. **Check authorization on the backend for every resource access.** Never trust IDs sent by the client; verify the user is a conversation member, post owner, or otherwise permitted.
3. Check authorization when a client joins a SignalR group, not only on REST endpoints.
4. Validate and limit all input (length, size, format). Render user content as plain text or sanitize it to prevent XSS.
5. Access tokens are short-lived; refresh tokens rotate and can be revoked; mobile stores tokens in secure storage.
6. Do not log passwords, tokens, or full message content.
7. Apply rate limiting to sensitive endpoints (login, sending messages, friend requests, reports) once the security advanced level is implemented; design code so it can be added without rewrites.

---

## 10. Testing rules

- **Every change to business logic must include tests.** Prioritize: conversation access rules, friendship/block states, call status transitions, authentication and authorization.
- Backend: xUnit; integration tests use real PostgreSQL and Redis (Testcontainers).
- Do not weaken or delete existing tests to make a change pass. If a test is wrong, explain why and ask.
- Run the relevant tests and lint before reporting a task as done, and state what you ran and the result.
- Never claim something works if you did not run it. If you could not run it, say so.

---

## 11. Git rules

- Commit messages in English, **Conventional Commits**: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`, with an optional scope, e.g. `feat(messaging): add cursor pagination for message history`.
- Branch naming convention (created by the user, not by you): `feature/<name>`.
- You may commit on the current branch. **If the current branch is `main`, do not commit; stop and tell the user.**
- Small commits, one purpose each. Never mix unrelated changes in one commit.
- Never `git push`, create branches, open PRs, rebase, or rewrite history.
- Never commit generated build output, `.env`, or secrets.

---

## 12. Documentation rules

- When you change the database schema, the API contract (endpoints, DTOs, SignalR events, error codes), or a technology decision, **update the matching document in `docs/` in the same change** and mention it in your summary.
- Keep `docs/` in Vietnamese; keep the existing structure and numbering.
- When a feature's checklist items in `docs/04_roadmap.md` are satisfied, tell the user; do not tick the boxes unless asked.

---

## 13. Open decisions (ask before choosing)

These are not decided. If a task depends on one of them, ask the user and wait for the answer.

- API style: controllers or Minimal API.
- Error handling style: exceptions vs. a result type for expected failures.
- Web UI library (for example Tailwind with shadcn/ui, or another).
- Call provider (LiveKit proposed), media storage (Cloudinary proposed), default LLM and budget.
- Monorepo vs. multiple repositories (the layout in section 4 assumes a monorepo).
- Email provider for production.

Once the user decides, add the answer here as a one-line rule and remove it from this list.

---

## 14. Commands

Scaffolding does not exist yet, so the real commands are not defined. **Do not invent or assume commands.** When scaffolding is created, fill this table in. Expected commands, to be verified at that time:

| Task | Command (to be confirmed) |
|---|---|
| Start infra | `docker compose -f infra/docker-compose.yml up -d` |
| Build backend | `dotnet build` |
| Test backend | `dotnet test` |
| Format backend | `dotnet format` |
| Create migration | `dotnet ef migrations add <Name>` |
| Apply migrations (local) | `dotnet ef database update` |
| Web dev / build / lint / test | `npm run dev` / `npm run build` / `npm run lint` / `npm test` |
| Mobile analyze / test | `flutter analyze` / `flutter test` |

If a command listed above fails because it does not exist yet, tell the user instead of working around it.

---

## 15. How to respond to the user

- Respond in **Vietnamese**.
- Explain **in detail the reasons** behind your decisions: why this approach, what alternatives you considered, and what the trade-offs are.
- When you show code in the conversation, **show only the changed part** (with the file path and enough surrounding lines to locate it), not the whole file. Edit the files in the repository directly.
- At the end of a task, summarize: what changed, which files, which tests or commands you ran and their results, which docs you updated, and anything you noticed but did not change.
- When something is unclear, **ask a clear question** instead of guessing. Ask one focused question at a time when possible.
- Be honest about limits: if you are unsure, did not verify, or could not run something, say so.

---

## 16. Definition of Done

A task is done only when all of these hold:

- [ ] It works according to `docs/COMINK_SPEC.md` and was tried with at least two accounts where relevant.
- [ ] Input validation and authorization are enforced on the backend.
- [ ] Loading, empty and error states exist in the UI; all text goes through i18n.
- [ ] Tests for important business logic exist and pass; lint passes.
- [ ] No secrets, no unrelated changes, no out-of-scope refactors.
- [ ] Related documents in `docs/` are updated.
- [ ] A summary was given to the user (section 15).