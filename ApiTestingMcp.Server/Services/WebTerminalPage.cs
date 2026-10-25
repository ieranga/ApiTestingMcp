namespace ApiTestingMcp.Server.Services;

public static class WebTerminalPage
{
    public static string Render(string currentBaseUrl, string currentOpenApiPath)
    {
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>ApiTestingMcp | AI-Powered API Testing Platform</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;600&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
  <style>
    :root {
      --bg: #090d16;
      --card-bg: #111827;
      --card-border: #1f2937;
      --terminal-bg: #030712;
      --accent-cyan: #06b6d4;
      --accent-emerald: #10b981;
      --accent-amber: #f59e0b;
      --accent-rose: #f43f5e;
      --accent-indigo: #6366f1;
      --text-main: #f9fafb;
      --text-muted: #9ca3af;
      --text-dim: #4b5563;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background-color: var(--bg);
      color: var(--text-main);
      font-family: 'Inter', -apple-system, sans-serif;
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      line-height: 1.5;
    }
    header {
      border-bottom: 1px solid var(--card-border);
      background: rgba(17, 24, 39, 0.7);
      backdrop-filter: blur(12px);
      padding: 1rem 1.5rem;
      position: sticky;
      top: 0;
      z-index: 50;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .logo-container {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .logo-badge {
      background: linear-gradient(135deg, #06b6d4 0%, #6366f1 100%);
      color: white;
      font-weight: 700;
      font-size: 0.85rem;
      padding: 0.25rem 0.6rem;
      border-radius: 6px;
      letter-spacing: 0.05em;
    }
    .title-group h1 {
      font-size: 1.15rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .status-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      background: rgba(16, 185, 129, 0.1);
      border: 1px solid rgba(16, 185, 129, 0.25);
      color: #34d399;
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0.2rem 0.55rem;
      border-radius: 9999px;
    }
    .status-dot {
      width: 7px;
      height: 7px;
      background: #10b981;
      border-radius: 50%;
      box-shadow: 0 0 8px #10b981;
      animation: pulse 2s infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; transform: scale(1); }
      50% { opacity: 0.5; transform: scale(0.85); }
    }
    .header-links a {
      color: var(--text-muted);
      text-decoration: none;
      font-size: 0.85rem;
      display: flex;
      align-items: center;
      gap: 0.4rem;
      transition: color 0.2s;
    }
    .header-links a:hover { color: var(--accent-cyan); }

    main {
      flex: 1;
      max-width: 1400px;
      width: 100%;
      margin: 0 auto;
      padding: 1.5rem;
      display: grid;
      grid-template-columns: 420px 1fr;
      gap: 1.5rem;
      min-width: 0;
    }
    @media (max-width: 1024px) {
      main { grid-template-columns: 1fr; }
    }

    /* Left Control Sidebar */
    .panel {
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 12px;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      min-width: 0;
      overflow: hidden;
    }
    .panel-title {
      font-size: 0.95rem;
      font-weight: 600;
      color: var(--text-main);
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }
    .form-label {
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--text-muted);
    }
    .input-field {
      width: 100%;
      background: var(--terminal-bg);
      border: 1px solid #374151;
      color: var(--text-main);
      padding: 0.6rem 0.75rem;
      border-radius: 8px;
      font-family: 'Fira Code', monospace;
      font-size: 0.85rem;
      outline: none;
      transition: border-color 0.2s, box-shadow 0.2s;
    }
    .input-field:focus {
      border-color: var(--accent-cyan);
      box-shadow: 0 0 0 2px rgba(6, 182, 212, 0.2);
    }

    .preset-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
    .chip {
      background: #1f2937;
      border: 1px solid #374151;
      color: var(--text-muted);
      font-size: 0.72rem;
      padding: 0.25rem 0.55rem;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .chip:hover {
      background: #374151;
      color: var(--text-main);
      border-color: var(--accent-cyan);
    }

    .btn-stack {
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      margin-top: 0.5rem;
    }
    .btn {
      width: 100%;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      transition: all 0.2s;
      border: 1px solid transparent;
    }
    .btn-primary {
      background: linear-gradient(135deg, #06b6d4 0%, #3b82f6 100%);
      color: white;
    }
    .btn-primary:hover {
      box-shadow: 0 0 15px rgba(6, 182, 212, 0.4);
      transform: translateY(-1px);
    }
    .btn-secondary {
      background: #1f2937;
      border-color: #374151;
      color: var(--text-main);
    }
    .btn-secondary:hover {
      background: #283548;
      border-color: #4b5563;
    }
    .btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
      transform: none !important;
      box-shadow: none !important;
    }

    .mcp-info-box {
      background: rgba(99, 102, 241, 0.08);
      border: 1px solid rgba(99, 102, 241, 0.25);
      border-radius: 8px;
      padding: 0.85rem;
      font-size: 0.78rem;
      color: #c7d2fe;
    }
    .mcp-info-box code {
      font-family: 'Fira Code', monospace;
      color: #a5b4fc;
      background: rgba(0,0,0,0.3);
      padding: 0.15rem 0.35rem;
      border-radius: 4px;
    }

    /* Terminal Workspace (Right Panel) */
    .terminal-container {
      background: var(--terminal-bg);
      border: 1px solid var(--card-border);
      border-radius: 12px;
      display: flex;
      flex-direction: column;
      overflow: hidden;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
    }
    .terminal-header {
      background: #0f172a;
      border-bottom: 1px solid var(--card-border);
      padding: 0.65rem 1rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .terminal-controls {
      display: flex;
      align-items: center;
      gap: 0.45rem;
    }
    .terminal-dot {
      width: 11px;
      height: 11px;
      border-radius: 50%;
    }
    .dot-red { background: #ef4444; }
    .dot-yellow { background: #f59e0b; }
    .dot-green { background: #10b981; }
    .terminal-title {
      font-family: 'Fira Code', monospace;
      font-size: 0.75rem;
      color: var(--text-muted);
    }
    .terminal-actions {
      display: flex;
      gap: 0.5rem;
    }
    .terminal-action-btn {
      background: transparent;
      border: none;
      color: var(--text-muted);
      cursor: pointer;
      font-size: 0.75rem;
      transition: color 0.2s;
    }
    .terminal-action-btn:hover { color: var(--text-main); }

    .terminal-tabs {
      display: flex;
      background: #090e1a;
      border-bottom: 1px solid var(--card-border);
    }
    .tab-btn {
      padding: 0.5rem 1.1rem;
      background: transparent;
      border: none;
      border-bottom: 2px solid transparent;
      color: var(--text-muted);
      font-size: 0.8rem;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }
    .tab-btn.active {
      color: var(--accent-cyan);
      border-bottom-color: var(--accent-cyan);
      background: rgba(6, 182, 212, 0.05);
    }

    .terminal-body {
      flex: 1;
      padding: 1.25rem;
      font-family: 'Fira Code', monospace;
      font-size: 0.82rem;
      overflow-y: auto;
      max-height: 600px;
      min-height: 480px;
      color: #e2e8f0;
      line-height: 1.6;
      white-space: pre-wrap;
      word-break: break-word;
    }
    .prompt-line {
      color: #38bdf8;
      display: flex;
      align-items: center;
      gap: 0.4rem;
      margin-bottom: 0.5rem;
    }
    .prompt-cursor {
      display: inline-block;
      width: 8px;
      height: 15px;
      background: #38bdf8;
      animation: blink 1s infinite;
      vertical-align: middle;
    }
    @keyframes blink {
      0%, 100% { opacity: 1; }
      50% { opacity: 0; }
    }

    /* Terminal Markdown Table Styling */
    .table-container {
      overflow-x: auto;
      margin: 1rem 0;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.78rem;
      background: #0b1120;
      border-radius: 6px;
      overflow: hidden;
    }
    th, td {
      padding: 0.55rem 0.75rem;
      border: 1px solid #1e293b;
      text-align: left;
    }
    th {
      background: #1e293b;
      color: #38bdf8;
      font-weight: 600;
    }
    tr:nth-child(even) { background: #070c18; }
    .badge-pass {
      color: #34d399;
      font-weight: 700;
      background: rgba(52, 211, 153, 0.1);
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
    }
    .badge-fail {
      color: #f87171;
      font-weight: 700;
      background: rgba(248, 113, 113, 0.1);
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
    }

    /* Config Modal / View */
    .config-code-box {
      background: #020617;
      border: 1px solid #1e293b;
      border-radius: 8px;
      overflow: hidden;
      font-family: 'Fira Code', monospace;
      width: 100%;
      max-width: 100%;
    }
    .config-code-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: #0b1120;
      padding: 0.45rem 0.75rem;
      border-bottom: 1px solid #1e293b;
    }
    .config-filename {
      font-size: 0.72rem;
      color: #94a3b8;
      font-weight: 500;
    }
    .copy-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      background: #1e293b;
      border: 1px solid #334155;
      color: #94a3b8;
      font-size: 0.72rem;
      padding: 0.25rem 0.55rem;
      border-radius: 4px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .copy-btn:hover { color: white; background: #334155; }
    .config-code-box pre {
      padding: 0.75rem;
      font-size: 0.74rem;
      line-height: 1.5;
      color: #cbd5e1;
      white-space: pre-wrap;
      word-break: break-all;
      overflow-x: auto;
      margin: 0;
    }

    .download-report-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      background: linear-gradient(135deg, rgba(6, 182, 212, 0.15) 0%, rgba(59, 130, 246, 0.15) 100%);
      border: 1px solid rgba(6, 182, 212, 0.4);
      color: #38bdf8;
      font-size: 0.76rem;
      font-weight: 600;
      padding: 0.35rem 0.8rem;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .download-report-btn:hover {
      background: linear-gradient(135deg, rgba(6, 182, 212, 0.35) 0%, rgba(59, 130, 246, 0.35) 100%);
      border-color: #06b6d4;
      color: #ffffff;
      box-shadow: 0 0 10px rgba(6, 182, 212, 0.3);
      transform: translateY(-1px);
    }
    .report-action-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 0.75rem;
      background: #090e1a;
      border: 1px solid #1e293b;
      border-radius: 8px;
      padding: 0.65rem 0.9rem;
      margin-bottom: 1.25rem;
    }
  </style>
</head>
<body>

  <header>
    <div class="logo-container">
      <span class="logo-badge">MCP</span>
      <div class="title-group">
        <h1>ApiTestingMcp <span class="status-pill"><span class="status-dot"></span> Online</span></h1>
      </div>
    </div>
    <div class="header-links">
      <a href="https://github.com/ieranga/ApiTestingMcp" target="_blank" rel="noopener">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
        GitHub
      </a>
    </div>
  </header>

  <main>
    <!-- Left Configuration Control Deck -->
    <div class="panel">
      <div class="panel-title">
        <span>Target API Setup</span>
        <span style="font-size:0.75rem; color:var(--accent-cyan); font-weight:500;">Live Control</span>
      </div>

      <div class="form-group">
        <label class="form-label" for="targetUrl">Target API Base URL</label>
        <input type="url" id="targetUrl" class="input-field" placeholder="https://api.yourdomain.com" value="{{currentBaseUrl}}">
      </div>

      <div class="form-group">
        <label class="form-label" for="openApiPath">OpenAPI / Swagger Path</label>
        <input type="text" id="openApiPath" class="input-field" placeholder="/swagger/v1/swagger.json" value="{{currentOpenApiPath}}">
      </div>

      <div class="form-group">
        <label class="form-label">Quick Test Presets</label>
        <div class="preset-chips">
          <span class="chip" onclick="applyPreset(window.location.origin + '/', '/swagger/v1/swagger.json')">⚡ Built-in Sample API</span>
          <span class="chip" onclick="applyPreset('https://petstore.swagger.io', '/v2/swagger.json')">🐾 Swagger Petstore</span>
          <span class="chip" onclick="applyPreset('https://dummyjson.com', '/docs')">📦 DummyJSON</span>
        </div>
      </div>

      <div class="btn-stack">
        <button id="btnHealth" class="btn btn-secondary" onclick="runHealthCheck()">
          🩺 Run Pre-Flight Health Check
        </button>
        <button id="btnDiscover" class="btn btn-secondary" onclick="runDiscoverEndpoints()">
          🔍 Discover Endpoints
        </button>
        <button id="btnTestSuite" class="btn btn-primary" onclick="runTestSuite()">
          🧪 Generate & Run Test Suite
        </button>
        <button id="btnDownloadPanel" class="btn btn-secondary" style="display:none; border-color:#06b6d4; color:#38bdf8; background:rgba(6,182,212,0.1);" onclick="downloadReport('md')">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
          <span>Download Test Report (.md)</span>
        </button>
      </div>

      <!-- Claude Desktop / Cursor Config Section -->
      <div class="mcp-info-box">
        <strong style="display:block; margin-bottom:0.25rem;">Connect AI Assistant (Claude / Cursor):</strong>
        <p style="margin-bottom:0.5rem; color:var(--text-muted); font-size:0.75rem;">Use this endpoint in your MCP client config:</p>
        <div class="config-code-box">
          <div class="config-code-header">
            <span class="config-filename">claude_desktop_config.json</span>
            <button class="copy-btn" onclick="copyMcpConfig()" title="Copy configuration">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
              <span id="copyBtnText">Copy</span>
            </button>
          </div>
          <pre id="mcpConfigSnippet">{
  "mcpServers": {
    "api-tester": {
      "url": "{{currentBaseUrl}}mcp"
    }
  }
}</pre>
        </div>
      </div>
    </div>

    <!-- Right Terminal Output Screen -->
    <div class="terminal-container">
      <div class="terminal-header">
        <div class="terminal-controls">
          <span class="terminal-dot dot-red"></span>
          <span class="terminal-dot dot-yellow"></span>
          <span class="terminal-dot dot-green"></span>
          <span class="terminal-title" style="margin-left:0.5rem;">mcp-terminal@azure:~$ live-session</span>
        </div>
        <div class="terminal-actions">
          <button class="terminal-action-btn" onclick="clearTerminal()">Clear</button>
        </div>
      </div>

      <div class="terminal-tabs" style="display:flex; justify-content:space-between; align-items:center;">
        <div style="display:flex;">
          <button class="tab-btn active" id="tabTerminal" onclick="switchTab('terminal')">Live Console</button>
          <button class="tab-btn" id="tabReport" onclick="switchTab('report')">Structured Report</button>
        </div>
        <div style="padding-right:0.75rem;">
          <button id="btnDownloadReport" class="download-report-btn" style="display:none;" onclick="downloadReport('md')" title="Export full report as Markdown">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            <span>Download Report (.md)</span>
          </button>
        </div>
      </div>

      <div id="terminalBody" class="terminal-body">
<span style="color:#06b6d4;">==================================================</span>
<span style="color:#38bdf8; font-weight:600;"> ApiTestingMcp Live Interactive Terminal</span>
<span style="color:#06b6d4;">==================================================</span>

Connected to Streamable HTTP MCP Engine on .NET 9.
Paste any public API or ngrok URL on the left, then click a test action.

<span class="prompt-line">mcp-tester:~$ <span class="prompt-cursor"></span></span>
      </div>

      <div id="reportContainer" class="terminal-body" style="display:none;">
        <p style="color:var(--text-muted);">No test suite report generated yet. Run a test suite to view results table.</p>
      </div>
    </div>
  </main>

  <script>
    const baseUrlInput = document.getElementById('targetUrl');
    const openApiPathInput = document.getElementById('openApiPath');
    const terminal = document.getElementById('terminalBody');
    const reportContainer = document.getElementById('reportContainer');

    function applyPreset(url, swagger) {
      baseUrlInput.value = url;
      openApiPathInput.value = swagger;
      logToTerminal(`\n[PRESET APPLIED] Target URL set to ${url} (Swagger: ${swagger})`);
    }

    function switchTab(tab) {
      document.getElementById('tabTerminal').classList.toggle('active', tab === 'terminal');
      document.getElementById('tabReport').classList.toggle('active', tab === 'report');
      if (tab === 'terminal') {
        terminal.style.display = 'block';
        reportContainer.style.display = 'none';
      } else {
        terminal.style.display = 'none';
        reportContainer.style.display = 'block';
      }
    }

    function logToTerminal(text, isCommand = false) {
      if (isCommand) {
        terminal.innerHTML += `\n<span style="color:#38bdf8; font-weight:600;">mcp-tester:~$ ${text}</span>\n`;
      } else {
        terminal.innerHTML += text + '\n';
      }
      terminal.scrollTop = terminal.scrollHeight;
    }

    function clearTerminal() {
      terminal.innerHTML = '<span class="prompt-line">mcp-tester:~$ <span class="prompt-cursor"></span></span>\n';
    }

    function setButtonsDisabled(disabled) {
      document.querySelectorAll('.btn').forEach(b => b.disabled = disabled);
    }

    async function setTargetApiOnServer() {
      const baseUrl = baseUrlInput.value.trim();
      const openApiPath = openApiPathInput.value.trim();
      const res = await fetch('/api/web/set-target', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ baseUrl, openApiPath })
      });
      return await res.json();
    }

    async function runHealthCheck() {
      setButtonsDisabled(true);
      logToTerminal('check_api_health --url ' + baseUrlInput.value, true);
      logToTerminal('Executing pre-flight connectivity probe...');
      try {
        await setTargetApiOnServer();
        const res = await fetch('/api/web/health', { method: 'POST' });
        const data = await res.json();

        if (data.isOnline) {
          logToTerminal(`\x1b[32m✔ [ONLINE]\x1b[0m ${data.message} (HTTP ${data.httpStatusCode}, ${data.responseTimeMs}ms)`);
        } else {
          logToTerminal(`\x1b[31m✖ [OFFLINE]\x1b[0m ${data.message}`);
        }
      } catch (err) {
        logToTerminal(`✖ [ERROR] Health probe failed: ${err.message}`);
      } finally {
        setButtonsDisabled(false);
      }
    }

    async function runDiscoverEndpoints() {
      setButtonsDisabled(true);
      logToTerminal('list_endpoints --target ' + baseUrlInput.value, true);
      logToTerminal('Downloading and parsing OpenAPI/Swagger specification...');
      try {
        await setTargetApiOnServer();
        const res = await fetch('/api/web/discover', { method: 'POST' });
        const endpoints = await res.json();

        if (!Array.isArray(endpoints) || endpoints.length === 0) {
          logToTerminal('✖ No endpoints discovered. Verify your OpenAPI/Swagger path.');
          return;
        }

        logToTerminal(`\x1b[32m✔ Discovered ${endpoints.length} endpoints successfully:\x1b[0m`);
        endpoints.slice(0, 15).forEach(ep => {
          const methodPadded = (ep.method || 'GET').padEnd(7);
          const tags = (ep.tags && ep.tags.length > 0) ? `[${ep.tags.join(', ')}]` : '';
          logToTerminal(`  ${methodPadded} ${ep.path} ${tags}`);
        });
        if (endpoints.length > 15) {
          logToTerminal(`  ... and ${endpoints.length - 15} more endpoints.`);
        }
      } catch (err) {
        logToTerminal(`✖ [ERROR] Discovery failed: ${err.message}`);
      } finally {
        setButtonsDisabled(false);
      }
    }

    let lastSuiteResult = null;
    let lastReportMarkdown = "";

    async function runTestSuite() {
      setButtonsDisabled(true);
      logToTerminal('run_test_suite --target ' + baseUrlInput.value, true);
      logToTerminal('Synthesizing test scenarios (Happy Path, Boundary, Negative, Security)...');
      logToTerminal('Executing test suite against target...');
      try {
        await setTargetApiOnServer();
        const res = await fetch('/api/web/run-tests', { method: 'POST' });
        const result = await res.json();

        if (result.error) {
          logToTerminal(`✖ [ERROR] Test suite failed: ${result.error}`);
          return;
        }

        lastSuiteResult = result;
        lastReportMarkdown = result.summaryReport || '';

        logToTerminal(`\n\x1b[36m==================================================\x1b[0m`);
        logToTerminal(` Test Suite Completed: ${result.passedCount}/${result.totalTests} Passed (${result.totalDurationMs}ms)`);
        logToTerminal(` Overall Status: ${result.overallStatus}`);
        logToTerminal(`\x1b[36m==================================================\x1b[0m\n`);

        if (result.summaryReport) {
          logToTerminal(result.summaryReport);
          renderMarkdownReport(result.summaryReport);
        }

        // Reveal the download buttons on the control panel and terminal tabs
        const btnPanel = document.getElementById('btnDownloadPanel');
        if (btnPanel) btnPanel.style.display = 'flex';

        const btnTab = document.getElementById('btnDownloadReport');
        if (btnTab) btnTab.style.display = 'inline-flex';

        logToTerminal(`\n\x1b[32m✔ Structured report ready! Use the 'Download Report' button to save as .md or .json.\x1b[0m`);
      } catch (err) {
        logToTerminal(`✖ [ERROR] Suite run failed: ${err.message}`);
      } finally {
        setButtonsDisabled(false);
      }
    }

    function downloadReport(format = 'md') {
      if (!lastReportMarkdown && !lastSuiteResult) {
        alert('No report available to download. Please run the test suite first.');
        return;
      }
      const isJson = format === 'json';
      const content = isJson ? JSON.stringify(lastSuiteResult, null, 2) : (lastReportMarkdown || JSON.stringify(lastSuiteResult, null, 2));
      const ext = isJson ? 'json' : 'md';
      const mime = isJson ? 'application/json;charset=utf-8' : 'text/markdown;charset=utf-8';
      const blob = new Blob([content], { type: mime });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
      a.href = url;
      a.download = `api-test-report-${timestamp}.${ext}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }

    function renderMarkdownReport(reportMarkdown) {
      // Basic markdown table parser for structured tab
      let html = '<div style="line-height:1.7;">';

      const statsText = lastSuiteResult 
        ? `${lastSuiteResult.passedCount}/${lastSuiteResult.totalTests} Passed (${lastSuiteResult.totalDurationMs}ms)`
        : 'Generated Report';

      html += `
        <div class="report-action-bar">
          <div>
            <div style="font-size:0.85rem; font-weight:600; color:#e2e8f0;">Structured Test Report</div>
            <div style="font-size:0.75rem; color:#94a3b8;">${statsText}</div>
          </div>
          <div style="display:flex; gap:0.5rem;">
            <button class="download-report-btn" onclick="downloadReport('md')" title="Download as Markdown">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              <span>Download .MD</span>
            </button>
            <button class="download-report-btn" onclick="downloadReport('json')" style="border-color:#3b82f6; color:#60a5fa;" title="Download as JSON">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              <span>Download .JSON</span>
            </button>
          </div>
        </div>
      `;

      const lines = reportMarkdown.split('\n');
      let inTable = false;
      let tableHtml = '<div class="table-container"><table>';

      lines.forEach(line => {
        if (line.startsWith('|')) {
          if (!inTable) { inTable = true; }
          if (line.includes(':---')) return; // separator line

          const cells = line.split('|').map(c => c.trim()).filter((_, idx, arr) => idx > 0 && idx < arr.length - 1);
          const isHeader = line.includes('Test Scenario');
          const tag = isHeader ? 'th' : 'td';

          tableHtml += '<tr>';
          cells.forEach(cell => {
            let clean = cell.replace(/`([^`]+)`/g, '<code>$1</code>');
            if (clean.includes('PASS')) clean = '<span class="badge-pass">PASS</span>';
            if (clean.includes('FAIL')) clean = '<span class="badge-fail">FAIL</span>';
            tableHtml += `<${tag}>${clean}</${tag}>`;
          });
          tableHtml += '</tr>';
        } else {
          if (inTable) {
            inTable = false;
            tableHtml += '</table></div>';
            html += tableHtml;
            tableHtml = '<div class="table-container"><table>';
          }
          if (line.startsWith('# ')) html += `<h2 style="color:#38bdf8; margin:1rem 0 0.5rem;">${line.replace('# ', '')}</h2>`;
          else if (line.startsWith('## ')) html += `<h3 style="color:#06b6d4; margin:1.25rem 0 0.5rem;">${line.replace('## ', '')}</h3>`;
          else if (line.startsWith('### ')) html += `<h4 style="color:#cbd5e1; margin:0.75rem 0 0.25rem;">${line.replace('### ', '')}</h4>`;
          else if (line.trim()) html += `<p style="margin:0.25rem 0; color:#94a3b8;">${line}</p>`;
        }
      });

      if (inTable) {
        tableHtml += '</table></div>';
        html += tableHtml;
      }
      html += '</div>';

      reportContainer.innerHTML = html;
    }

    function copyMcpConfig() {
      const code = document.getElementById('mcpConfigSnippet').innerText;
      navigator.clipboard.writeText(code).then(() => {
        const btnText = document.getElementById('copyBtnText');
        if (btnText) {
          btnText.innerText = 'Copied!';
          setTimeout(() => btnText.innerText = 'Copy', 2000);
        }
      });
    }

    // Auto-update config snippet with current origin
    window.addEventListener('DOMContentLoaded', () => {
      const currentOrigin = window.location.origin;
      const snippet = document.getElementById('mcpConfigSnippet');
      if (snippet) {
        snippet.innerText = `{\n  "mcpServers": {\n    "api-tester": {\n      "url": "${currentOrigin}/mcp"\n    }\n  }\n}`;
      }
      const targetInput = document.getElementById('targetUrl');
      if (targetInput && (!targetInput.value || targetInput.value.includes('localhost'))) {
        targetInput.value = currentOrigin + '/';
      }
    });
  </script>
</body>
</html>
""";
    }
}
