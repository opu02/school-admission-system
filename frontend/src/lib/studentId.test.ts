import { describe, expect, it } from 'vitest';
import { isValidStudentId, studentIdProblem } from './studentId';

describe('student id (birth registration number)', () => {
  it('accepts exactly 17 digits', () => {
    expect(isValidStudentId('20152691234567890')).toBe(true);
    expect(studentIdProblem('20152691234567890')).toBeNull();
  });

  it.each([
    ['', 'Please enter'],
    ['12345', 'exactly 17 digits (you entered 5)'],
    ['2015269123456789A', 'Only numbers'],
    ['201526912345678901', 'exactly 17 digits (you entered 18)'],
  ])('rejects %j with a clear message', (value, message) => {
    expect(isValidStudentId(value)).toBe(false);
    expect(studentIdProblem(value)).toContain(message);
  });
});
