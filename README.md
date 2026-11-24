# ApiTestingMcp

An AI-driven API testing tool built as a **Model Context Protocol (MCP)** server in **.NET 9**.

Run it on your own machine and let an AI assistant such as **Claude Desktop, Claude Code or Cursor** discover your REST API, generate test scenarios, run them, validate responses and write a test report for you.

> This is a learning project: I built it to understand how MCP servers work and to share what I learned.

---

## 🌟 Key Features

- **Pre-flight health checks (`check_api_health`)**: Checks that the target API is reachable before running tests.
- **OpenAPI discovery (`list_endpoints`, `get_endpoint_schema`)**: Reads the Swagger/OpenAPI document to find endpoints, methods, tags and request/response schemas.
- **Authentication (`login_api`, `set_bearer_token`, `set_api_key`, `clear_auth`)**: Supports static tokens or API keys from environment variables, login flows with JWT extraction and claim decoding, and multiple auth profiles.
- **HTTP execution (`call_api`, `call_and_validate`)**: Sends requests to the configured target API with stored credentials attached automatically.
- **Automated test suites (`generate_test_scenarios`, `run_test_suite`)**:
  - Generates positive (happy path), negative (missing required fields, empty strings, 404) and security (missing token) test cases from the OpenAPI schema.
  - Runs them and produces a Markdown report with failure analysis.
- **Response validation (`validate_response`, `validate_against_schema`)**: JSON path assertions, type checks and OpenAPI contract validation.

---

## 🏗️ How It Works

```
┌────────────────────┐  stdio (JSON-RPC)  ┌──────────────────────┐   HTTP   ┌──────────────┐
│ AI client          │ ─────────────────► │ ApiTestingMcp.Server │ ───────► │ Your API     │
│ Claude / Cursor    │ ◄───────────────── │ (MCP tools)          │ ◄─────── │ (localhost)  │
└────────────────────┘    tool results    └──────────────────────┘          └──────────────┘
```

1. The AI client starts the MCP server as a local process and talks to it over **stdio**.
2. The AI asks the server which **tools** it offers, such as `list_endpoints` and `run_test_suite`.
3. The AI calls those tools. The server sends real HTTP requests to your API and returns the results.
4. The AI reads the results and writes the test report.

Everything runs on your machine. Your tokens stay in your local MCP client config and are never sent to a hosted service.

```
ApiTestingMcp/
├── ApiTestingMcp.Server/      # MCP server (stdio transport)
│   ├── Configuration/         # Target API options
│   ├── Models/                # Request, response, auth, validation and test models
│   ├── Services/              # HTTP client, OpenAPI parser, auth manager, validator, test generator
│   ├── Tools/                 # MCP tool definitions
│   └── Program.cs             # Host setup
├── ApiTestingMcp.SampleApi/   # Small sample API to try the tools on (http://localhost:3001)
└── ApiTestingMcp.Client/      # Scripted MCP client demo (no AI needed)
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- An MCP client: Claude Desktop, Claude Code or Cursor

### 1. Clone and publish the server

```powershell
git clone https://github.com/ieranga/ApiTestingMcp.git
cd ApiTestingMcp
dotnet publish ApiTestingMcp.Server -c Release -o publish
```

This creates `publish/ApiTestingMcp.Server.dll`. Your MCP client will run it, so note the **full path**, for example `C:/dev/ApiTestingMcp/publish/ApiTestingMcp.Server.dll`.

### 2. (Optional) Start the sample API

If you want to try the tools before testing your own API:

```powershell
dotnet run --project ApiTestingMcp.SampleApi
```

The sample API listens on `http://localhost:3001` and serves its OpenAPI document at `/swagger/v1/swagger.json`.

### 3. Connect your MCP client

The server is configured with environment variables, which every MCP client lets you set for a stdio server:

| Variable | Purpose | Default |
|:---|:---|:---|
| `TARGET_API_BASE_URL` | Base URL of the API to test | `http://localhost:3001/` |
| `TARGET_API_OPENAPI_PATH` | Path to the OpenAPI/Swagger JSON | `/swagger/v1/swagger.json` |
| `TARGET_API_TIMEOUT_SECONDS` | Request timeout | `30` |
| `TARGET_API_TOKEN` | Static Bearer token, sent as `Authorization: Bearer <token>` | *(none)* |
| `TARGET_API_KEY` | Static API key | *(none)* |
| `TARGET_API_KEY_HEADER` | Header name for the API key | `X-Api-Key` |

> Use test or development credentials only. Don't put production secrets in these files.

#### Claude Desktop

