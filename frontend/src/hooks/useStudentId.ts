import { useCallback, useState } from 'react';
import { isValidStudentId } from '../lib/studentId';

const STORAGE_KEY = 'school-admission.studentId';

function readStored(): string {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY) ?? '';
    return isValidStudentId(value) ? value : '';
  } catch {
    return ''; // storage blocked (private mode etc.)
  }
}

/**
 * The active student's birth registration number, kept in localStorage so the
 * student still sees their applications after a refresh or browser restart.
 */
export function useStudentId(): [string, (value: string) => void] {
  const [studentId, setStudentIdState] = useState(readStored);

  const setStudentId = useCallback((value: string) => {
    setStudentIdState(value);
    try {
      if (value) window.localStorage.setItem(STORAGE_KEY, value);
      else window.localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }, []);

  return [studentId, setStudentId];
}
