import { useEffect, useRef } from 'react';
import { formatCountdown, msUntil } from '../lib/serverClock';
import type { Hold, HoldStatus } from '../lib/types';

interface Props {
  hold: Hold;
  now: number;
  busy: 'pay' | 'cancel' | null;
  highlighted: boolean;
  onPay: (hold: Hold) => void;
  onCancel: (hold: Hold) => void;
  onExpired: (hold: Hold) => void;
}

const STATUS_TEXT: Record<HoldStatus, string> = {
  HELD: 'Seat held – pay to confirm',
  CONFIRMED: 'Admission confirmed',
  EXPIRED: 'Expired – seat released',
  CANCELLED: 'Cancelled',
};

/** The status of a HELD hold whose time is over is shown as EXPIRED right away. */
export function displayStatus(hold: Hold, now: number): HoldStatus {
  return hold.status === 'HELD' && msUntil(hold.expiresAt, now) <= 0 ? 'EXPIRED' : hold.status;
}

/** One application: status, live countdown, Pay fee and Cancel buttons. */
export function HoldCard({ hold, now, busy, highlighted, onPay, onCancel, onExpired }: Props) {
  const status = displayStatus(hold, now);
  const remaining = msUntil(hold.expiresAt, now);
  const isHeld = status === 'HELD';

  // Tell the parent once when the countdown reaches zero so it can refresh from the server.
  const reported = useRef(false);
  useEffect(() => {
    if (hold.status === 'HELD' && status === 'EXPIRED' && !reported.current) {
      reported.current = true;
      onExpired(hold);
    }
  }, [hold, status, onExpired]);

  const urgent = isHeld && remaining < 60_000;

  return (
    <li
      className={`card hold-card hold-card--${status.toLowerCase()} ${highlighted ? 'hold-card--new' : ''}`}
      data-testid="hold-card"
    >
      <div className="hold-card__head">
        <h3 className="hold-card__class">{hold.className}</h3>
        <span className={`badge badge--${status.toLowerCase()}`} data-testid="hold-status">{status}</span>
      </div>

      <p className="hold-card__text">{STATUS_TEXT[status]}</p>

      {isHeld && (
        <p className={`countdown ${urgent ? 'countdown--urgent' : ''}`} aria-live="polite">
          Time left to pay: <strong data-testid="countdown">{formatCountdown(remaining)}</strong>
        </p>
      )}
      {status === 'EXPIRED' && (
        <p className="muted small">The fee was not paid in time. You can apply again if seats are free.</p>
      )}

      <div className="hold-card__actions">
        <button
          type="button"
          className="btn btn--success"
          disabled={!isHeld || busy !== null}
          onClick={() => onPay(hold)}
        >
          {busy === 'pay' ? 'Paying…' : status === 'CONFIRMED' ? 'Fee paid' : 'Pay fee'}
        </button>
        <button
          type="button"
          className="btn btn--ghost"
          disabled={!isHeld || busy !== null}
          onClick={() => onCancel(hold)}
        >
          {busy === 'cancel' ? 'Cancelling…' : 'Cancel'}
        </button>
      </div>

      <p className="hold-card__meta">Application ID: <code>{hold.holdId}</code></p>
    </li>
  );
}
