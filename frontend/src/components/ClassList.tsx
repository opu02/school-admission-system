import type { Hold, SchoolClass } from '../lib/types';

interface Props {
  classes: SchoolClass[] | undefined;
  loading: boolean;
  failed: boolean;
  studentId: string;
  activeHoldsByClass: Map<string, Hold>;
  applyingClassId: string | null;
  onApply: (schoolClass: SchoolClass) => void;
}

/** All classes with live seat counts and an Apply button. */
export function ClassList({ classes, loading, failed, studentId, activeHoldsByClass, applyingClassId, onApply }: Props) {
  if (loading && !classes) return <p className="muted">Loading classes…</p>;
  if (failed && !classes) return <p className="empty">Classes could not be loaded yet. We will keep trying.</p>;
  if (!classes || classes.length === 0) {
    return <p className="empty">No classes yet. An admin can create one below.</p>;
  }

  return (
    <ul className="class-list">
      {classes.map((c) => {
        const active = activeHoldsByClass.get(c.id);
        const full = c.availableSeats <= 0;
        const takenPct = c.totalSeats ? ((c.confirmedSeats + c.heldSeats) / c.totalSeats) * 100 : 0;
        const confirmedPct = c.totalSeats ? (c.confirmedSeats / c.totalSeats) * 100 : 0;

        let label = 'Apply';
        let disabledReason: string | null = null;
        if (applyingClassId === c.id) label = 'Applying…';
        if (!studentId) disabledReason = 'Enter the birth registration number above first.';
        else if (active) {
          label = active.status === 'CONFIRMED' ? 'Admitted' : 'Applied';
          disabledReason = active.status === 'CONFIRMED'
            ? 'This student is already admitted to this class.'
            : 'You already have an active application here.';
        } else if (full) {
          label = 'Full';
          disabledReason = 'No free seats right now.';
        }

        return (
          <li key={c.id} className="card class-card" data-testid="class-card">
            <div className="class-card__head">
              <h3 className="class-card__name">{c.name}</h3>
              <span className={`seats-left ${full ? 'seats-left--full' : ''}`}>
                {full ? 'Full' : `${c.availableSeats} free`}
              </span>
            </div>

            <div className="seat-bar" role="img" aria-label={`${c.confirmedSeats} confirmed, ${c.heldSeats} held, ${c.availableSeats} free of ${c.totalSeats}`}>
              <span className="seat-bar__confirmed" style={{ width: `${confirmedPct}%` }} />
              <span className="seat-bar__held" style={{ width: `${Math.max(0, takenPct - confirmedPct)}%` }} />
            </div>

            <dl className="seat-stats">
              <div><dt>Total</dt><dd>{c.totalSeats}</dd></div>
              <div><dt>Held</dt><dd>{c.heldSeats}</dd></div>
              <div><dt>Confirmed</dt><dd>{c.confirmedSeats}</dd></div>
              <div><dt>Available</dt><dd>{c.availableSeats}</dd></div>
            </dl>

            <div className="class-card__actions">
              <button
                type="button"
                className="btn btn--primary"
                disabled={!!disabledReason || applyingClassId !== null}
                title={disabledReason ?? undefined}
                onClick={() => onApply(c)}
              >
                {label}
              </button>
              {disabledReason && studentId && <span className="muted small">{disabledReason}</span>}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
