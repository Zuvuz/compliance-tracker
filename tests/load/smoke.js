// US-138: a short check that the stack and the sign-in work, before and after the heavier tests (R5: it recovers)
import exec from 'k6/execution';
import { common, limits, projectIds, round, signIn } from './lib.js';

export const options = { ...common, vus: 2, duration: '30s', thresholds: limits };

let projects;

export default function () {
  if (exec.vu.iterationInScenario === 0) {
    signIn(`load-user-${exec.vu.idInTest}`);
    projects = projectIds();
  }

  round(projects);
}
