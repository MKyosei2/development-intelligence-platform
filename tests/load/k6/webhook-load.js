// Purpose: Load-tests collector ingestion with sustained, burst, and duplicate/idempotency traffic.
import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate } from "k6/metrics";
const accepted = new Counter("dip_ingest_accepted"); const contractFailures = new Rate("dip_ingest_contract_failures");
export const options = { scenarios: { sustained: { executor: "constant-arrival-rate", rate: 40, timeUnit: "1s", duration: "2m", preAllocatedVUs: 30, maxVUs: 120 }, burst: { executor: "ramping-arrival-rate", startRate: 20, timeUnit: "1s", preAllocatedVUs: 20, maxVUs: 200, stages: [{ target: 100, duration: "20s" }, { target: 250, duration: "20s" }, { target: 50, duration: "20s" }], startTime: "30s" } }, thresholds: { http_req_failed: ["rate<0.01"], http_req_duration: ["p(95)<1000", "p(99)<2000"], dip_ingest_contract_failures: ["rate<0.002"] } };
function payload(seq) { const now = new Date().toISOString(); return JSON.stringify({ schemaVersion: "1.0", collectorType: "generic-json", collectorVersion: "load-v2", projectId: __ENV.DIP_PROJECT_ID, commitSha: `load${String(seq).padStart(36,"0")}`.slice(0,40), branch: "load-test", startedAt: now, finishedAt: now, status: "succeeded", metrics: [{ name: "load.sequence", value: seq }], issues: [] }); }
export default function () {
  const baseUrl = __ENV.DIP_API_URL || "http://localhost:7001"; const seq = (__VU * 1000000) + __ITER; const body = payload(seq);
  const response = http.post(`${baseUrl}/api/collectors/reports`, body, { headers: { "Content-Type": "application/json", "X-DIP-Load-Test": "1" }, tags: { route: "collector-ingest" } });
  const ok = check(response, { "collector accepted/idempotent": (r) => r.status === 201 || r.status === 202 || r.status === 409, "no server error": (r) => r.status < 500 }); contractFailures.add(!ok); if (ok) accepted.add(1);
  if (__ITER % 20 === 0) { const duplicate = http.post(`${baseUrl}/api/collectors/reports`, body, { headers: { "Content-Type": "application/json", "X-DIP-Load-Test": "1" }, tags: { route: "collector-duplicate" } }); contractFailures.add(!(duplicate.status === 201 || duplicate.status === 202 || duplicate.status === 409)); }
  sleep(Math.random() * 0.05);
}
