---
name: Testurio
version: 0.2.0
status: concept
author: vyacheslav.kozyrev@gmail.com
created: 2026-04-25
updated: 2026-04-25
tags: [ai-agent, testing, automation, devops, saas, business]
---

# Testurio

## Overview

Testurio is a SaaS platform that automates software testing for product teams. It provides a public website for onboarding and plan management, a personal workspace for managing projects and settings, and an AI agent that autonomously generates and executes test scenarios when a work item moves to the testing stage. API testing ships first; UI end-to-end testing via browser automation is added in the MVP.

## Product Areas

### 1. Public Website

- Presents the Testurio service and value proposition
- Describes available subscription plans and pricing
- Entry point for sign-up and plan purchase

### 2. User Account & Personal Area

Created after purchase or registration. Users can manage:

- **Settings**: appearance, language, payment method, personal information
- **Projects**: create and configure multiple testing projects
- **Statistics**: view testing history and results per project

### 3. AI Testing Agent

Triggered automatically via webhooks from connected PM tools. Runs tests and reports results back — no manual intervention required.

## Core Workflow

1. A work item or user story status changes to **"In Testing"** in a connected PM tool.
2. The webhook triggers the Testurio agent.
3. The agent reads the story description and acceptance criteria.
4. The agent generates test scenarios based on the project's testing strategy and custom prompts.
5. The agent executes the tests against the configured URL — API calls (HTTP requests/responses) in the first stage; browser-driven UI flows in the MVP stage.
6. A test report is attached back to the original work item and recorded in the project statistics.

## Integrations

- **Project Management**: Azure DevOps, Jira (webhook receivers)
- **Testing Target**: API testing against any HTTP/HTTPS endpoint (POC); UI E2E browser testing against any web product URL (MVP)
- **Report Delivery**: Comment or attachment on the originating work item
- **Payments**: subscription plan purchase (payment provider TBD)

## Project Configuration

Each project has its own isolated settings:

| Property           | Description                                                               |
| ------------------ | ------------------------------------------------------------------------- |
| `name`             | Project display name                                                      |
| `description`      | What the project tests                                                    |
| `product_url`      | Base URL of the product under test                                        |
| `test_type`        | Type of testing to perform: `api` (POC) \| `ui_e2e` \| `both` (MVP)     |
| `testing_strategy` | High-level strategy guiding test generation (e.g. smoke, regression, BDD) |
| `access_token`     | Token used by the PM tool webhook to authenticate with Testurio        |
| `model_settings`   | LLM model version and inference parameters                                |
| `custom_prompt`    | Optional prompt to guide or constrain test generation                     |
| `pm_tool`          | Connected PM tool (`azuredevops` \| `jira`)                               |
| `pm_token`         | API token for writing reports back to the PM tool                         |

## Key Components

- **Public Website** — marketing, pricing, sign-up, plan purchase
- **User Portal** — personal area: account settings, project management, statistics dashboard
- **Webhook Receiver** — HTTP endpoints that accept status-change events from Azure DevOps / Jira
- **Story Parser** — extracts description and acceptance criteria from the incoming payload
- **Test Generator** — uses a fine-tuned LLM to produce structured test scenarios
- **Test Executor** — runs generated scenarios via HTTP client (API testing, POC) or browser automation (UI E2E, MVP)
- **Report Writer** — formats results and posts them back to the work item via the PM tool API

## Non-Goals (v1)

- Mobile app testing
- Load / performance testing
- Test case version history
- Team / multi-user accounts (single user per account in v1)
