# Testurio — Claude Code

SaaS platform for automated software testing. See `documents/architecture.md` for full architecture detail.

## Source Layout

```
source/
├── Testurio.Web/            # Next.js — public site + user portal
├── Testurio.Api/            # ASP.NET Core — REST API + webhooks
├── Testurio.Worker/         # .NET Worker Service — test pipeline
├── Testurio.Core/           # Domain models, interfaces, value objects
├── Testurio.Plugins/        # Semantic Kernel plugins (StoryParser, TestGenerator, TestExecutor, ReportWriter)
└── Testurio.Infrastructure/ # Cosmos, Blob, Service Bus, Stripe, Key Vault clients
tests/
├── Testurio.UnitTests/
└── Testurio.IntegrationTests/
infra/
├── modules/                 # Bicep — one file per Azure service
└── k8s/                     # vLLM deployment + service manifests
```

## Feature Specifications

Specs live in `specifications/<####>-<name>/` — always read before implementing:
- `stories.md` — user stories and acceptance criteria
- `plan.md` — layered task list with file paths
- `progress.md` — status tracking

## Rules

Load per task:
- Frontend work: `@.claude/rules/ui.md`
- Backend work: `@.claude/rules/be.md`
- Writing tests: `@.claude/rules/qa.md`
