# -*- coding: utf-8 -*-
"""UnityMCP(HTTP, 127.0.0.1:8080/mcp)를 직접 부르는 작은 도구. 세션의 MCP 연결이 끊겼을 때 쓴다.
사용: python Tools/umcp.py list
      python Tools/umcp.py call <tool> '<json args>'   (args 대신 @파일.json 가능)
"""
import io, json, sys, urllib.request

URL = "http://127.0.0.1:8080/mcp"
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")


def post(body, sid=None, timeout=600):
    h = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if sid:
        h["mcp-session-id"] = sid
    req = urllib.request.Request(URL, json.dumps(body).encode("utf-8"), h)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        sid = r.headers.get("mcp-session-id") or sid
        raw = r.read().decode("utf-8")
    msgs = [json.loads(l[5:]) for l in raw.splitlines() if l.startswith("data:")]
    if not msgs and raw.strip():
        msgs = [json.loads(raw)]
    return sid, msgs


def session():
    sid, _ = post({"jsonrpc": "2.0", "id": 1, "method": "initialize",
                   "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                              "clientInfo": {"name": "umcp", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    return sid


def main():
    sid = session()
    if sys.argv[1] == "list":
        _, m = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"}, sid)
        for t in m[-1]["result"]["tools"]:
            print(t["name"])
        return
    tool, arg = sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else "{}"
    if arg.startswith("@"):
        arg = open(arg[1:], encoding="utf-8").read()
    _, m = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                 "params": {"name": tool, "arguments": json.loads(arg)}}, sid)
    res = m[-1].get("result", m[-1])
    for c in res.get("content", []) if isinstance(res, dict) else []:
        print(c.get("text", c))
    if not isinstance(res, dict) or "content" not in res:
        print(json.dumps(res, ensure_ascii=False)[:4000])


if __name__ == "__main__":
    main()
