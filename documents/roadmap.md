---
name: Testurio — Roadmap
version: 1.0.0
status: draft
updated: 2026-04-27
tags: [product, business, roadmap]
---

# Testurio — Roadmap

---

## Stage 1 — POC

**Goal:** Prove that the AI testing loop works end-to-end — a real Jira story moves to "In Testing" and a real test report lands back on that story automatically.

**Key question being answered:** Can the AI agent read a story, generate meaningful test scenarios, execute them against a live web app, and produce a useful report — with no human intervention?

### In Scope

| Area | Detail |
| --- | --- |
| PM tool | Jira only (webhook receiver + report writer) |
| LLM | Commercial API via OpenAI-compatible endpoint (e.g. GPT-4o) |
| Pipeline | Webhook → Story Parser → Test Generator → Test Executor (HTTP client, API only) → Report Writer |
| Configuration | Single hardcoded project config: product URL, Jira token, testing strategy |
| Environment access | URL-based only; auth credentials hardcoded in config |

### Out of Scope

- Public website
- User portal and dashboard
- Authentication and multi-tenancy
- Billing and subscriptions
- UI E2E / browser automation (Playwright)
- Screenshots (UI E2E only — not applicable to API testing)
- Azure DevOps integration
- Statistics and test history
- Self-hosted LLM (vLLM)
- Per-project settings UI

### Deliverable

Moving a Jira story to "In Testing" triggers a full automated test run and attaches a report to that story. Demonstrated against a real (or staging) web application.

### LLM Switch Note

The commercial API and the final self-hosted model (vLLM) both expose an OpenAI-compatible REST interface. Semantic Kernel abstracts the provider — switching from POC to MVP is a single config change (endpoint URL + model ID). No code changes required in the test generator.

---

## Stage 2 — MVP

**Goal:** First shippable product that real paying customers can sign up for, configure, and use independently.

### In Scope

All 17 features from `documents/features.md`:

| Area | Features |
| --- | --- |
| AI Testing Pipeline | 0001 Automatic trigger, 0002 Test scenario generation, 0003 Automated API execution, 0004 Report delivery, 0005 Execution log capture, 0018 Automated UI E2E execution |
| Project Management | 0006 Project creation & config, 0007 PM tool integration (ADO + Jira), 0008 Custom prompt, 0009 Report format settings, 0017 Environment access config |
| Statistics & Dashboard | 0010 Project dashboard, 0011 Test history & trends |
| Public Website | 0012 Marketing & pricing, 0013 Registration & sign-in |
| Account & Billing | 0014 Account settings, 0015 Plan purchase, 0016 Subscription management |

### Key Upgrades from POC

| Concern | POC | MVP |
| --- | --- | --- |
| LLM | Commercial API | Self-hosted vLLM (LoRA fine-tuned) |
| Test execution | API only (HTTP client) | API + UI E2E (Playwright browser automation) |
| Screenshots | Not applicable | Captured during UI E2E runs |
| PM tools | Jira only | Jira + Azure DevOps |
| Configuration | Hardcoded | Full per-project UI in user portal |
| Auth | None | Azure AD B2C (email, social login) |
| Multi-tenancy | None | Logical isolation by `userId` (Cosmos DB partition key) |
| Environment access | Hardcoded URL | IP allowlist + Basic Auth + header token (per project) |
| Billing | None | Stripe — plan purchase + subscription management |
| Observability | None | Application Insights — logs, traces, alerting |

### Deliverable

A publicly accessible SaaS product. A QA lead can sign up, purchase a plan, create a project, connect Jira or Azure DevOps, and have automated test reports running within the same session — with no assistance from the Testurio team.
