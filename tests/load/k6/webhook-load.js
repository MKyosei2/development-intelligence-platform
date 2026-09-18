// Purpose: Verifies behavior and invariants covered by w eb ho ok l oa d.
import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  scenarios: {
    webhook_burst: {
      executor: "ramping-arrival-rate",
      startRate: 10,
      timeUnit: "1s",
      preAllocatedVUs: 20,
      maxVUs: 100,
      stages: [
        { target: 50, duration: "30s" },
        { target: 100, duration: "30s" },
        { target: 0, duration: "15s" }
      ]
    }
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1000"]
  }
};

export default function () {
  const baseUrl = __ENV.DIP_API_URL || "http://localhost:7001";
  const payload = JSON.stringify({
    schemaVersion: "1.0",
    collectorType: "generic-json",
    collectorVersion: "0.1.0",
    projectId: __ENV.DIP_PROJECT_ID,
    commitSha: "abcdef1234567890",
    branch: "develop",
    startedAt: new Date().toISOString(),
    finishedAt: new Date().toISOString(),
    status: "succeeded",
    metrics: [],
    issues: []
  });

  const response = http.post(`${baseUrl}/api/collectors/reports`, payload, {
    headers: { "Content-Type": "application/json" }
  });

  check(response, {
    "collector report accepted": (r) => r.status === 201 || r.status === 409
  });

  sleep(0.1);
}
