#!/usr/bin/env node
// Minimal MCP-over-HTTP client for the MCP for Unity server (http://127.0.0.1:8080/mcp).
// Lets scripts and agents call Unity tools without registering the server in their client.
//
// Usage:
//   node Tools/unity-mcp.mjs tools                     # list tool names
//   node Tools/unity-mcp.mjs describe <tool>           # print a tool's input schema
//   node Tools/unity-mcp.mjs call <tool> '<json args>' # call a tool ('-' reads args from stdin)

import fs from "node:fs";

const url = process.env.UNITY_MCP_URL || "http://127.0.0.1:8080/mcp";
let sessionId = null;
let nextId = 1;

async function rpc(method, params, isNotification = false) {
  const body = { jsonrpc: "2.0", method, ...(params ? { params } : {}) };
  if (!isNotification) body.id = nextId++;
  const headers = { "Content-Type": "application/json", Accept: "application/json, text/event-stream" };
  if (sessionId) headers["Mcp-Session-Id"] = sessionId;
  const res = await fetch(url, { method: "POST", headers, body: JSON.stringify(body) });
  sessionId = res.headers.get("mcp-session-id") ?? sessionId;
  if (isNotification) return null;
  const text = await res.text();
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${text}`);
  // Streamable HTTP may answer with SSE; take the data line carrying our response id.
  const payloads = (res.headers.get("content-type") || "").includes("text/event-stream")
    ? text.split(/\r?\n/).filter((l) => l.startsWith("data:")).map((l) => JSON.parse(l.slice(5)))
    : [JSON.parse(text)];
  const msg = payloads.find((p) => p.id === body.id) ?? payloads.at(-1);
  if (msg.error) throw new Error(JSON.stringify(msg.error));
  return msg.result;
}

async function connect() {
  await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "moremush-unity-mcp-cli", version: "1.0" },
  });
  await rpc("notifications/initialized", undefined, true);
}

async function main() {
  const [cmd, name, rawArgs] = process.argv.slice(2);
  await connect();
  if (cmd === "tools") {
    const { tools } = await rpc("tools/list", {});
    console.log(tools.map((t) => t.name).join("\n"));
  } else if (cmd === "describe") {
    const { tools } = await rpc("tools/list", {});
    const tool = tools.find((t) => t.name === name);
    if (!tool) throw new Error(`no tool named ${name}`);
    console.log(JSON.stringify({ description: tool.description, inputSchema: tool.inputSchema }, null, 1));
  } else if (cmd === "call") {
    const argText = rawArgs === "-" ? fs.readFileSync(0, "utf8") : rawArgs || "{}";
    const result = await rpc("tools/call", { name, arguments: JSON.parse(argText) });
    const text = (result.content || []).filter((c) => c.type === "text").map((c) => c.text).join("\n");
    console.log(text || JSON.stringify(result.structuredContent ?? result));
    if (result.isError) process.exitCode = 1;
  } else {
    console.error("usage: unity-mcp.mjs tools | describe <tool> | call <tool> '<json>'");
    process.exitCode = 2;
  }
}

main().catch((e) => {
  console.error(`unity-mcp: ${e.message}`);
  process.exit(1);
});
