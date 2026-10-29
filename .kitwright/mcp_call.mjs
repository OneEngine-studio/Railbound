import http from "http";

const MCP_URL = "http://127.0.0.1:9285/p/0c5f74cb/t/b2ef602f73e51c6657329a4cdc060bee/mcp";

function post(body) {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify(body);
    const url = new URL(MCP_URL);
    const req = http.request(
      {
        hostname: url.hostname,
        port: url.port,
        path: url.pathname,
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Content-Length": Buffer.byteLength(data),
        },
        timeout: 180000,
      },
      (res) => {
        let raw = "";
        res.on("data", (c) => (raw += c));
        res.on("end", () => {
          try {
            resolve(JSON.parse(raw));
          } catch (e) {
            reject(new Error("Bad JSON: " + raw.slice(0, 500)));
          }
        });
      }
    );
    req.on("error", reject);
    req.write(data);
    req.end();
  });
}

async function main() {
  const tool = process.argv[2];
  if (!tool) {
    console.error("Usage: node mcp_call.mjs <toolName> [jsonArgs]");
    process.exit(1);
  }
  let args = {};
  if (process.argv[3]) {
    if (process.argv[3] === "--file") {
      const fs = await import("fs");
      args = JSON.parse(fs.readFileSync(process.argv[4], "utf8"));
    } else {
      args = JSON.parse(process.argv[3]);
    }
  }
  await post({
    jsonrpc: "2.0",
    id: 1,
    method: "initialize",
    params: {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "cursor-agent", version: "1.0" },
    },
  });
  const result = await post({
    jsonrpc: "2.0",
    id: 2,
    method: "tools/call",
    params: { name: tool, arguments: args },
  });
  console.log(JSON.stringify(result, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
