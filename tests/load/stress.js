// US-138 R4: more and more users until p95 or the errors pass the limits; the summary records where that happened.
// The limits do not stop the run, so it goes on to the top; the workflow then checks that it recovers (R5).
import exec from 'k6/execution';
import { common, projectIds, round, signIn, summaryText, USERS } from './lib.js';

export const options = {
  ...common,
  scenarios: {
    stress: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '1m', target: USERS },
        { duration: '1m', target: USERS * 2 },
        { duration: '1m', target: USERS * 3 },
        { duration: '1m', target: USERS * 4 },
        { duration: '30s', target: 0 },
      ],
    },
  },
  thresholds: {
    'http_req_duration{kind:page}': [{ threshold: 'p(95)<1000', abortOnFail: false }],
    http_req_failed: [{ threshold: 'rate<0.01', abortOnFail: false }],
  },
};

let projects;

export default function () {
  if (exec.vu.iterationInScenario === 0) {
    signIn(`load-user-${((exec.vu.idInTest - 1) % USERS) + 1}`, false);
    projects = projectIds(false);
  }

  round(projects);
}

export function handleSummary(data) {
  return { '/results/stress-summary.json': JSON.stringify(data, null, 2), stdout: summaryText('Stress test', data) };
}
