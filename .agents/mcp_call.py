#!/usr/bin/env python3
import sys, json, urllib.request

URL = "http://127.0.0.1:9285/p/2d134c5c/t/92023d1a44efcc017ce53ab4a8bf3409/mcp"

def call_tool(name, arguments={}):
    payload = json.dumps({
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {"name": name, "arguments": arguments}
    }).encode("utf-8")
    req = urllib.request.Request(URL, data=payload, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            if "result" in data and "content" in data["result"]:
                for item in data["result"]["content"]:
                    if item.get("type") == "text":
                        print(item.get("text"))
                    else:
                        print(item)
            elif "error" in data:
                print("MCP ERROR:", data["error"])
            else:
                print(json.dumps(data, indent=2))
    except Exception as e:
        print("HTTP ERROR:", e)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: mcp_tool.py <tool_name> [json_args_or_file]")
        sys.exit(1)
    tool_name = sys.argv[1]
    args = {}
    if len(sys.argv) > 2:
        arg_str = sys.argv[2]
        if arg_str.startswith("@"):
            with open(arg_str[1:], "r") as f:
                args = json.load(f)
        else:
            args = json.loads(arg_str)
    call_tool(tool_name, args)
