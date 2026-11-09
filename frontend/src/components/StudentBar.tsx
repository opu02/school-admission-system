import { useState, type FormEvent } from 'react';
import { STUDENT_ID_LENGTH, studentIdProblem } from '../lib/studentId';

interface Props {
  studentId: string;
  onChange: (studentId: string) => void;
}

/** Top bar: the student's 17-digit birth registration number (no login in this system). */
export function StudentBar({ studentId, onChange }: Props) {
  const [draft, setDraft] = useState(studentId);
  const [touched, setTouched] = useState(false);
  const [editing, setEditing] = useState(!studentId);
  const problem = studentIdProblem(draft);

  function submit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);
    if (problem) return;
    onChange(draft);
    setEditing(false);
  }

  if (!editing && studentId) {
    return (
      <section className="student-bar" aria-label="Student">
        <div>
          <span className="student-bar__label">Applying as student</span>
          <strong className="student-bar__id" data-testid="active-student-id">{studentId}</strong>
        </div>
        <button
          type="button"
          className="btn btn--ghost"
          onClick={() => {
            setDraft(studentId);
            setTouched(false);
            setEditing(true);
          }}
        >
          Change student
        </button>
      </section>
    );
  }

  return (
    <form className="student-bar student-bar--editing" onSubmit={submit} noValidate aria-label="Student">
      <label className="field">
        <span className="field__label">Student's birth registration number (17 digits)</span>
        <input
          className="input input--mono"
          inputMode="numeric"
          autoComplete="off"
          placeholder="e.g. 20152691234567890"
          maxLength={STUDENT_ID_LENGTH}
          value={draft}
          onChange={(e) => setDraft(e.target.value.replace(/\s/g, ''))}
          onBlur={() => setTouched(true)}
          aria-invalid={touched && !!problem}
          autoFocus
        />
        {touched && problem && <span className="field__error" role="alert">{problem}</span>}
      </label>
      <div className="student-bar__actions">
        <button type="submit" className="btn btn--primary">Continue</button>
        {studentId && (
          <button type="button" className="btn btn--ghost" onClick={() => setEditing(false)}>
            Cancel
          </button>
        )}
      </div>
    </form>
  );
}
