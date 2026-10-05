# Kratix Agent

Ask the ERP business questions in plain English, Urdu or Roman Urdu and get answers from live data.
Runs fully locally on Ollama; no cloud API.

## How it works

```
React app  --question + user's ERP token-->  Agent (FastAPI, :5080)
                                                |  0. Verifies the token itself (same key as the ERP)
                                                |  1. Qwen picks a tool and its arguments
                                                |  2. Tool calls the ERP API with the user's own token (GET only)
                                                |  3. Qwen writes the answer, streamed word by word
                                                v
                                        Kratix ERP API (:5069)
```

Chat history, the question log (timings and tool calls per question) and later reports live in a SQLite file,
`data/agent.db` (git-ignored). `Kratix_ERP` is never written to.

- **Read-only by design.** The ERP client only has `get()`; the agent cannot create, change or delete anything.
- **Same permissions as the user.** The ERP checks the token on every call, so Sales cannot see stock or production,
  Worker cannot see money, and so on. A blocked tool comes back as "your role cannot see that", which the model relays.
- **No SQL from the model.** Tools wrap existing ERP endpoints and trim the results to what the model needs.

## Tools

| Tool | ERP endpoint(s) | Roles |
|---|---|---|
| `business_overview` | `dashboard/metrics` | Owner, Admin |
| `receivables` | `reports/receivables` | Owner, Admin, Sales |
| `sales_report` | `reports/sales` | Owner, Admin, Sales |
| `current_stock` | `inventory`, `products` | Owner, Admin, Operations, Worker |
| `stock_movement_report` | `reports/stock` | Owner, Admin, Operations, Worker |
| `production_report` | `reports/production` | Owner, Admin, Operations, Worker |
| `supplier_quality` | `purchaseorders`, `inboundreceipts` | Owner, Admin, Operations, Worker |
| `purchase_orders` | `purchaseorders` | Owner, Admin, Operations, Worker |
| `sales_orders` | `salesorders` | Owner, Admin, Sales |
| `customer_details` | `customers` | Owner, Admin, Sales |
| `create_report` | as the report type (below) | Same as the report's data |

To add one: write an async function in `app/tools.py`, register it in `TOOLS`, add a test in `tests/test_tools.py`.

## Setup

```bash
ollama pull qwen2.5:7b            # about 4.7 GB; resumes if interrupted
cd kratix-agent
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements.txt
```

Copy `.env.example` to `.env` and set `AGENT_JWT_KEY` to the ERP's `Jwt:Key` (from the ERP's
`appsettings.Development.json`, or its `Jwt__Key` on a server). The agent refuses to start without it. `.env` is
git-ignored; never commit the key. Other settings have working defaults in `app/config.py`
(the model stays `qwen2.5:7b`).

## Run

The ERP backend must be running on 5069 and Ollama on 11434.

```bash
.venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 5080
curl http://127.0.0.1:5080/health     # agent, ollama, modelLoaded, erp, db
```

The model is loaded at startup (`/health` shows `modelLoaded: true` after a few seconds) and kept loaded for
`AGENT_KEEP_ALIVE` (30 minutes) after each question.

All routes except `/health` need `Authorization: Bearer <ERP token>`:

| Route | |
|---|---|
| `POST /chat/sessions` | Start a conversation: `{id, title, createdAt}` |
| `GET /chat/sessions` | My conversations, newest first |
| `GET /chat/sessions/{id}/messages` | Messages of one of my conversations |
| `DELETE /chat/sessions/{id}` | Delete one of my conversations |
| `POST /chat/sessions/{id}/ask` | Body `{"question": "..."}`. Answer as server-sent events, below |

Events of an answer, in order:

| Event | Data | Use in the app |
|---|---|---|
| `tool` | `{name, label}` | Show "Checking sales..." while the ERP is read |
| `data` | `{tool, title, count, rows}` | Draw the full table under the answer (the model only reads the first 10 rows) |
| `token` | `{text}` | Append to the answer |
| `done` | `{messageId, toolCalls, firstTokenMs, totalMs}` | Answer finished and saved |
| `error` | `{message}` | Show the message; nothing was saved |

When the model makes a report, a `report` event `{id, type, format, fileName, sizeBytes, createdAt, expiresAt,
downloadUrl}` comes before `done`, and `done.report` repeats it.

Refusals before the answer starts are plain HTTP: 401 sign in again, 404 not your conversation, 429 still answering
your last question or more than 20 questions in a minute, 503 the assistant is busy (10 questions queued).

## Reports

Ask in chat ("this month's sales in Excel", "slides on receivables", "stock report for last week as a sheet") or call
the routes directly (for a button in the app):

