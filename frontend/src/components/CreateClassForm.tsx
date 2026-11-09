import { useState, type FormEvent } from 'react';
import { api, errorMessage } from '../lib/api';

interface Props {
  onCreated: (name: string) => void;
}

/** School admin: create a class (one per grade + shift) with its seat count. */
export function CreateClassForm({ onCreated }: Props) {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    const totalSeats = Number(seats);

    if (!trimmed) return setError('Please enter a class name, e.g. "Class 6 - Morning Shift".');
    if (!Number.isInteger(totalSeats) || totalSeats < 1 || totalSeats > 100000) {
      return setError('Seats must be a whole number between 1 and 100000.');
    }

    setSaving(true);
    setError(null);
    try {
      await api.createClass(trimmed, totalSeats);
      setName('');
      setSeats('');
      onCreated(trimmed);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="card create-class" onSubmit={submit} noValidate>
      <h3 className="card__title">Create a class <span className="tag">Admin</span></h3>
      <div className="create-class__row">
        <label className="field field--grow">
          <span className="field__label">Class name</span>
          <input
            className="input"
            placeholder="Class 6 - Morning Shift"
            maxLength={100}
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </label>
        <label className="field field--seats">
          <span className="field__label">Seats</span>
          <input
            className="input"
            type="number"
            min={1}
            max={100000}
            placeholder="120"
            value={seats}
            onChange={(e) => setSeats(e.target.value)}
          />
        </label>
        <button type="submit" className="btn btn--primary" disabled={saving}>
          {saving ? 'Creating…' : 'Create'}
        </button>
      </div>
      {error && <p className="field__error" role="alert">{error}</p>}
    </form>
  );
}
