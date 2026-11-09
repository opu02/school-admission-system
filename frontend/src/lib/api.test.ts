import { describe, expect, it, vi } from 'vitest';
import { api, ApiError } from './api';

function respond(status: number, body?: unknown) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), { status }),
  ));
}

describe('api client', () => {
  it('sends the student id header when applying', async () => {
    respond(201, { holdId: 'h_1', status: 'HELD' });
    await api.createHold('c_1', '20152691234567890');

    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toMatch(/\/classes\/c_1\/holds$/);
    expect((init!.headers as Record<string, string>)['X-Student-Id']).toBe('20152691234567890');
  });

  it('turns an error code into a friendly message', async () => {
    respond(409, { status: 409, code: 'NO_SEATS_AVAILABLE', detail: 'raw server text' });

    const error = await api.createHold('c_1', '20152691234567890').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('NO_SEATS_AVAILABLE');
    expect((error as ApiError).message).toMatch(/all seats in this class are taken/i);
  });

  it('keeps the server message for validation errors', async () => {
    respond(400, { code: 'VALIDATION_FAILED', detail: 'Total seats must be a whole number between 1 and 100000.' });

    const error = (await api.createClass('X', 0).catch((e: unknown) => e)) as ApiError;
    expect(error.message).toBe('Total seats must be a whole number between 1 and 100000.');
  });

  it('reports "server down" as a network error, not a crash', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    const error = (await api.listClasses().catch((e: unknown) => e)) as ApiError;
    expect(error.isNetworkError).toBe(true);
    expect(error.message).toMatch(/cannot reach the server/i);
  });
});
