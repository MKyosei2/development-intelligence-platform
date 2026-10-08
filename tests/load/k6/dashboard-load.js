// Purpose: Exercises realistic mixed dashboard/read traffic with ramping arrival rate and latency/contract SLOs.
import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";
const contractFailures = new Rate("dip_contract_failures");
const projectListLatency = new Trend("dip_project_list_latency", true);
export const options = { scenarios: { steady_reads: { executor: "ramping-arrival-rate", startRate: 10, timeUnit: "1s", preAllocatedVUs: 20, maxVUs: 150, stages: [{ target: 25, duration: "30s" }, { target: 60, duration: "60s" }, { target: 100, duration: "60s" }, { target: 25, duration: "30s" }] } }, thresholds: { http_req_failed: ["rate<0.005"], http_req_duration: ["p(95)<500", "p(99)<1000"], dip_contract_failures: ["rate<0.001"], dip_project_list_latency: ["p(95)<400"] } };
export default function () {
  const baseUrl = __ENV.DIP_API_URL || "http://localhost:7001";
  const list = http.get(`${baseUrl}/api/projects`, { tags: { route: "projects" } }); projectListLatency.add(list.timings.duration);
  const listOk = check(list, { "projects status 200": (r) => r.status === 200, "projects json": (r) => { try { JSON.parse(r.body); return true; } catch (_) { return false; } } }); contractFailures.add(!listOk);
  const projectId = __ENV.DIP_PROJECT_ID;
  if (projectId) {
    const responses = http.batch([["GET", `${baseUrl}/api/v1/dashboard/projects/${projectId}`, null, { tags: { route: "dashboard" } }], ["GET", `${baseUrl}/api/v1/qa/projects/${projectId}/trends?take=30`, null, { tags: { route: "qa-trends" } }], ["GET", `${baseUrl}/api/v1/intelligence-dashboard/projects/${projectId}`, null, { tags: { route: "intelligence" } }]]);
    for (const response of responses) contractFailures.add(!check(response, { "project read success": (r) => r.status === 200, "project read bounded": (r) => r.body.length < 5000000 }));
  }
  sleep(Math.random() * 0.2);
}
