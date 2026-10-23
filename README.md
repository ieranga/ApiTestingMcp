# ApiTestingMcp

An AI-driven API Testing Platform implementing the **Model Context Protocol (MCP)** in **.NET 9**. 

`ApiTestingMcp` bridges AI agents / MCP clients with Web APIs for automated exploration, pre-flight health checks, schema-driven validation, authentication handling, and automated test scenario generation with failure diagnostics.

---

## 🌟 Key Features

- **Pre-Flight Health Checks (`check_api_health`)**: Verifies target API reachability, DNS/connection status, and port readiness before executing tests.
- **Dynamic OpenAPI Discovery (`list_endpoints`, `get_endpoint_schema`)**: Discovers endpoints, routes, methods, tags, and request/response JSON schemas from Swagger/OpenAPI documentation.
- **Authentication Lifecycle (`login_api`, `set_bearer_token`, `set_api_key`, `clear_auth`)**: Handles JWT token extraction, token decoding/claims inspection, multi-profile token caching, and automatic credential attachment to authenticated requests.
- **Isolated HTTP Execution (`call_api`, `call_and_validate`)**: Safe, sandboxed request execution against the configured target API with security perimeter checks (blocking SSRF and arbitrary external domains).
- **Automated Scenario Generation & Test Suites (`generate_test_scenarios`, `run_test_suite`)**:
  - Automatically synthesizes Positive (Happy Path), Negative (Missing Required Fields, Empty Strings, 404 Not Found), and Security (Unauthorized/Missing Tokens) test cases directly from OpenAPI schemas.
  - Executes parallel/sequential test runs and generates structured Markdown test reports with automatic root-cause failure analysis.
- **Contract & Response Validation (`validate_response`, `validate_against_schema`)**: JSON dot-notation path evaluation, value assertions, and strict OpenAPI schema contract adherence.

---

## 🏗️ Architecture

```
ApiTestingMcp/
├── ApiTestingMcp.Server/               # ASP.NET Core Web API / MCP Server
│   ├── Configuration/                  # Target API & Server configuration
│   ├── Models/                         # Request, Response, Auth, Validation & Test models
│   ├── Services/
│   │   ├── ApiClientService.cs         # HTTP client with security boundaries & auth injection
│   │   ├── OpenApiService.cs           # OpenAPI parser, resolver, & schema cache
│   │   ├── AuthManagerService.cs       # Token extraction, decoding & profile management
│   │   ├── ResponseValidatorService.cs # Contract & response assertion engine
│   │   └── TestGeneratorService.cs     # Dynamic test scenario synthesis & suite runner
│   ├── Tools/                          # MCP Tools registered with ModelContextProtocol
│   │   ├── ApiTestingTools.cs
│   │   ├── ApiDiscoveryTools.cs
│   │   ├── AuthTools.cs
│   │   ├── ApiValidationTools.cs
│   │   └── TestSuiteTools.cs
│   └── Program.cs                      # Streamable HTTP MCP Server host
│
└── ApiTestingMcp.Client/               # Client demonstrating automated MCP testing lifecycle
    └── Program.cs                      # Pre-flight check -> Discovery -> Scenario Gen -> Test Suite
```

---

## 🛠️ MCP Tools Reference

| Tool Name | Description |
|:---|:---|
| `check_api_health` | Checks whether the configured target API base URL is online and reachable. |
| `list_endpoints` | Discovers all available endpoints, HTTP methods, tags, and summaries via OpenAPI/Swagger. |
| `get_endpoint_schema` | Retrieves parameters, request body schemas, and response definitions for an endpoint. |
| `call_api` | Executes an HTTP request against the configured target API. |
| `login_api` | Sends credentials, extracts JWT token from response, and caches it for subsequent calls. |
| `set_bearer_token` | Explicitly sets a JWT token and decodes claims. |
| `set_api_key` | Configures an API Key header for requests. |
| `clear_auth` | Clears stored authentication credentials. |
| `validate_response` | Validates status code, required JSON keys, and data types on a response. |
| `validate_against_schema`| Validates response structure against the documented OpenAPI contract schema. |
| `call_and_validate` | Calls an endpoint and validates its response in a single operation. |
| `generate_test_scenarios` | Synthesizes positive and negative test cases from the target API schema. |
| `run_test_suite` | Executes a batch test suite and produces a Markdown failure analysis report. |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### 1. Configuration

Configure your target API in `ApiTestingMcp.Server/appsettings.json`:

```json
{
  "TargetApi": {
    "BaseUrl": "https://localhost:7209/",
    "OpenApiPath": "/swagger/v1/swagger.json",
    "TimeoutSeconds": 30
  }
}
```

### 2. Run the MCP Server

```bash
dotnet run --project ApiTestingMcp.Server --urls http://localhost:3001
```

The MCP Server starts listening for streamable HTTP MCP transport on `http://localhost:3001/mcp`.

### 3. Run the Automated Client Runner

In another terminal:

```bash
dotnet run --project ApiTestingMcp.Client
```

The client will:
1. Connect via MCP over HTTP transport.
2. Execute pre-flight reachability checks on the configured `TargetApi`.
3. Discover endpoints and filter tags.
4. Synthesize test scenarios (Happy Path, Boundary, Negative, Security).
5. Execute the test suite and output a Markdown report with failure root-cause diagnostics.

---

## 📄 License

MIT
