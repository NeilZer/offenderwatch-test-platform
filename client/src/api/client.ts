import type {
  CleanupReport, Comparison, Dashboard, Environment, Notification, RunDetail, RunSummary, Scenario, Schedule,
  TestCaseItem, TestDataRecord, TestHistory, User,
} from './types';

/** Error with the server's problem-details message, safe to show to the user. */
export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
  }
}

/** Called on any 401 so the app can return to the login screen. */
let onUnauthorized: () => void = () => {};
export const setUnauthorizedHandler = (handler: () => void) => { onUnauthorized = handler; };

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, {
      method,
      credentials: 'same-origin',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'The platform API is not reachable. Is the server running?');
  }
  if (response.status === 401 && !url.endsWith('/auth/login')) onUnauthorized();
  if (!response.ok) throw new ApiError(response.status, await readProblem(response));
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function readProblem(response: Response): Promise<string> {
  try {
    const problem = await response.json();
    if (problem.errors) return Object.values(problem.errors as Record<string, string[]>).flat().join(' ');
    return problem.detail ?? problem.title ?? `Request failed (${response.status})`;
  } catch {
    return response.status === 403 ? 'Your role (viewer) cannot do this — sign in as an operator.' : `Request failed (${response.status})`;
  }
}

const get = <T,>(url: string) => request<T>('GET', url);
const post = <T,>(url: string, body?: unknown) => request<T>('POST', url, body ?? {});
const put = <T,>(url: string, body: unknown) => request<T>('PUT', url, body);
const del = (url: string) => request<void>('DELETE', url);

export interface EnvironmentInput { name: string; baseUrl: string; isDefault: boolean; protectedIds: string }
export interface StartRunInput { environmentId: number; scope: string; grep?: string; keepTestData?: boolean }
export interface ScheduleInput { name: string; cron: string; timeZone: string; environmentId: number; scope: string; enabled: boolean }

export const api = {
  login: (username: string, password: string) => post<User>('/api/auth/login', { username, password }),
  logout: () => post<void>('/api/auth/logout'),
  me: () => get<User>('/api/auth/me'),

  environments: () => get<Environment[]>('/api/environments'),
  createEnvironment: (input: EnvironmentInput) => post<Environment>('/api/environments', input),
  updateEnvironment: (id: number, input: EnvironmentInput) => put<Environment>(`/api/environments/${id}`, input),
  makeDefaultEnvironment: (id: number) => post<Environment>(`/api/environments/${id}/default`),
  deleteEnvironment: (id: number) => del(`/api/environments/${id}`),

  runs: (environmentId?: number) => get<RunSummary[]>(`/api/runs${environmentId ? `?environmentId=${environmentId}` : ''}`),
  run: (id: number) => get<RunDetail>(`/api/runs/${id}`),
  runOutput: (id: number) => get<string[]>(`/api/runs/${id}/output`),
  startRun: (input: StartRunInput) => post<RunSummary>('/api/runs', input),
  stopRun: (id: number) => post<void>(`/api/runs/${id}/stop`),
  compare: (baseId: number, targetId: number) => get<Comparison>(`/api/runs/compare?base=${baseId}&target=${targetId}`),
  result: (id: number) => get<Scenario>(`/api/results/${id}`),

  tests: () => get<TestCaseItem[]>('/api/tests'),
  testHistory: (id: number) => get<TestHistory>(`/api/tests/${id}/history`),
  dashboard: () => get<Dashboard>('/api/dashboard'),

  testData: (environmentId?: number, activeOnly = false) =>
    get<TestDataRecord[]>(`/api/test-data?activeOnly=${activeOnly}${environmentId ? `&environmentId=${environmentId}` : ''}`),
  cleanTestData: (environmentId: number) => post<CleanupReport>('/api/test-data/clean', { environmentId }),

  schedules: () => get<Schedule[]>('/api/schedules'),
  createSchedule: (input: ScheduleInput) => post<Schedule>('/api/schedules', input),
  updateSchedule: (id: number, input: ScheduleInput) => put<Schedule>(`/api/schedules/${id}`, input),
  deleteSchedule: (id: number) => del(`/api/schedules/${id}`),

  settings: () => get<{ webhookUrl: string | null }>('/api/settings'),
  saveSettings: (webhookUrl: string | null) => put<{ webhookUrl: string | null }>('/api/settings', { webhookUrl }),
  notifications: () => get<Notification[]>('/api/notifications'),
};
