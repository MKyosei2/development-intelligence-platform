// Purpose: Verifies d as hb oa rd l oa d behavior, edge cases, and invariants for k6.
import http from "k6/http";
import { check } from "k6";

export const options = {
  vus: 20,
  duration: "1m",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<500"]
  }
};

export default function () {
  const baseUrl = __ENV.DIP_API_URL || "http://localhost:7001";
  const response = http.get(`${baseUrl}/api/projects`);

  check(response, {
    "projects returned": (r) => r.status === 200
  });
}
