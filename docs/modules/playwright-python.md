# Python Playwright Automation (Web + Electron)

Cascade supports two automation backends:

- **Windows desktop apps**: C# Body + UIA3 over gRPC
- **Websites / Electron apps**: **Python Playwright** (Brain-side)

## Auto-detection rules

- If `start_app(app_name)` receives a **URL** (`http://` / `https://`), Cascade starts a Playwright browser session and navigates to it.
- If `start_app(app_name)` receives an **Electron `.exe` path** and the folder contains `resources/app.asar`, Cascade launches the exe with:
  - `--remote-debugging-port=<freePort>`
  - `--user-data-dir=<tempDir>`
  - then connects via `chromium.connect_over_cdp(...)`.
- Otherwise, Cascade falls back to **gRPC/UIA**.

## Tooling

### Base tools (recommended first)

The standard tools remain the same and are routed automatically:

- `start_app`
- `get_semantic_tree`
- `get_screenshot`
- `click_element`
- `type_text`
- `hover_element`
- `focus_element`
- `scroll_element`
- `wait_visible`

For `WEB` selectors, these are executed via Playwright automatically.

### Extended Playwright tools

For richer web/electron flows (navigation, DOM selection, evaluation), use `pw_*` tools such as:

- `pw_goto`, `pw_back`, `pw_forward`, `pw_reload`, `pw_wait_for_url`
- `pw_click`, `pw_fill`, `pw_press`, `pw_select_option`
- `pw_eval`, `pw_eval_on_selector`
- `pw_list_frames`
- `pw_get_cookies`

## Skill selector conventions for Web/Electron

- Set `selector.platform_source = WEB`
- Prefer `selector.id` when the DOM has a stable `id` (e.g. `login-button`).
- If there is no DOM id, store a stable Playwright selector string:
  - `selector.id = "css=..."`
  - or `selector.path = ["css=..."]` for chained selectors

The base tools will route correctly as long as `platform_source` is `WEB`.

## Setup

Install Python deps and Playwright browsers:

```powershell
cd python
pip install -r requirements.txt
python -m playwright install
```


