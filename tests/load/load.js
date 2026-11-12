// US-138 R2, R3: 50 signed-in users for 5 minutes doing what users do; a run over a limit fails
import exec from 'k6/execution';
import { common, limits, projectIds, round, signIn, USERS } from './lib.js';

export const options = {
  ...common,
  scenarios: {
    users: { executor: 'constant-vus', vus: USERS, duration: __ENV.LOAD_DURATION || '5m' },
  },
  thresholds: limits,
};

let projects;

export default function () {
  if (exec.vu.iterationInScenario === 0) {
    signIn(`load-user-${exec.vu.idInTest}`);
    projects = projectIds();
  }

  round(projects);
}

export function handleSummary(data) {
  return { '/results/load-summary.json': JSON.stringify(data, null, 2), stdout: '' };
}
