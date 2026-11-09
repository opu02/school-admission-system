import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Hold } from '../lib/types';
import { HoldCard } from './HoldCard';

const base: Hold = {
  holdId: 'h_1',
  classId: 'c_1',
  className: 'Class 6 - Morning Shift',
  studentId: '20152691234567890',
  status: 'HELD',
  expiresAt: '2026-11-01T04:10:00Z',
};
const expiresMs = Date.parse(base.expiresAt);

function renderCard(now: number, hold: Hold = base) {
  const handlers = { onPay: vi.fn(), onCancel: vi.fn(), onExpired: vi.fn() };
  const view = render(<HoldCard hold={hold} now={now} busy={null} highlighted={false} {...handlers} />);
  return { ...view, ...handlers };
}

describe('HoldCard', () => {
  it('shows the countdown and enabled buttons while the seat is held', () => {
    renderCard(expiresMs - 95_000);

    expect(screen.getByTestId('hold-status')).toHaveTextContent('HELD');
    expect(screen.getByTestId('countdown')).toHaveTextContent('1:35');
    expect(screen.getByRole('button', { name: 'Pay fee' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeEnabled();
  });

  it('switches to EXPIRED and disables Pay fee when time runs out, without a reload', () => {
    const { rerender, onExpired, onPay, onCancel } = renderCard(expiresMs - 1_000);
    expect(screen.getByRole('button', { name: 'Pay fee' })).toBeEnabled();

    // Time passes: same hold data from the server, only the clock moved.
    rerender(<HoldCard hold={base} now={expiresMs + 1} busy={null} highlighted={false} onPay={onPay} onCancel={onCancel} onExpired={onExpired} />);

    expect(screen.getByTestId('hold-status')).toHaveTextContent('EXPIRED');
    expect(screen.getByRole('button', { name: 'Pay fee' })).toBeDisabled();
    expect(screen.queryByTestId('countdown')).not.toBeInTheDocument();
    expect(onExpired).toHaveBeenCalledTimes(1);
  });

  it('shows a confirmed admission as paid', () => {
    renderCard(expiresMs - 60_000, { ...base, status: 'CONFIRMED' });

    expect(screen.getByTestId('hold-status')).toHaveTextContent('CONFIRMED');
    expect(screen.getByRole('button', { name: 'Fee paid' })).toBeDisabled();
  });
});
