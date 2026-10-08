// Purpose: Exercises mixed pipeline/dashboard/QA reads under sustained concurrency.
import http from "k6/http";
import { check, sleep } from "k6";
import { Rate } from "k6/metrics";
const contractFailures = new Rate("dip_pipeline_contract_failures");
export const options = { scenarios: { pipeline_mix: { executor: "ramping-vus", startVUs: 5, stages: [{ target: 25, duration: "30s" }, { target: 75, duration: "60s" }, { target: 100, duration: "60s" }, { target: 10, duration: "30s" }], gracefulRampDown: "20s" } }, thresholds: { http_req_failed: ["rate<0.01"], http_req_duration: ["p(95)<750", "p(99)<1500"], dip_pipeline_contract_failures: ["rate<0.002"] } };
export default function () {
  const baseUrl = __ENV.DIP_API_URL || "http://localhost:7001"; const projectId = __ENV.DIP_PROJECT_ID; if (!projectId) throw new Error("DIP_PROJECT_ID is required for pipeline-load.js");
  const responses = http.batch([["GET", `${baseUrl}/api/v1/pipeline-runs/projects/${projectId}?take=100`, null, { tags: { route: "pipeline-runs" } }], ["GET", `${baseUrl}/api/v1/qa/projects/${projectId}/trends?take=30`, null, { tags: { route: "qa-trends" } }], ["GET", `${baseUrl}/api/v1/dashboard/projects/${projectId}`, null, { tags: { route: "dashboard" } }]]);
  for (const response of responses) contractFailures.add(!check(response, { "pipeline response 200": (r) => r.status === 200, "pipeline response parseable": (r) => { try { JSON.parse(r.body); return true; } catch (_) { return false; } } }));
  sleep(0.05 + Math.random() * 0.15);
}
