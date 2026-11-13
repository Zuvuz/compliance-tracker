// US-138: what the load test's scripts share: the addresses, the sign-in through Keycloak's own form, and a user's round
// through the tracker. Only test users and test data (R6); the addresses point at the test stack in docker compose.
import http from 'k6/http';
import exec from 'k6/execution';
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

// R3: pages under 1 second, report downloads under 5 seconds (p95), and under 1 % errors. Every user must be signed in
// and every page must come from the tracker itself, or the figures measure the sign-in page instead (fixed after the
// first run, where no user was signed in).
export const limits = {
  'http_req_duration{kind:page}': ['p(95)<1000'],
  'http_req_duration{kind:report}': ['p(95)<5000'],
  http_req_failed: ['rate<0.01'],
  'checks{check:signed in}': ['rate==1'],
  'checks{check:from the tracker}': ['rate>0.99'],
};

// The text of an element on a Keycloak page, to say why a sign-in stopped there
const text = (doc, selector) => doc.find(selector).text().trim().replace(/\s+/g, ' ');

// Sign in like a browser: a page that needs sign-in sends us to Keycloak, whose form we fill in and post
// stopOnFailure: the smoke and load tests stop, as their figures would be wrong; the stress test goes on (R4)
export function signIn(username, stopOnFailure = true) {
  if (!PASSWORD) {
    throw new Error('Set LOAD_PASSWORD to the test users\' password');
  }

  const login = http.get(`${BASE}/Projects`, { redirects: 10, tags: { kind: 'signin' } });
  const form = parseHTML(login.body).find('#kc-form-login');
  const action = form.attr('action');
  if (!action) {
    throw new Error(`No Keycloak sign-in form for ${username} (status ${login.status})`);
  }

  let back = http.post(action, { username, password: PASSWORD, credentialId: '' }, { redirects: 10, tags: { kind: 'signin' } });

  // Keycloak answers with a page whose form a browser posts to /signin-oidc by script (response_mode=form_post);
  // k6 runs no script, so the form is posted here, as the browser would
  const answer = parseHTML(String(back.body)).find('form[action$="/signin-oidc"]');
  if (answer.size() > 0) {
    const fields = {};
    // Every named field (Keycloak writes TYPE="HIDDEN" in capitals, which a [type=hidden] selector misses)
    answer.find('input').each((_, input) => {
      const name = input.getAttribute('name');
      if (name) {
        fields[name] = input.getAttribute('value') || '';
      }
    });
    back = http.post(answer.attr('action'), fields, { redirects: 10, tags: { kind: 'signin' } });
  }
  const signedIn = back.status === 200 && back.url.startsWith(BASE) && !String(back.body).includes('kc-form-login');
  check(back, { 'signed in': () => signedIn });
  if (!signedIn) {
    // Where it stopped and what Keycloak said, without the password; the test stops, as its figures would be wrong
    const doc = parseHTML(String(back.body));
    const forms = [];
    doc.find('form').each((_, f) => forms.push(f.getAttribute('id') || f.getAttribute('action').split('?')[0].split('/').pop()));
    console.error(`Sign-in of ${username} stopped at ${back.url.split('?')[0]} (status ${back.status}): `
      + `title "${text(doc, 'title')}", heading "${text(doc, 'h1')}", forms [${forms.join(', ')}], `
      + `message "${text(doc, '[id*=error], [class*=alert], [class*=helper-text], [class*=feedback]').slice(0, 200)}"`);
    if (stopOnFailure) {
      exec.test.abort(`${username} could not sign in`);
    }
  }
}

// The projects the signed-in user can see, from the links on the projects page
export function projectIds(stopOnFailure = true) {
  const page = http.get(`${BASE}/Projects`, { tags: { kind: 'page' } });
  const ids = [];
  parseHTML(page.body).find('a[href^="/Projects/Details/"]').each((_, a) => {
    const id = a.getAttribute('href').split('/')[3];
    if (id && !ids.includes(id)) {
      ids.push(id);
    }
  });
  if (ids.length === 0 && stopOnFailure) {
    exec.test.abort(`The projects page lists no project (status ${page.status}, ${page.url.split('?')[0]})`);
  }
  return ids;
}

// The figures that answer R3, as text for the job's log (handleSummary replaces k6's own summary)
export function summaryText(name, data) {
  const m = data.metrics;
  const p95 = (key) => (m[key] ? `${Math.round(m[key].values['p(95)'])} ms` : 'none');
  const rate = (key) => (m[key] ? `${(m[key].values.rate * 100).toFixed(2)} %` : 'none');
  return [
    `${name}: ${m.http_reqs ? m.http_reqs.values.count : 0} requests, ${m.vus_max ? m.vus_max.values.max : 0} users at most`,
    `  pages p95: ${p95('http_req_duration{kind:page}')} (limit 1000 ms)`,
    `  reports p95: ${p95('http_req_duration{kind:report}')} (limit 5000 ms)`,
    `  failed requests: ${rate('http_req_failed')} (limit 1 %)`,
    `  signed in: ${rate('checks{check:signed in}')}, pages from the tracker: ${rate('checks{check:from the tracker}')}`,
    '',
  ].join('\n');
}

// R2: what a user does: the dashboard, a project's tabs, its activity, and now and then a report
export function round(projects) {
  if (projects.length === 0) {
    return; // a stress user that could not sign in has nothing to do
  }

  const id = projects[Math.floor(Math.random() * projects.length)];
  const get = (path, kind) => {
    const r = http.get(`${BASE}${path}`, { tags: { kind, name: path.replace(id, '{id}') } });
    check(r, {
      [`${kind} answered`]: (x) => x.status === 200,
      'from the tracker': (x) => x.url.startsWith(BASE),
    });
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
