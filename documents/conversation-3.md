# AI Cost Matrix Conversation — 2026-05-09

---

**User:** please read @documents/conversation-1.md and regenerate price matrix of using standard model (opus 4-7) on Azure AI for test scenario generation. Please start with 1000 scenarios per month. Next please generate table with columns Azure AI and opus 4-7, Claude API calls, Open AI calls, CPU with 16G RAM (local solution). Rows start with 1000 scenarios per month.

**Claude:** Here's the updated cost matrix. Using the same token assumptions from the original conversation (600 input + 3,500 output tokens per scenario).

## Pricing Assumptions

| Parameter | Value |
|---|---|
| Tokens per scenario (input) | ~600 |
| Tokens per scenario (output + thinking) | ~3,500 |
| **Claude Opus 4-7 — Azure AI / direct API input** | ~$15 / 1M tokens |
| **Claude Opus 4-7 — Azure AI / direct API output** | ~$75 / 1M tokens |
| **Cost per scenario (Claude Opus 4-7)** | **~$0.27** |
| **OpenAI GPT-4o input** | ~$2.50 / 1M tokens |
| **OpenAI GPT-4o output** | ~$10 / 1M tokens |
| **Cost per scenario (GPT-4o)** | **~$0.037** |
| **Local CPU 16GB RAM** (Azure Standard_D4s_v3, 4 vCPU) | ~$150 / month fixed |

> **Azure AI note:** Claude Opus 4-7 is available via Azure AI Foundry Marketplace as pay-per-token MaaS — pricing is virtually identical to direct Anthropic API. The marginal premium (~3–5%) is from Azure billing overhead; treated as equal here.

> **Local CPU note:** 16GB RAM without GPU limits you to quantized 7–8B models (e.g., Llama 3.1 8B Q4). Quality for structured code generation is significantly lower than frontier models — expect more failed/incomplete test scenarios.

## Cost Matrix (USD / month)

| Scenarios/month | Azure AI + Opus 4-7 | Claude API (direct) | OpenAI GPT-4o | Local CPU 16GB RAM |
|---|---|---|---|---|
| 1,000 | $270 | $270 | $37 | ~$150 fixed |
| 5,000 | $1,350 | $1,350 | $185 | ~$150 fixed |
| 10,000 | $2,700 | $2,700 | $370 | ~$150 fixed |
| 25,000 | $6,750 | $6,750 | $925 | ~$160 (near CPU limit) |
| 50,000 | $13,500 | $13,500 | $1,850 | ~$320 (2 instances) |
| 100,000 | $27,000 | $27,000 | $3,700 | ~$500 (3–4 instances) |
| 500,000 | $135,000 | $135,000 | $18,500 | ~$2,100 (14 instances) |

## Key Takeaways

| Decision | Threshold |
|---|---|
| **GPT-4o vs Opus 4-7:** | GPT-4o is 7× cheaper but quality gap for reasoning-heavy Playwright generation is significant — needs validation before committing |
| **Azure AI vs direct API:** | No cost difference; choose Azure AI for consolidated billing, data residency, and future distilled model hosting in one platform |
| **Local CPU break-even vs GPT-4o:** | ~4,000–5,000 scenarios/month |
| **Local CPU break-even vs Opus 4-7:** | ~600 scenarios/month — but quality drop makes this a false economy unless you're distilling |
| **Local CPU ceiling:** | Single 16GB instance maxes out around ~20K scenarios/month before latency degrades |

---

**User:** please include claude sonnet into the matrix

**Claude:** Claude Sonnet 4.6 pricing: ~$3/1M input, ~$15/1M output → **~$0.054/scenario** (600 input + 3,500 output tokens).

## Cost Matrix (USD / month)

| Scenarios/month | Azure AI + Opus 4-7 | Claude API Opus 4-7 | Claude API Sonnet 4.6 | OpenAI GPT-4o | Local CPU 16GB RAM |
|---|---|---|---|---|---|
| 1,000 | $270 | $270 | $54 | $37 | ~$150 fixed |
| 5,000 | $1,350 | $1,350 | $270 | $185 | ~$150 fixed |
| 10,000 | $2,700 | $2,700 | $540 | $370 | ~$150 fixed |
| 25,000 | $6,750 | $6,750 | $1,350 | $925 | ~$160 |
| 50,000 | $13,500 | $13,500 | $2,700 | $1,850 | ~$320 |
| 100,000 | $27,000 | $27,000 | $5,400 | $3,700 | ~$500 |
| 500,000 | $135,000 | $135,000 | $27,000 | $18,500 | ~$2,100 |

## Break-even Summary

| Comparison | Crossover |
|---|---|
| Sonnet vs GPT-4o | Sonnet is ~1.5× more expensive — GPT-4o wins on cost alone; quality tradeoff must be validated |
| Opus 4-7 vs Sonnet | Opus is 5× more expensive; Sonnet is the natural default until quality tests prove Opus is needed |
| Sonnet vs Local CPU | Local breaks even at ~3,000 scenarios/month, but with much lower model quality |

**Recommended starting point:** Claude Sonnet 4.6 via Claude API direct or Azure AI — best balance of quality and cost. Escalate to Opus 4-7 only for stories that fail Sonnet's output quality threshold.

---

**User:** and please include haiku into the matrix

**Claude:** Claude Haiku 4.5 pricing: ~$0.80/1M input, ~$4/1M output → **~$0.015/scenario**.

## Cost Matrix (USD / month)

| Scenarios/month | Azure AI + Opus 4-7 | Claude Opus 4-7 | Claude Sonnet 4.6 | Claude Haiku 4.5 | OpenAI GPT-4o | Local CPU 16GB |
|---|---|---|---|---|---|---|
| 1,000 | $270 | $270 | $54 | $15 | $37 | ~$150 fixed |
| 5,000 | $1,350 | $1,350 | $270 | $75 | $185 | ~$150 fixed |
| 10,000 | $2,700 | $2,700 | $540 | $150 | $370 | ~$150 fixed |
| 25,000 | $6,750 | $6,750 | $1,350 | $375 | $925 | ~$160 |
| 50,000 | $13,500 | $13,500 | $2,700 | $750 | $1,850 | ~$320 |
| 100,000 | $27,000 | $27,000 | $5,400 | $1,500 | $3,700 | ~$500 |
| 500,000 | $135,000 | $135,000 | $27,000 | $7,500 | $18,500 | ~$2,100 |

## Model Selection Guide

| Model | Use case fit for test generation |
|---|---|
| **Haiku 4.5** | Simple CRUD stories, boilerplate scenarios — validate quality before committing |
| **Sonnet 4.6** | Best default: strong reasoning, structured output, 18× cheaper than Opus |
| **Opus 4-7** | Complex stories, edge cases, novel patterns — route only when Sonnet fails |
| **GPT-4o** | Cheapest non-local option but requires separate quality validation for Playwright output |
| **Local CPU** | Only viable after distillation; raw 8B models on CPU will produce unreliable test code |

**Recommended architecture:** Haiku for simple stories → Sonnet as default → Opus for escalations. Implement quality scoring on output to route automatically.