Edit `claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{
  "mcpServers": {
    "api-tester": {
      "command": "dotnet",
      "args": ["C:/dev/ApiTestingMcp/publish/ApiTestingMcp.Server.dll"],
      "env": {
        "TARGET_API_BASE_URL": "http://localhost:5000/",
        "TARGET_API_TOKEN": "your-test-token"
      }
    }
  }
}
```

Restart Claude Desktop after saving.

#### Claude Code

```powershell
claude mcp add api-tester --env TARGET_API_BASE_URL=http://localhost:5000/ --env TARGET_API_TOKEN=your-test-token -- dotnet C:/dev/ApiTestingMcp/publish/ApiTestingMcp.Server.dll
```

#### Cursor

Create `.cursor/mcp.json` in your project, or `~/.cursor/mcp.json` to use it in every project:

```json
{
  "mcpServers": {
    "api-tester": {
      "command": "dotnet",
      "args": ["C:/dev/ApiTestingMcp/publish/ApiTestingMcp.Server.dll"],
      "env": {
        "TARGET_API_BASE_URL": "http://localhost:5000/",
        "TARGET_API_KEY": "your-test-api-key",
        "TARGET_API_KEY_HEADER": "X-Api-Key"
      }
    }
  }
}
```

### 4. Ask the AI to test your API

Example prompts:

- *"Check that my API is online, list its endpoints and run the full test suite. Give me a Markdown report."*
- *"Generate test scenarios for the Users endpoints and explain any failures."*
- *"Call GET /api/users/10 and validate the response against the OpenAPI schema."*

You can also switch targets during a chat: *"Use set_target_api to point at http://localhost:7000/."*

---

## 🔐 Trying Auth with the Sample API

The sample API includes static demo credentials (sample values, not real secrets):

| Endpoint | Auth | MCP config value |
|:---|:---|:---|
| `GET /api/secure/profile` | Bearer token | `TARGET_API_TOKEN` = the `SampleBearerToken` value in `ApiTestingMcp.SampleApi/Program.cs` |
| `GET /api/secure/stats` | API key in `X-Api-Key` | `TARGET_API_KEY` = `sample-api-key` |
| `POST /api/Auth/login` | Login flow | Ask the AI to call `login_api` with `user1@gmail.com` / `password123` |

---

## 🧪 Scripted Client Demo (no AI)

`ApiTestingMcp.Client` starts the server over stdio and runs the full flow (health check → discovery → scenario generation → test suite) without an AI:

```powershell
dotnet build
dotnet run --project ApiTestingMcp.Client -- http://localhost:3001/
```

---

## 🛠️ MCP Tools Reference

| Tool Name | Description |
|:---|:---|
| `set_target_api` / `get_target_api` | Change or show the API under test. |
| `check_api_health` | Checks that the target API base URL is online. |
| `list_endpoints` | Lists endpoints, HTTP methods, tags and summaries from OpenAPI/Swagger. |
| `get_endpoint_schema` | Gets parameters, request body schema and response definitions for an endpoint. |
| `call_api` | Sends an HTTP request to the target API. |
| `login_api` | Logs in, extracts the JWT from the response and stores it for later calls. |
| `set_bearer_token` | Sets a JWT token and decodes its claims. |
| `set_api_key` | Sets an API key header for requests. |
| `get_auth_status` / `clear_auth` | Shows or clears stored credentials. |
| `validate_response` | Validates status code, required JSON keys and data types. |
| `validate_against_schema` | Validates a response against its OpenAPI schema. |
| `call_and_validate` | Calls an endpoint and validates the response in one step. |
| `generate_test_scenarios` | Generates positive and negative test cases from the API schema. |
| `run_test_suite` | Runs a batch of tests and produces a Markdown report with failure analysis. |

---

## 📚 What I Learned

- **MCP basics:** an MCP server exposes *tools* with names, descriptions and typed parameters. The AI decides which tool to call from the description, so clear descriptions matter as much as the code.
- **Transports:** I started with a hosted HTTP transport, then moved to **stdio**. For a developer tool, stdio is simpler and safer: no hosting, no shared state between users, and credentials stay on the user's machine.
- **stdout belongs to the protocol:** with stdio, any stray `Console.WriteLine` corrupts the JSON-RPC stream, so all logging goes to stderr.
- **Schema-driven testing:** an OpenAPI document contains enough information to generate useful positive, negative and security test cases automatically.

## ⚠️ Known Limitations

- Generated tests use placeholder values (e.g. ID `1`), so some happy-path tests can fail when that record doesn't exist.
- "Unauthorized" security tests assume every write endpoint needs auth.
- There are no automated unit tests yet.

---

## 📄 License

MIT. Only test APIs you own or have permission to test.
