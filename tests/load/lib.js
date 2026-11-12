// US-138: what the load test's scripts share: the addresses, the sign-in through Keycloak's own form, and a user's round
// through the tracker. Only test users and test data (R6); the addresses point at the test stack in docker compose.
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { parseHTML } from 'k6/html';

export const BASE = __ENV.BASE_URL || 'https://localhost:8443';
export const USERS = parseInt(__ENV.LOAD_USERS || '50', 10);
const PASSWORD = __ENV.LOAD_PASSWORD;

// The test stack uses its own certificate authority, and Keycloak's name points at this machine
export const common = {
  insecureSkipTLSVerify: true,
  hosts: { 'keycloak.localhost': '127.0.0.1' },
};

// R3: pages under 1 second, report downloads under 5 seconds (p95), and under 1 % errors
export const limits = {
  'http_req_duration{kind:page}': ['p(95)<1000'],
  'http_req_duration{kind:report}': ['p(95)<5000'],
  http_req_failed: ['rate<0.01'],
};

// Sign in like a browser: a page that needs sign-in sends us to Keycloak, whose form we fill in and post
export function signIn(username) {
  if (!PASSWORD) {
    throw new Error('Set LOAD_PASSWORD to the test users\' password');
  }

  const login = http.get(`${BASE}/Projects`, { redirects: 10, tags: { kind: 'signin' } });
  const form = parseHTML(login.body).find('#kc-form-login');
  const action = form.attr('action');
  if (!action) {
    throw new Error(`No Keycloak sign-in form for ${username} (status ${login.status})`);
  }

  const back = http.post(action, { username, password: PASSWORD, credentialId: '' }, { redirects: 10, tags: { kind: 'signin' } });
  check(back, { 'signed in': (r) => r.status === 200 && r.url.startsWith(BASE) });
}

// The projects the signed-in user can see, from the links on the projects page
export function projectIds() {
  const page = http.get(`${BASE}/Projects`, { tags: { kind: 'page' } });
  const ids = [];
  parseHTML(page.body).find('a[href^="/Projects/Details/"]').each((_, a) => {
    const id = a.getAttribute('href').split('/')[3];
    if (id && !ids.includes(id)) {
      ids.push(id);
    }
  });
  return ids;
}

// R2: what a user does: the dashboard, a project's tabs, its activity, and now and then a report
export function round(projects) {
  const id = projects[Math.floor(Math.random() * projects.length)];
  const get = (path, kind) => {
    const r = http.get(`${BASE}${path}`, { tags: { kind, name: path.replace(id, '{id}') } });
    check(r, { [`${kind} answered`]: (x) => x.status === 200 });
    sleep(Math.random() * 2 + 1);
  };

  group('pages', () => {
    get('/', 'page');
    get(`/Projects/Details/${id}`, 'page');
    get(`/Projects/Details/${id}/compliance`, 'page');
    get(`/Projects/Details/${id}/risks`, 'page');
    get(`/Projects/Details/${id}/product-requirements`, 'page');
    get(`/Projects/Details/${id}/reports`, 'page');
    get(`/Projects/Details/${id}/activity`, 'page');
  });

  if (Math.random() < 0.2) {
    group('reports', () => {
      get(`/Projects/Traceability/${id}?format=xlsx`, 'report');
      get(`/Projects/Traceability/${id}?format=pdf&audience=auditor`, 'report');
    });
  }
}
