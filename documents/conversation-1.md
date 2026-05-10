# AI Strategy Conversation — 2026-05-09

---

**User:** ok, would would you recoomend for our project, what approach? 1) have external AI API like Claude API or OpenAI API, have fine-tuned or embeded or distilled model in AzureAI, have a RAG layer plus API calls, have a local trained model?

**Claude:** For Testurio's use case — generating test scenarios from user stories — **external Claude API is the right call**, and your architecture already reflects this correctly.

Here's why the alternatives don't fit:

| Option | Why it doesn't fit Testurio |
|---|---|
| **Fine-tuned model (Azure AI)** | Fine-tuning improves style/format, not reasoning. Test generation requires strong code reasoning — you'd degrade quality and pay for training + hosting |
| **RAG + API** | RAG helps when you have a large private knowledge base to query. Your input is already self-contained (a single user story + acceptance criteria) |
| **Local trained model** | GPU infra cost, ops burden, and model quality all lose to frontier API — only justified for air-gapped/compliance scenarios |

**Stick with Claude API because:**
- Test generation requires multi-step reasoning over arbitrary user stories — frontier models with adaptive thinking handle novel inputs far better than fine-tuned/distilled models
- You pay per job, not 24/7 for idle GPU capacity
- `claude-opus-4-7` with adaptive thinking is the highest-quality option for reasoning-heavy tasks
- Zero infra to operate or scale

**The one thing worth adding later**: a lightweight RAG layer over *your own past test results* — so the generator can learn which test patterns worked well for similar stories in a given project. But that's a v2 optimization, not a v1 requirement.

---

**User:** ok, I think this approach works well for smal size project, but what if we scale? and will need to generate a lots of scenarios and the run a lots of tests? 1st problem is COST: what will be more expensive to call external model not trained for test generation like OpenAI or or smaller but a lot more specific? We can collect all information about generated test scenarios for different stories and use this data to train our model.

**Claude:** Great question — this is exactly the right time to think about it, before you're locked in.

## The Cost Reality at Scale

At low volume, API cost is negligible. The break-even math shifts as you grow:

| Approach | Cost model | Break-even signal |
|---|---|---|
| Claude API (`opus-4-7`) | ~$15/M input tokens, linear | Always pays per call |
| Smaller fine-tuned model (Azure AI) | Fixed hosting + cheap inference | ~50-100k test generations/month |
| Self-hosted open model (vLLM) | GPU cost, fixed | High, predictable, massive volume |

## The Right Scaling Strategy for Testurio

**Phase 1 (now):** Claude API — correct, no change needed.

**Phase 2 (data flywheel):** Start collecting now, cost nothing extra:
- Store every `(user_story → generated_tests)` pair in Cosmos
- Tag outcomes: did the tests pass? did they catch real bugs? did the client accept them?
- This labeled dataset is your future training corpus

**Phase 3 (scale crossover):** Distillation — not fine-tuning from scratch:
- Use Claude `opus-4-7` as teacher, distill into a smaller model (e.g., `Mistral-7B` or `Phi-4`)
- Fine-tuning on *your domain* (software testing, Playwright, API assertions) is where smaller models actually compete with frontier models — the task is structured and learnable
- Host on Azure AI or your existing vLLM infra (you already have `infra/k8s/` for vLLM)

**Phase 4 (hybrid routing):** Keep both models, route by complexity:
```
simple story + known pattern  →  specialized small model (cheap, fast)
complex story + edge case     →  Claude API (expensive, high quality)
```

## The Key Insight

Test generation is one of the *best* candidates for distillation because:
- Output format is highly structured (Playwright code, assertions)
- Domain vocabulary is finite (HTTP, selectors, auth patterns)
- Quality is objectively measurable (do tests pass? do they catch bugs?)

Unlike open-ended reasoning, you can *automatically evaluate* model quality — which makes the training loop tight.

**Start collecting training data now.** The model swap is a later decision; the data is the hard part.

---

**User:** please save this conversation into phases.md file

*(Saved to `documents/phases.md`)*

---

**User:** ok, let's continue our discusstion. If I got with Azure AI, I can use claude oupus 4-7 at the beginning and the destill collected data into smaller one and host it there, correct? please anser yes/no

**Claude:** Yes.

---

**User:** ok, let's try to esimate costs. Plese generate matrix of costs based on workload, start from 1000 generated scenarios per month

**Claude:** Here's the matrix with assumptions stated upfront — pricing is approximate and should be verified before committing.

## Assumptions

| Parameter | Value |
|---|---|
| Tokens per scenario (input) | ~600 (story + criteria + system prompt) |
| Tokens per scenario (output + thinking) | ~3,500 (Playwright code + adaptive thinking) |
| Claude `opus-4-7` input price | ~$15 / 1M tokens |
| Claude `opus-4-7` output price | ~$75 / 1M tokens |
| **Cost per scenario (Claude API)** | **~$0.27** |
| Distilled 7B model on Azure AI (1× T4 GPU) | ~$380–500 / month fixed |
| Distilled model inference cost | ~$0 variable (fixed hosting) |
| Hybrid split | 80% small model / 20% Claude API |

## Cost Matrix (USD / month)

| Scenarios/month | Claude API only | Distilled model (Azure AI) | Hybrid 80/20 |
|---|---|---|---|
| 1,000 | $270 | $420 | $270 |
| 5,000 | $1,350 | $420 | $570 |
| 10,000 | $2,700 | $430 | $960 |
| 25,000 | $6,750 | $450 | $1,800 |
| 50,000 | $13,500 | $500 | $3,170 |
| 100,000 | $27,000 | $650 | $5,840 |
| 500,000 | $135,000 | $2,000* | $28,100 |

*500K/month needs ~3–4 GPU instances running in parallel.

## Break-even

- **Claude API vs. distilled model:** ~8,000–10,000 scenarios/month
- **Claude API vs. hybrid:** ~3,000–4,000 scenarios/month (hybrid wins earlier because you still cut 80% of API calls)

**Recommendation:** Start with pure Claude API, switch to hybrid at ~5K/month, move distilled model to primary at ~25K+/month.
