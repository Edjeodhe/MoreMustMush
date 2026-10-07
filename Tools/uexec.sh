#!/usr/bin/env bash
# Run C# (from stdin) inside the Unity Editor through the MCP for Unity server.
cd "$(dirname "$0")/.." && python -c "import json,sys; print(json.dumps({'action':'execute','code':sys.stdin.read()}))" | node Tools/unity-mcp.mjs call execute_code -
