import { useCallback, useMemo, useRef, useState } from 'react';
import { ClassList } from './components/ClassList';
import { CreateClassForm } from './components/CreateClassForm';
import { HoldCard } from './components/HoldCard';
import { Notices, type NoticeMessage } from './components/Notice';
import { StudentBar } from './components/StudentBar';
import { useNow } from './hooks/useNow';
import { usePolling } from './hooks/usePolling';
import { useStudentId } from './hooks/useStudentId';
import { api, ApiError, errorMessage } from './lib/api';
import { POLL_INTERVAL_MS } from './lib/config';
import { serverNow, syncServerTime } from './lib/serverClock';
import type { Hold, SchoolClass } from './lib/types';

export default function App() {
  const [studentId, setStudentId] = useStudentId();
  const now = useNow();

  // ---------- notices ----------
  const [notices, setNotices] = useState<NoticeMessage[]>([]);
  const nextNoticeId = useRef(1);
  const dismiss = useCallback((id: number) => setNotices((list) => list.filter((n) => n.id !== id)), []);
  const notify = useCallback(
    (kind: NoticeMessage['kind'], text: string) => {
      const id = nextNoticeId.current++;
      setNotices((list) => [...list.slice(-3), { id, kind, text }]);
      window.setTimeout(() => dismiss(id), kind === 'error' ? 8000 : 5000);
    },
    [dismiss],
  );

  // ---------- data (polled) ----------
  const classes = usePolling(api.listClasses, POLL_INTERVAL_MS);

  const loadMyHolds = useCallback(async () => {
    const sentAt = Date.now();
    const holds = await api.myHolds(studentId);
    // Use the middle of the round-trip as the moment the server answered.
    syncServerTime(holds[0]?.serverTime, (sentAt + Date.now()) / 2);
    return holds;
  }, [studentId]);
  const myHolds = usePolling(loadMyHolds, POLL_INTERVAL_MS, !!studentId);

  const refreshAll = useCallback(async () => {
    await Promise.all([classes.refresh(), studentId ? myHolds.refresh() : Promise.resolve()]);
  }, [classes, myHolds, studentId]);

  /** The student's HELD (not yet timed out) or CONFIRMED application per class. */
  const activeHoldsByClass = useMemo(() => {
    const map = new Map<string, Hold>();
    for (const hold of myHolds.data ?? []) {
      const stillHeld = hold.status === 'HELD' && Date.parse(hold.expiresAt) > serverNow();
      if (stillHeld || hold.status === 'CONFIRMED') map.set(hold.classId, hold);
    }
    return map;
    // `now` is a dependency so a hold drops out of the map the moment it expires.
  }, [myHolds.data, now]); // eslint-disable-line react-hooks/exhaustive-deps

  // ---------- actions ----------
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);
  const [busyHold, setBusyHold] = useState<{ holdId: string; action: 'pay' | 'cancel' } | null>(null);
  const [highlightHoldId, setHighlightHoldId] = useState<string | null>(null);

  async function apply(schoolClass: SchoolClass) {
    setApplyingClassId(schoolClass.id);
    try {
      const sentAt = Date.now();
      const hold = await api.createHold(schoolClass.id, studentId);
      syncServerTime(hold.serverTime, (sentAt + Date.now()) / 2);
      setHighlightHoldId(hold.holdId);
      notify('success', `A seat in "${schoolClass.name}" is held for you. Please pay the fee before the time runs out.`);
    } catch (err) {
      notify('error', errorMessage(err));
    } finally {
      setApplyingClassId(null);
      await refreshAll();
    }
  }

  async function pay(hold: Hold) {
    setBusyHold({ holdId: hold.holdId, action: 'pay' });
    try {
      await api.confirmHold(hold.holdId, studentId);
      notify('success', `Payment received. Admission to "${hold.className}" is confirmed!`);
    } catch (err) {
      notify('error', errorMessage(err));
    } finally {
      setBusyHold(null);
      await refreshAll();
    }
  }

  async function cancel(hold: Hold) {
    if (!window.confirm(`Cancel the application for "${hold.className}"? The seat will be given to someone else.`)) {
      return;
    }
    setBusyHold({ holdId: hold.holdId, action: 'cancel' });
    try {
      await api.cancelHold(hold.holdId, studentId);
      notify('info', `Application for "${hold.className}" was cancelled.`);
    } catch (err) {
      notify('error', errorMessage(err));
    } finally {
      setBusyHold(null);
      await refreshAll();
    }
  }

  const onExpired = useCallback(
    (hold: Hold) => {
      notify('error', `Time is up for "${hold.className}". The seat was released.`);
      void refreshAll();
    },
    [notify, refreshAll],
  );

  // ---------- connection status ----------
  const offline = [classes.error, myHolds.error].some((e) => e instanceof ApiError && e.isNetworkError);
  const loadError = [classes.error, myHolds.error].find((e) => e && !(e instanceof ApiError && e.isNetworkError));

  const holds = myHolds.data ?? [];
  const activeCount = holds.filter((h) => h.status === 'HELD' && Date.parse(h.expiresAt) > now).length;

  return (
    <div className="app">
      <header className="app-header">
        <div className="app-header__brand">
          <span className="app-header__logo" aria-hidden>🏫</span>
          <div>
            <h1>School Admission</h1>
            <p className="muted">First come, first served · pay the fee before your seat hold expires</p>
          </div>
        </div>
        <StudentBar
          studentId={studentId}
          onChange={(id) => {
            setStudentId(id);
            setHighlightHoldId(null);
          }}
        />
      </header>

      {offline && (
        <div className="banner banner--error" role="alert">
          Cannot reach the server. Seat numbers may be out of date. Retrying automatically…
        </div>
      )}
      {!offline && loadError !== undefined && (
        <div className="banner banner--warning" role="alert">{errorMessage(loadError)}</div>
      )}

      <main className="layout">
        <section className="layout__classes" aria-labelledby="classes-title">
          <div className="section-head">
            <h2 id="classes-title">Classes</h2>
            <span className="muted small">Updates every {POLL_INTERVAL_MS / 1000}s</span>
          </div>
          <ClassList
            classes={classes.data}
            loading={classes.loading}
            failed={classes.error !== undefined}
            studentId={studentId}
            activeHoldsByClass={activeHoldsByClass}
            applyingClassId={applyingClassId}
            onApply={apply}
          />
          <CreateClassForm
            onCreated={(name) => {
              notify('success', `Class "${name}" was created.`);
              void classes.refresh();
            }}
          />
        </section>

        <section className="layout__holds" aria-labelledby="holds-title">
          <div className="section-head">
            <h2 id="holds-title">My applications</h2>
            {activeCount > 0 && <span className="tag tag--warning">{activeCount} waiting for payment</span>}
          </div>

          {!studentId && (
            <p className="empty">Enter the student's birth registration number at the top to see and make applications.</p>
          )}
          {studentId && myHolds.loading && !myHolds.data && <p className="muted">Loading applications…</p>}
          {studentId && myHolds.data && holds.length === 0 && (
            <p className="empty">No applications yet. Choose a class and press <strong>Apply</strong>.</p>
          )}

          {holds.length > 0 && (
            <ul className="hold-list">
              {holds.map((hold) => (
                <HoldCard
                  key={hold.holdId}
                  hold={hold}
                  now={now}
                  busy={busyHold?.holdId === hold.holdId ? busyHold.action : null}
                  highlighted={hold.holdId === highlightHoldId}
                  onPay={pay}
                  onCancel={cancel}
                  onExpired={onExpired}
                />
              ))}
            </ul>
          )}
        </section>
      </main>

      <Notices notices={notices} onDismiss={dismiss} />
    </div>
  );
}