| Route | |
|---|---|
| `POST /reports` | Body `{"type": "sales", "format": "xlsx", "period": "this_month"}` or with `"from"`/`"to"` (YYYY-MM-DD) |
| `GET /reports` | My reports that have not expired, newest first |
| `GET /reports/{id}/download` | The file. Own reports; Owner and Admin can download anyone's |

| Type | Data | Excel sheets | PowerPoint slides |
|---|---|---|---|
| `sales` | `reports/sales` | Summary, By customer, By product, Invoices | Title, key figures, sales by customer and by product charts, tables |
| `receivables` | `reports/receivables` (as of today) | Summary, By customer (aged), Open invoices | Title, key figures, unpaid-by-age chart, tables |
| `stock` | `reports/stock`, `inventory` | Summary, Movement summary, Stock by warehouse and lot | Title, key figures, closing stock charts (bags and kg apart), tables |
| `production` | `reports/production` | Summary, Daily, Batches, Output by product | Title, key figures, daily yield and output charts, tables |

Reports are fixed templates filled from ERP data; the model only picks type, format and dates. Charts are native
PowerPoint charts (editable). Tables run 15 rows per slide with continuation slides. Roles follow the ERP: a Worker
asking for a sales report is told their role cannot see it. Limit 10 reports per user per hour. Files are kept 7 days
in `data/reports/<user id>/`, then removed.

## In the ERP app

The React app has an **Assistant** page (menu: Insights & Admin > Assistant, every role) that uses the routes above:
conversation list, streamed answers, the full table under each answer, report download cards, thumbs up/down with an
optional comment, a "Make a report" button, and for Owner/Admin a "Usage" view of `/admin/stats`. Code:
`kratix-ui/src/api/agent.ts` and `kratix-ui/src/pages/Assistant.tsx`. The app finds the agent at `VITE_AGENT_URL`
(default `http://127.0.0.1:5080`); the agent's `AGENT_CORS_ORIGINS` must include the app's address.

## Checks in code around the model

qwen2.5:7b makes mistakes that prompt wording alone did not fix reliably, so the agent checks its tool calls:

- A first reply with figures but no tool call is never shown; a business question answered without a tool is told
  once to use one. Off-topic questions ("write me a python code") are not pushed to use a tool and are sent without
  the earlier messages, so the model declines instead of repeating its last answer.
- A question with two or three parts ("expected deliveries today and kis customer ne paise dene hain") is split and
  each part answered in turn in one streamed answer; parts must be business questions of four words or more.
- `purchase_orders` has `pending` for expected deliveries (not yet fully received), ignored when a PO number is given.
- Customer, supplier and warehouse filters the user never typed are dropped (it invented "Khwab Foods" and a "main"
  warehouse, then reported nothing found). Unknown order statuses ("any") are ignored. A list of products
  ("wheat, flour, bran") matches any of them.
- A data tool called with a file format becomes `create_report`.
- Temperature 0, so the same question gets the same answer and a fix can be checked.

## Feedback, stats and housekeeping

| Route | |
|---|---|
| `POST /messages/{id}/feedback` | Body `{"rating": 1 or -1, "comment": "..."}` on one of my answers (`done.messageId`); posting again replaces it |
| `GET /admin/stats?days=7` | Owner and Admin only (1-180 days): questions per day and user, failed %, time to first words and to complete (p50/p95), tool calls and errors, thumbs up/down with the thumbs-down questions and answers, reports made |

At startup and every 24 hours the agent removes expired report files, question log rows older than 180 days and
conversations deleted more than 30 days ago (their log rows stay, unlinked), then writes a backup of `agent.db` to
`data/backups/` and keeps the newest 14. All periods are settings (`AGENT_LOG_RETENTION_DAYS` and so on).

Back up by hand, safe while the agent runs:

```bash
.venv/Scripts/python scripts/backup.py              # into data/backups/
.venv/Scripts/python scripts/backup.py D:/Backups   # into another folder, e.g. next to the ERP backups
```

## Test

```bash
.venv/Scripts/python -m pytest -q                         # unit tests, no network or model needed
.venv/Scripts/python ask_live.py                          # MVP questions as Owner, one conversation, with timings
.venv/Scripts/python ask_live.py --role Sales "Who owes us money?"
.venv/Scripts/python ask_live.py --new-session-each "..."  # each question in a fresh conversation
```

`ask_live.py` signs in with the demo accounts from the git-ignored `appsettings.Development.json`, streams each
answer and prints the time to first words against the 500 ms target. It only reads.

## Not built yet

- Golden question set and role check script (B4)
- Chat page in the React app
- WhatsApp and voice input
