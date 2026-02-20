# OpenClaw Supported Tools & Instruments

> Comprehensive reference for all agent capabilities in OpenClaw

---

## Overview

OpenClaw provides a rich set of tools (also referred to as "instruments") that enable agents to interact with the outside world. These tools are organized into functional groups and can be configured per-agent for security and capability control.

---

## Core Tool Categories

### 🔧 Runtime & Execution

| Tool | Description |
|------|-------------|
| `exec` | Run shell commands in the workspace with support for background processes, PTY, and elevated execution |
| `process` | Manage background exec sessions (poll, log, write, kill) |

### 📁 File System

| Tool | Description |
|------|-------------|
| `read` | Read file contents (supports text files and images) |
| `write` | Create or overwrite files |
| `edit` | Make precise edits to files using exact text replacement |
| `apply_patch` | Apply structured patches across multiple files |

### 🌐 Web & Browser

| Tool | Description |
|------|-------------|
| `browser` | Control a dedicated browser for automation, screenshots, and UI interaction |
| `web_search` | Search the web using Brave Search API |
| `web_fetch` | Fetch and extract readable content from URLs |

### 💬 Messaging

| Tool | Description |
|------|-------------|
| `message` | Send messages and channel actions across Discord, Google Chat, Slack, Telegram, WhatsApp, Signal, iMessage, and MS Teams |

### 📅 Scheduling

| Tool | Description |
|------|-------------|
| `cron` | Manage Gateway cron jobs, scheduled tasks, and wake events |

### 🎨 Canvas & UI

| Tool | Description |
|------|-------------|
| `canvas` | Drive node Canvas for rendering, A2UI interactions, and visual output |

### 📱 Nodes & Devices

| Tool | Description |
|------|-------------|
| `nodes` | Discover and control paired devices; capture camera, screen, location; send notifications and run commands |

### 🧠 Memory & Context

| Tool | Description |
|------|-------------|
| `memory_search` | Semantic search across MEMORY.md and daily memory files |
| `memory_get` | Retrieve specific memory snippets |

### 👥 Session Management

| Tool | Description |
|------|-------------|
| `sessions_list` | List active sessions with optional filtering |
| `sessions_history` | Fetch message history for any session |
| `sessions_send` | Send a message to another session |
| `sessions_spawn` | Spawn a sub-agent in a background session |
| `session_status` | Get status of current or specified session |
| `agents_list` | List agent IDs available for spawning |

### 🖼️ Media

| Tool | Description |
|------|-------------|
| `image` | Analyze images using the configured image model |

---

## Tool Groups

For convenience, tools can be referenced by groups in configuration:

| Group | Includes |
|-------|----------|
| `group:runtime` | `exec`, `bash`, `process` |
| `group:fs` | `read`, `write`, `edit`, `apply_patch` |
| `group:sessions` | `sessions_list`, `sessions_history`, `sessions_send`, `sessions_spawn`, `session_status` |
| `group:memory` | `memory_search`, `memory_get` |
| `group:web` | `web_search`, `web_fetch` |
| `group:ui` | `browser`, `canvas` |
| `group:automation` | `cron`, `gateway` |
| `group:messaging` | `message` |
| `group:nodes` | `nodes` |
| `group:openclaw` | All built-in OpenClaw tools |

---

## Tool Profiles

OpenClaw provides pre-configured tool profiles for common use cases:

| Profile | Description |
|---------|-------------|
| `minimal` | Session status only |
| `coding` | File system, runtime, sessions, memory, and image tools |
| `messaging` | Messaging tools plus session management |
| `full` | No restrictions (same as unset) |

---

## Configuration

Tools can be allowlisted or denied via `openclaw.json`:

```json5
{
  tools: {
    deny: ["browser"],
    allow: ["group:fs", "message"]
  }
}
```

Tools can also be restricted by provider:

```json5
{
  tools: {
    byProvider: {
      "google-antigravity": { profile: "minimal" }
    }
  }
}
```

---

## Safety & Permissions

- **Allow/Deny**: Use `tools.allow` and `tools.deny` to control access
- **Elevated Mode**: Some tools support `elevated` execution for additional permissions
- **Session Visibility**: Control which sessions are visible to agents via `tools.sessions.visibility`

---

## Plugin Tools (Optional)

Additional tools may be available via plugins:

| Plugin | Description |
|--------|-------------|
| Lobster | Typed workflow runtime with resumable approvals |
| LLM Task | JSON-only LLM step for structured output |
| Firecrawl | Anti-bot fallback for web fetching |

---

## Quick Reference

### Common Agent Flows

**Browser Automation:**
1. `browser` → `start`
2. `snapshot` (ai or aria)
3. `act` (click/type/press)
4. `screenshot` for confirmation

**Node Interaction:**
1. `nodes` → `status`
2. `describe` on target node
3. `notify` / `run` / `camera_snap` / `screen_record`

**Session Spawning:**
1. `agents_list` → check available agents
2. `sessions_spawn` → start sub-agent
3. Monitor completion via announcement

---

*Last updated: February 2026*
