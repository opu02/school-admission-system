import { API_BASE_URL } from './config';
import type { Hold, SchoolClass } from './types';

/** An error the UI can show directly: `message` is already human-friendly. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string,
    readonly extra: Record<string, unknown> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }

  /** True when the server could not be reached at all. */
  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

/** Friendly text per backend error code (falls back to the server's own "detail"). */
const FRIENDLY_MESSAGES: Record<string, string> = {
  NO_SEATS_AVAILABLE: 'Sorry, all seats in this class are taken right now. A seat may open up if someone does not pay in time.',
  ACTIVE_HOLD_EXISTS: 'You already have an active application for this class. Please pay the fee or cancel it first.',
  HOLD_EXPIRED: 'Time is up. The seat was released because the fee was not paid in time. You can apply again if seats are free.',
  HOLD_CANCELLED: 'This application was cancelled. Please apply again.',
  HOLD_ALREADY_CONFIRMED: 'The fee is already paid, so this admission cannot be cancelled online. Please contact the school office.',
  HOLD_NOT_OWNED: 'This application belongs to a different student.',
  HOLD_NOT_FOUND: 'This application could not be found.',
  CLASS_NOT_FOUND: 'This class no longer exists.',
  STUDENT_ID_REQUIRED: 'Please enter the 17-digit birth registration number first.',
  STUDENT_ID_INVALID: 'The birth registration number must be exactly 17 digits.',
  INTERNAL_ERROR: 'Something went wrong on the server. Please try again in a moment.',
};

const NETWORK_MESSAGE = 'Cannot reach the server. Please check your connection; we will keep trying.';

interface ProblemBody {
  detail?: string;
  title?: string;
  code?: string;
  [key: string]: unknown;
}

async function request<T>(method: string, path: string, options: { studentId?: string; body?: unknown } = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (options.studentId) headers['X-Student-Id'] = options.studentId;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(NETWORK_MESSAGE, 0, 'NETWORK_ERROR');
  }

  if (response.status === 204) return undefined as T;

  const text = await response.text();
  let data: unknown = undefined;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = undefined;
    }
  }

  if (!response.ok) {
    const problem = (data ?? {}) as ProblemBody;
    const code = problem.code ?? `HTTP_${response.status}`;
    // Validation messages from the server are already specific ("Total seats must be ...").
    const message =
      (code !== 'VALIDATION_FAILED' && FRIENDLY_MESSAGES[code]) ||
      problem.detail ||
      (response.status >= 500 ? FRIENDLY_MESSAGES.INTERNAL_ERROR : `Request failed (${response.status}).`);
    throw new ApiError(message, response.status, code, problem);
  }

  return data as T;
}

export const api = {
  listClasses: () => request<SchoolClass[]>('GET', '/classes'),
  createClass: (name: string, totalSeats: number) =>
    request<SchoolClass>('POST', '/classes', { body: { name, totalSeats } }),
  createHold: (classId: string, studentId: string) =>
    request<Hold>('POST', `/classes/${encodeURIComponent(classId)}/holds`, { studentId }),
  getHold: (holdId: string) => request<Hold>('GET', `/holds/${encodeURIComponent(holdId)}`),
  confirmHold: (holdId: string, studentId: string) =>
    request<Hold>('POST', `/holds/${encodeURIComponent(holdId)}/confirm`, { studentId }),
  cancelHold: (holdId: string, studentId: string) =>
    request<void>('DELETE', `/holds/${encodeURIComponent(holdId)}`, { studentId }),
  myHolds: (studentId: string) => request<Hold[]>('GET', '/me/holds', { studentId }),
};

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  return 'Something unexpected happened. Please try again.';
}
